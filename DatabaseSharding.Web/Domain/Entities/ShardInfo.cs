using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class ShardInfo
    {
        private ShardInfo() { }

        public ShardInfo(string shardId, string connectionString, int weight = 100)
        {
            ShardId = shardId ?? throw new ArgumentNullException(nameof(shardId));
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Weight = weight;
            IsActive = true;
            LastHealthCheck = DateTime.UtcNow;
        }

        public string ShardId { get; private set; } = string.Empty;
        public string ConnectionString { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }
        public int Weight { get; private set; }
        public DateTime LastHealthCheck { get; private set; }
        public bool IsRebalancing { get; private set; }

        public void UpdateHealth(bool isHealthy)
        {
            IsActive = isHealthy;
            LastHealthCheck = DateTime.UtcNow;
        }

        public void StartRebalancing() => IsRebalancing = true;
        public void CompleteRebalancing() => IsRebalancing = false;
        public void UpdateWeight(int weight) => Weight = weight;
    }

}
