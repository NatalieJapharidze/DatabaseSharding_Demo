namespace Infrastructure.Interfaces
{
    public interface IShardRebalancingService
    {
        Task<bool> RebalanceToNewShardAsync(string newShardConnectionString);
    }
}
