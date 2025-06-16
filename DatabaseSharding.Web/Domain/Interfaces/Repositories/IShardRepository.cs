using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Models;

namespace Domain.Interfaces.Repositories
{
    public interface IShardRepository
    {
        Task<IReadOnlyList<ShardInfo>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<ShardInfo?> GetByIdAsync(string shardId, CancellationToken cancellationToken = default);
        Task UpdateHealthAsync(string shardId, bool isHealthy, CancellationToken cancellationToken = default);
    }
}
