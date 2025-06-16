using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly IShardingService _shardingService;

        public DatabaseHealthCheck(IShardingService shardingService)
        {
            _shardingService = shardingService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                var shards = await _shardingService.GetAllShardsAsync(cancellationToken);
                var healthyShards = shards.Count(s => s.IsActive);
                var totalShards = shards.Count;

                if (healthyShards == 0)
                {
                    return HealthCheckResult.Unhealthy("All shards are down");
                }

                if (healthyShards < totalShards)
                {
                    return HealthCheckResult.Degraded($"{healthyShards}/{totalShards} shards are healthy");
                }

                return HealthCheckResult.Healthy($"All {totalShards} shards are healthy");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Failed to check shard health", ex);
            }
        }
    }
}
