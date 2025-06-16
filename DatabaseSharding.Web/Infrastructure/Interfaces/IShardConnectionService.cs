using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Models;
using Infrastructure.Data.Contexts;

namespace Infrastructure.Interfaces
{
    public interface IShardConnectionService
    {
        Task<ShardDbContext> GetContextAsync(ShardKey shardKey, CancellationToken cancellationToken = default);
        Task<ShardDbContext> GetContextForShardAsync(string shardId, CancellationToken cancellationToken = default);
        Task<List<ShardDbContext>> GetAllContextsAsync(CancellationToken cancellationToken = default);
        Task<bool> TestShardConnectivityAsync(string shardId, CancellationToken cancellationToken = default);
    }
}
