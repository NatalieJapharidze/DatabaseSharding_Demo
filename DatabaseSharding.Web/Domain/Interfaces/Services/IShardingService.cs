using Domain.Models;

namespace Domain.Interfaces.Services
{
    public interface IShardingService
    {
        Task<string> GetShardForKeyAsync(ShardKey key, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<ShardInfo>> GetAllShardsAsync(CancellationToken cancellationToken = default);
        Task<bool> IsShardHealthyAsync(string shardId, CancellationToken cancellationToken = default);
        Task RebalanceAsync(string newShardConnectionString, CancellationToken cancellationToken = default);
    }
}
