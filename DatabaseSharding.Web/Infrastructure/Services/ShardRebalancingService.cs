using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Services;
using Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ShardRebalancingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ShardingOptions _options;
        private readonly ILogger<ShardRebalancingService> _logger;

        public ShardRebalancingService(
            IServiceProvider serviceProvider,
            IOptions<ShardingOptions> options,
            ILogger<ShardRebalancingService> logger)
        {
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _logger = logger;
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
    }
}
