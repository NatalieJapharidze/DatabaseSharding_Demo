using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Services;
using Infrastructure.Data.Contexts;
using Infrastructure.Interfaces;
using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    public class ShardMetricsService : IShardMetricsService
    {
        private readonly IShardConnectionService _shardConnectionService;
        private readonly IHashingService _hashingService;
        private readonly ILogger<ShardMetricsService> _logger;

        public ShardMetricsService(
            IShardConnectionService shardConnectionService,
            IHashingService hashingService,
            ILogger<ShardMetricsService> logger)
        {
            _shardConnectionService = shardConnectionService;
            _hashingService = hashingService;
            _logger = logger;
        }

        public async Task<ShardMetrics> GetShardMetricsAsync(string shardId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var context = await _shardConnectionService.GetContextForShardAsync(shardId, cancellationToken);

                var metrics = new ShardMetrics
                {
                    ShardId = shardId,
                    UserCount = await context.Users.CountAsync(cancellationToken),
                    IsHealthy = await context.Database.CanConnectAsync(cancellationToken),
                    LastHealthCheck = DateTime.UtcNow,
                    ConnectionString = MaskConnectionString(context.Database.GetConnectionString()),
                    DatabaseSize = await GetDatabaseSizeAsync(context, cancellationToken),
                    AverageResponseTime = await MeasureResponseTimeAsync(context, cancellationToken)
                };

                return metrics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metrics for shard {ShardId}", shardId);
                return new ShardMetrics
                {
                    ShardId = shardId,
                    IsHealthy = false,
                    LastHealthCheck = DateTime.UtcNow,
                    Error = ex.Message
                };
            }
        }

        public async Task<List<ShardMetrics>> GetAllShardMetricsAsync(CancellationToken cancellationToken = default)
        {
            var shardIds = _hashingService.GetAllShards();
            var tasks = shardIds.Select(shardId => GetShardMetricsAsync(shardId, cancellationToken));
            return (await Task.WhenAll(tasks)).ToList();
        }

        public async Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default)
        {
            var shardMetrics = await GetAllShardMetricsAsync(cancellationToken);

            return new SystemMetrics
            {
                TotalShards = shardMetrics.Count,
                HealthyShards = shardMetrics.Count(s => s.IsHealthy),
                TotalUsers = shardMetrics.Sum(s => s.UserCount),
                AverageUsersPerShard = shardMetrics.Count > 0 ? shardMetrics.Average(s => s.UserCount) : 0,
                AverageResponseTime = shardMetrics.Where(s => s.AverageResponseTime > 0).Select(s => s.AverageResponseTime).DefaultIfEmpty(0).Average(),
                LastCalculated = DateTime.UtcNow,
                ShardDistribution = shardMetrics.ToDictionary(s => s.ShardId, s => s.UserCount)
            };
        }

        private async Task<long> GetDatabaseSizeAsync(ShardDbContext context, CancellationToken cancellationToken)
        {
            try
            {
                // Get database size in bytes (PostgreSQL specific)
                const string sizeSql = @"
                SELECT pg_database_size(current_database()) as size_bytes";

                using var command = context.Database.GetDbConnection().CreateCommand();
                command.CommandText = sizeSql;

                await context.Database.OpenConnectionAsync(cancellationToken);
                var result = await command.ExecuteScalarAsync(cancellationToken);

                return Convert.ToInt64(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get database size");
                return 0;
            }
            finally
            {
                if (context.Database.GetDbConnection().State == System.Data.ConnectionState.Open)
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
        }

        private async Task<double> MeasureResponseTimeAsync(ShardDbContext context, CancellationToken cancellationToken)
        {
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await context.Users.Take(1).ToListAsync(cancellationToken);
                stopwatch.Stop();

                return stopwatch.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to measure response time");
                return 0;
            }
        }

        private string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Unknown";

            try
            {
                var parts = connectionString.Split(';');
                var maskedParts = parts.Select(part =>
                {
                    if (part.Contains("Password", StringComparison.OrdinalIgnoreCase))
                        return "Password=***";
                    return part;
                });

                var masked = string.Join(";", maskedParts);
                return masked.Length > 80 ? masked.Substring(0, 80) + "..." : masked;
            }
            catch
            {
                return "Invalid connection string";
            }
        }
    }
}
