using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Models
{
    public class ShardMetrics
    {
        public string ShardId { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public string ConnectionString { get; set; } = string.Empty;
        public long DatabaseSize { get; set; }
        public double AverageResponseTime { get; set; }
        public string Error { get; set; } = string.Empty;
    }
}
