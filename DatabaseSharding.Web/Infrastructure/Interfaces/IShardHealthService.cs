using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Models;

namespace Infrastructure.Interfaces
{
    public interface IShardHealthService
    {
        Task<List<ShardHealthStatus>> CheckAllShardsHealthAsync(CancellationToken cancellationToken = default);
        Task<ShardHealthStatus> CheckShardHealthAsync(string shardId, CancellationToken cancellationToken = default);
        Task<bool> IsSystemHealthyAsync(CancellationToken cancellationToken = default);
    }
}
