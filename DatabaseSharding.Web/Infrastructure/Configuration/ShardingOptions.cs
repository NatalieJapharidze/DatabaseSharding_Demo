using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Configuration
{
    public class ShardingOptions
    {
        public const string SectionName = "Sharding";

        public List<string> ShardConnectionStrings { get; set; } = new();
        public int VirtualNodesPerShard { get; set; } = 150;
        public int RebalanceBatchSize { get; set; } = 1000;
        public TimeSpan RebalanceInterval { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
        public int MaxRetryAttempts { get; set; } = 3;
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool InitializeDatabasesOnStartup { get; set; } = true;
        public bool SeedTestDataOnInitialization { get; set; } = true;
        public int DatabaseInitializationTimeoutSeconds { get; set; } = 30;
    }
}
