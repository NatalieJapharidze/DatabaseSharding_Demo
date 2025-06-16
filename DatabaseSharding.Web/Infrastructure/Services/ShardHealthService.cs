using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Services;
using Domain.Models;
using Infrastructure.Configuration;
using Infrastructure.Interfaces;
using Infrastructure.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ShardHealthService : IShardHealthService
    {
        private readonly IShardConnectionService _shardConnectionService;
        private readonly IHashingService _hashingService;
        private readonly ShardingOptions _options;
        private readonly ILogger<ShardHealthService> _logger;

        public ShardHealthService(
            IShardConnectionService shardConnectionService,
            IHashingService hashingService,
            IOptions<ShardingOptions> options,
            ILogger<ShardHealthService> logger)
        {
            _shardConnectionService = shardConnectionService;
            _hashingService = hashingService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<List<ShardHealthStatus>> CheckAllShardsHealthAsync(CancellationToken cancellationToken = default)
        {
            var shardIds = _hashingService.GetAllShards();
            var healthCheckTasks = shardIds.Select(shardId => CheckShardHealthAsync(shardId, cancellationToken));

            return (await Task.WhenAll(healthCheckTasks)).ToList();
        }

        public async Task<ShardHealthStatus> CheckShardHealthAsync(string shardId, CancellationToken cancellationToken = default)
        {
            var healthStatus = new ShardHealthStatus
            {
                ShardId = shardId,
                CheckTime = DateTime.UtcNow
            };

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Test connectivity
                var canConnect = await _shardConnectionService.TestShardConnectivityAsync(shardId, cancellationToken);
                healthStatus.CanConnect = canConnect;

                if (canConnect)
                {
                    using var context = await _shardConnectionService.GetContextForShardAsync(shardId, cancellationToken);

                    // Test table access
                    try
                    {
                        var userCount = await context.Users.CountAsync(cancellationToken);
                        healthStatus.TableAccessible = true;
                        healthStatus.RecordCount = userCount;
                    }
                    catch (Exception ex)
                    {
                        healthStatus.TableAccessible = false;
                        healthStatus.Errors.Add($"Table access failed: {ex.Message}");
                    }

                    // Test write operations
                    try
                    {
                        var testUser = new User(
                            new Email($"health-check-{Guid.NewGuid()}@test.com"),
                            "Health",
                            "Check");

                        context.Users.Add(testUser);
                        await context.SaveChangesAsync(cancellationToken);

                        // Clean up test data
                        context.Users.Remove(testUser);
                        await context.SaveChangesAsync(cancellationToken);

                        healthStatus.WriteOperationsWork = true;
                    }
                    catch (Exception ex)
                    {
                        healthStatus.WriteOperationsWork = false;
                        healthStatus.Errors.Add($"Write operations failed: {ex.Message}");
                    }
                }
                else
                {
                    healthStatus.Errors.Add("Cannot connect to database");
                }

                stopwatch.Stop();
                healthStatus.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                healthStatus.IsHealthy = canConnect && healthStatus.TableAccessible && healthStatus.WriteOperationsWork;
            }
            catch (Exception ex)
            {
                healthStatus.IsHealthy = false;
                healthStatus.Errors.Add($"Health check failed: {ex.Message}");
                _logger.LogError(ex, "Health check failed for shard {ShardId}", shardId);
            }

            return healthStatus;
        }

        public async Task<bool> IsSystemHealthyAsync(CancellationToken cancellationToken = default)
        {
            var healthStatuses = await CheckAllShardsHealthAsync(cancellationToken);
            var healthyShards = healthStatuses.Count(s => s.IsHealthy);
            var totalShards = healthStatuses.Count;

            // System is healthy if at least 70% of shards are healthy
            var healthThreshold = Math.Ceiling(totalShards * 0.7);
            return healthyShards >= healthThreshold;
        }
    }
}
