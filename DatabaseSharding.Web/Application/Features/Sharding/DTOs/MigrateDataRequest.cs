using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Sharding.DTOs
{
    public class MigrateDataRequest
    {
        public string SourceShardId { get; set; } = string.Empty;
        public string TargetShardId { get; set; } = string.Empty;
        public List<string> EmailPatterns { get; set; } = new();
    }
}
