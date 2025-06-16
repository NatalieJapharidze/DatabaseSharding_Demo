using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Infrastructure.Models;

namespace Infrastructure.Interfaces
{
    public interface IShardMetricsService
    {
        Task<ShardMetrics> GetShardMetricsAsync(string shardId, CancellationToken cancellationToken = default);
        Task<List<ShardMetrics>> GetAllShardMetricsAsync(CancellationToken cancellationToken = default);
        Task<SystemMetrics> GetSystemMetricsAsync(CancellationToken cancellationToken = default);
    }
}
