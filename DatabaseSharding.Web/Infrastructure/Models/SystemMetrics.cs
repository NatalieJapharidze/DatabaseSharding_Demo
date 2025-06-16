using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Models
{
    public class SystemMetrics
    {
        public int TotalShards { get; set; }
        public int HealthyShards { get; set; }
        public int TotalUsers { get; set; }
        public double AverageUsersPerShard { get; set; }
        public double AverageResponseTime { get; set; }
        public DateTime LastCalculated { get; set; }
        public Dictionary<string, int> ShardDistribution { get; set; } = new();
    }
}
