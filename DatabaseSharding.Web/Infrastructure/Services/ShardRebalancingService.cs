using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Services;
using Domain.Models;
using Infrastructure.Configuration;
using Infrastructure.Data.Contexts;
using Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public partial class ShardRebalancingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
    private readonly ShardingOptions _options;
    private readonly ILogger<ShardRebalancingService> _logger;

    public ShardRebalancingService(
        IServiceProvider serviceProvider,
        IOptions<ShardingOptions> options,
        ILogger<ShardRebalancingService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformHealthChecksAsync(stoppingToken);
                    await Task.Delay(_options.HealthCheckInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during health check execution");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var shardingService = scope.ServiceProvider.GetRequiredService<IShardingService>();

            _logger.LogInformation("Performing health checks on all shards");

            var shards = await shardingService.GetAllShardsAsync(cancellationToken);
            var unhealthyShards = shards.Where(s => !s.IsActive).ToList();

            if (unhealthyShards.Any())
            {
                _logger.LogWarning("Found {Count} unhealthy shards: {ShardIds}",
                    unhealthyShards.Count,
                    string.Join(", ", unhealthyShards.Select(s => s.ShardId)));
            }
            else
            {
                _logger.LogInformation("All shards are healthy");
            }
        }
        public async Task<bool> RebalanceToNewShardAsync(string newShardConnectionString)
        {
            using var scope = _serviceProvider.CreateScope();
            var hashRing = scope.ServiceProvider.GetRequiredService<IHashingService>();
            var shardConnectionService = scope.ServiceProvider.GetRequiredService<IShardConnectionService>();

            try
            {
                var newShardId = $"shard_{Guid.NewGuid():N}";
                _logger.LogInformation("Starting rebalancing to new shard {ShardId}", newShardId);

                // Step 1: Get rebalance mapping before adding the new shard
                var rebalanceMapping = hashRing.GetRebalanceMapping(newShardId);

                // Step 2: Initialize the new shard
                await InitializeNewShardAsync(newShardId, newShardConnectionString);

                // Step 3: Add the new shard to the ring
                hashRing.AddShard(newShardId);

                // Step 4: Perform data migration
                foreach (var sourceShardMapping in rebalanceMapping)
                {
                    await MigrateDataAsync(sourceShardMapping.Key, newShardId, sourceShardMapping.Value);
                }

                _logger.LogInformation("Completed rebalancing to new shard {ShardId}", newShardId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rebalance to new shard");
                return false;
            }
        }

        private async Task InitializeNewShardAsync(string shardId, string connectionString)
        {
            _logger.LogInformation("Initializing new shard {ShardId}", shardId);

            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<ShardDbContext>();
                optionsBuilder.UseNpgsql(connectionString);

                using var context = new ShardDbContext(optionsBuilder.Options);

                // Create database and tables
                await context.Database.EnsureCreatedAsync();

                // Verify table exists
                var userCount = await context.Users.CountAsync();
                _logger.LogInformation("New shard {ShardId} initialized with {Count} users", shardId, userCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize new shard {ShardId}", shardId);
                throw;
            }
        }

        private async Task MigrateDataAsync(string sourceShardId, string targetShardId, List<string> keysToMigrate)
        {
            if (!keysToMigrate.Any())
            {
                _logger.LogInformation("No data to migrate from {SourceShard} to {TargetShard}",
                    sourceShardId, targetShardId);
                return;
            }

            _logger.LogInformation("Migrating {Count} keys from {SourceShard} to {TargetShard}",
                keysToMigrate.Count, sourceShardId, targetShardId);

            using var scope = _serviceProvider.CreateScope();
            var shardConnectionService = scope.ServiceProvider.GetRequiredService<IShardConnectionService>();

            try
            {
                using var sourceContext = await shardConnectionService.GetContextForShardAsync(sourceShardId);
                using var targetContext = await shardConnectionService.GetContextForShardAsync(targetShardId);

                // Get users that need to be migrated based on their email patterns
                var usersToMigrate = await GetUsersToMigrateAsync(sourceContext, keysToMigrate);

                if (!usersToMigrate.Any())
                {
                    _logger.LogInformation("No actual users found to migrate from {SourceShard} to {TargetShard}",
                        sourceShardId, targetShardId);
                    return;
                }

                // Begin transaction for data consistency
                using var sourceTransaction = await sourceContext.Database.BeginTransactionAsync();
                using var targetTransaction = await targetContext.Database.BeginTransactionAsync();

                try
                {
                    // Step 1: Copy users to target shard
                    foreach (var user in usersToMigrate)
                    {
                        var newUser = new User(
                            user.Email, user.FirstName, user.LastName);

                        targetContext.Users.Add(newUser);
                    }

                    await targetContext.SaveChangesAsync();

                    // Step 2: Verify data was copied correctly
                    var copiedCount = await targetContext.Users
                        .CountAsync(u => usersToMigrate.Select(m => m.Email.Value).Contains(u.Email.Value));

                    if (copiedCount != usersToMigrate.Count)
                    {
                        throw new InvalidOperationException(
                            $"Data verification failed: Expected {usersToMigrate.Count}, got {copiedCount}");
                    }

                    // Step 3: Remove users from source shard
                    sourceContext.Users.RemoveRange(usersToMigrate);
                    await sourceContext.SaveChangesAsync();

                    // Step 4: Commit both transactions
                    await targetTransaction.CommitAsync();
                    await sourceTransaction.CommitAsync();

                    _logger.LogInformation("Successfully migrated {Count} users from {SourceShard} to {TargetShard}",
                        usersToMigrate.Count, sourceShardId, targetShardId);
                }
                catch (Exception ex)
                {
                    // Rollback both transactions on error
                    await targetTransaction.RollbackAsync();
                    await sourceTransaction.RollbackAsync();
                    throw new InvalidOperationException($"Migration failed: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate data from {SourceShard} to {TargetShard}",
                    sourceShardId, targetShardId);
                throw;
            }
        }

        private async Task<List<User>> GetUsersToMigrateAsync(
            ShardDbContext sourceContext,
            List<string> sampleKeys)
        {
            // Since we have sample keys, we need to find actual users whose emails
            // would map to the same hash ranges as these sample keys

            var allUsers = await sourceContext.Users.ToListAsync();
            var usersToMigrate = new List<User>();

            using var scope = _serviceProvider.CreateScope();
            var hashingService = scope.ServiceProvider.GetRequiredService<IHashingService>();

            foreach (var user in allUsers)
            {
                // Check if this user's email would be affected by the rebalancing
                // This is a simplified approach - in production, you'd use more sophisticated logic
                var userEmailHash = ComputeSHA256Hash(user.Email.Value);
                var sampleHash = ComputeSHA256Hash(sampleKeys.First());

                // If the hashes are in similar ranges, this user should be migrated
                if (Math.Abs((int)userEmailHash - (int)sampleHash) < uint.MaxValue / 10)
                {
                    usersToMigrate.Add(user);
                }
            }

            return usersToMigrate;
        }

        private uint ComputeSHA256Hash(string input)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
