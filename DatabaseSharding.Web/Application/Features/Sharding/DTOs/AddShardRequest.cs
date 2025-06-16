using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Sharding.DTOs
{
    public class AddShardRequest
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int Weight { get; set; } = 100;
    }
}
