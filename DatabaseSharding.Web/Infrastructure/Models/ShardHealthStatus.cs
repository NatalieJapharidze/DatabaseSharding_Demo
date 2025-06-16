using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Models
{
    public class ShardHealthStatus
    {
        public string ShardId { get; set; } = string.Empty;
        public DateTime CheckTime { get; set; }
        public bool IsHealthy { get; set; }
        public bool CanConnect { get; set; }
        public bool TableAccessible { get; set; }
        public bool WriteOperationsWork { get; set; }
        public long ResponseTimeMs { get; set; }
        public int RecordCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
