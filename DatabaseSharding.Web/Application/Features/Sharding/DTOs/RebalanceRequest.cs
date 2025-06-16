using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Sharding.DTOs
{
    public class RebalanceRequest
    {
        public string NewShardConnectionString { get; set; } = string.Empty;
    }
}
