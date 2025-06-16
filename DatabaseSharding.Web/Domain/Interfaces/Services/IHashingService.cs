using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Models;

namespace Domain.Interfaces.Services
{
    public interface IHashingService
    {
        string GetShard(ShardKey key);
        void AddShard(string shardId, int weight = 100);
        void RemoveShard(string shardId);
        IReadOnlyList<string> GetAllShards();
        void UpdateShardWeight(string shardId, int weight);
        Dictionary<string, List<string>> GetRebalanceMapping(string newShardId);
    }
}
