using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Models;
using Infrastructure.Configuration;
using Infrastructure.Data.Contexts;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class DatabaseInitializationService : IDatabaseInitializationService
    {
        private readonly ShardingOptions _options;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            IOptions<ShardingOptions> options,
            ILogger<DatabaseInitializationService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task InitializeAllDatabasesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting database initialization for {Count} shards", _options.ShardConnectionStrings.Count);

            var initializationTasks = _options.ShardConnectionStrings.Select(async (connectionString, index) =>
            {
                var shardId = $"shard_{index}";
                await InitializeSingleShardAsync(shardId, connectionString, cancellationToken);
            });

            try
            {
                await Task.WhenAll(initializationTasks);
                _logger.LogInformation("Database initialization completed successfully for all shards");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed for one or more shards");
                throw;
            }
        }

        private async Task InitializeSingleShardAsync(string shardId, string connectionString, CancellationToken cancellationToken)
        {
            const int maxRetries = 3;
            const int delayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Initializing database for {ShardId} (attempt {Attempt}/{MaxRetries})",
                        shardId, attempt, maxRetries);

                    var optionsBuilder = new DbContextOptionsBuilder<ShardDbContext>();
                    optionsBuilder.UseNpgsql(connectionString);
                    optionsBuilder.EnableSensitiveDataLogging();
                    optionsBuilder.EnableDetailedErrors();

                    using var context = new ShardDbContext(optionsBuilder.Options);

                    // Step 1: Ensure database exists
                    _logger.LogDebug("Ensuring database exists for {ShardId}", shardId);
                    var databaseCreated = await context.Database.EnsureCreatedAsync(cancellationToken);

                    if (databaseCreated)
                    {
                        _logger.LogInformation("Database created for {ShardId}", shardId);
                    }
                    else
                    {
                        _logger.LogInformation("Database already exists for {ShardId}", shardId);
                    }

                    // Step 2: Check if we can connect
                    _logger.LogDebug("Testing connection for {ShardId}", shardId);
                    var canConnect = await context.Database.CanConnectAsync(cancellationToken);

                    if (!canConnect)
                    {
                        throw new InvalidOperationException($"Cannot connect to database for {shardId}");
                    }

                    // Step 3: Verify schema exists by checking if Users table exists
                    _logger.LogDebug("Checking if Users table exists for {ShardId}", shardId);
                    var tableExists = await CheckIfUsersTableExistsAsync(context, cancellationToken);

                    if (!tableExists)
                    {
                        _logger.LogWarning("Users table doesn't exist for {ShardId}, attempting to create schema", shardId);

                        // Try multiple approaches to create the schema
                        await CreateSchemaAsync(context, shardId, cancellationToken);

                        // Verify the table was created
                        tableExists = await CheckIfUsersTableExistsAsync(context, cancellationToken);

                        if (!tableExists)
                        {
                            throw new InvalidOperationException($"Failed to create Users table for {shardId} after all attempts");
                        }

                        _logger.LogInformation("Successfully created Users table for {ShardId}", shardId);
                    }

                    // Step 4: Count existing records (only after confirming table exists)
                    int userCount = 0;
                    try
                    {
                        userCount = await context.Users.CountAsync(cancellationToken);
                        _logger.LogInformation("Successfully verified {ShardId}: Users table exists with {Count} records",
                            shardId, userCount);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to count users in {ShardId} despite table existing", shardId);
                        throw new InvalidOperationException($"Users table exists but cannot be accessed in {shardId}: {ex.Message}");
                    }

                    // Step 5: Seed test data if needed and enabled
                    if (userCount == 0 && _options.SeedTestDataOnInitialization)
                    {
                        await SeedTestDataAsync(context, shardId, cancellationToken);
                    }
                    else if (userCount == 0)
                    {
                        _logger.LogInformation("Test data seeding is disabled for {ShardId}", shardId);
                    }
                    else
                    {
                        _logger.LogInformation("Skipping test data seeding for {ShardId} - table already has data", shardId);
                    }

                    // Success - break retry loop
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Attempt {Attempt}/{MaxRetries} failed for {ShardId}: {Message}",
                        attempt, maxRetries, shardId, ex.Message);

                    if (attempt == maxRetries)
                    {
                        _logger.LogError("Failed to initialize {ShardId} after {MaxRetries} attempts", shardId, maxRetries);
                        throw;
                    }

                    // Wait before retrying
                    await Task.Delay(delayMs * attempt, cancellationToken);
                }
            }
        }

        private async Task<bool> CheckIfUsersTableExistsAsync(ShardDbContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Check if the Users table exists in the information schema
                const string checkTableSql = @"
                SELECT COUNT(*) 
                FROM information_schema.tables 
                WHERE table_schema = 'public' 
                AND table_name = 'Users'";

                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = checkTableSql;

                await context.Database.OpenConnectionAsync(cancellationToken);
                var result = await command.ExecuteScalarAsync(cancellationToken);

                var tableCount = Convert.ToInt32(result);
                var exists = tableCount > 0;

                _logger.LogDebug("Table existence check for Users: {Exists} (count: {Count})", exists, tableCount);
                return exists;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Users table existence check failed: {Message}", ex.Message);
                return false;
            }
            finally
            {
                if (context.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
        }

        private async Task CreateSchemaAsync(ShardDbContext context, string shardId, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Attempting to create schema for {ShardId}", shardId);

            try
            {
                // Method 1: Try EnsureCreated first
                _logger.LogDebug("Method 1: Using EnsureCreated for {ShardId}", shardId);
                await context.Database.EnsureCreatedAsync(cancellationToken);

                if (await CheckIfUsersTableExistsAsync(context, cancellationToken))
                {
                    _logger.LogInformation("Method 1 succeeded for {ShardId}", shardId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Method 1 (EnsureCreated) failed for {ShardId}", shardId);
            }

            try
            {
                // Method 2: Delete and recreate
                _logger.LogDebug("Method 2: Delete and recreate for {ShardId}", shardId);
                await context.Database.EnsureDeletedAsync(cancellationToken);
                await context.Database.EnsureCreatedAsync(cancellationToken);

                if (await CheckIfUsersTableExistsAsync(context, cancellationToken))
                {
                    _logger.LogInformation("Method 2 succeeded for {ShardId}", shardId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Method 2 (Delete/Recreate) failed for {ShardId}", shardId);
            }

            try
            {
                // Method 3: Manual table creation
                _logger.LogDebug("Method 3: Manual table creation for {ShardId}", shardId);
                await CreateUsersTableManuallyAsync(context, cancellationToken);

                if (await CheckIfUsersTableExistsAsync(context, cancellationToken))
                {
                    _logger.LogInformation("Method 3 succeeded for {ShardId}", shardId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Method 3 (Manual creation) failed for {ShardId}", shardId);
            }

            throw new InvalidOperationException($"All schema creation methods failed for {shardId}");
        }

        private async Task CreateUsersTableManuallyAsync(ShardDbContext context, CancellationToken cancellationToken)
        {
            const string createTableSql = @"
            -- Drop table if it exists (for clean state)
            DROP TABLE IF EXISTS ""Users"";
            
            -- Create the Users table
            CREATE TABLE ""Users"" (
                ""Id"" uuid NOT NULL DEFAULT gen_random_uuid(),
                ""Email"" character varying(255) NOT NULL,
                ""FirstName"" character varying(100) NOT NULL,
                ""LastName"" character varying(100) NOT NULL,
                ""CreatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                CONSTRAINT ""PK_Users"" PRIMARY KEY (""Id""),
                CONSTRAINT ""IX_Users_Email"" UNIQUE (""Email"")
            );
            
            -- Create index on Email for performance
            CREATE INDEX ""IX_Users_Email_Index"" ON ""Users"" (""Email"");
        ";

            _logger.LogDebug("Executing manual table creation SQL");
            await context.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
            _logger.LogDebug("Manual table creation SQL executed successfully");
        }

        private async Task CreateUsersTableAlternativeAsync(ShardDbContext context, CancellationToken cancellationToken)
        {
            // Alternative approach: Create table step by step
            var createCommands = new[]
            {
            @"CREATE TABLE IF NOT EXISTS ""Users"" (""Id"" uuid PRIMARY KEY DEFAULT gen_random_uuid())",
            @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Email"" varchar(255) NOT NULL",
            @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""FirstName"" varchar(100) NOT NULL",
            @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""LastName"" varchar(100) NOT NULL",
            @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""CreatedAt"" timestamptz NOT NULL DEFAULT NOW()",
            @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""UpdatedAt"" timestamptz NOT NULL DEFAULT NOW()",
            @"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Email_Unique"" ON ""Users"" (""Email"")"
        };

            foreach (var command in createCommands)
            {
                try
                {
                    await context.Database.ExecuteSqlRawAsync(command, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Command failed (may be expected): {Command}, Error: {Error}", command, ex.Message);
                }
            }
        }

        private async Task SeedTestDataAsync(ShardDbContext context, string shardId, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Seeding test data for {ShardId}", shardId);

                // Add a test user to verify the shard is working
                var testUser = new User(
                    new Email($"test-{shardId}@example.com"),
                    "Test",
                    $"User-{shardId}");

                context.Users.Add(testUser);
                await context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully seeded test user {UserId} for {ShardId}", testUser.Id, shardId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to seed test data for {ShardId}, but this is not critical", shardId);
            }
        }
    }
}
