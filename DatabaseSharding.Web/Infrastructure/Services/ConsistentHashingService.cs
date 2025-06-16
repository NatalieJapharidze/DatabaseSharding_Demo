using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Services;
using Domain.Models;
using Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ConsistentHashingService : IHashingService
    {
        private readonly SortedDictionary<uint, string> _ring = new();
        private readonly Dictionary<string, int> _shardWeights = new();
        private readonly Dictionary<string, List<uint>> _shardVirtualNodes = new();
        private readonly int _virtualNodesPerWeight;
        private readonly object _lock = new();

        public ConsistentHashingService(IOptions<ShardingOptions> options)
        {
            _virtualNodesPerWeight = options.Value.VirtualNodesPerShard;
            InitializeRing(options.Value);
        }

        public string GetShard(ShardKey key)
        {
            if (!_ring.Any())
                throw new InvalidOperationException("No shards available in the ring");

            lock (_lock)
            {
                var hash = ComputeHash(key.Value);
                var shard = _ring.FirstOrDefault(kvp => kvp.Key >= hash);

                return shard.Key != 0 ? shard.Value : _ring.First().Value;
            }
        }

        public void AddShard(string shardId, int weight = 100)
        {
            lock (_lock)
            {
                if (_shardWeights.ContainsKey(shardId))
                    return;

                _shardWeights[shardId] = weight;
                _shardVirtualNodes[shardId] = new List<uint>();

                var virtualNodes = weight * _virtualNodesPerWeight / 100;
                for (int i = 0; i < virtualNodes; i++)
                {
                    var virtualNodeKey = $"{shardId}:{i}";
                    var hash = ComputeHash(virtualNodeKey);

                    _ring[hash] = shardId;
                    _shardVirtualNodes[shardId].Add(hash);
                }
            }
        }

        public void RemoveShard(string shardId)
        {
            lock (_lock)
            {
                if (!_shardWeights.ContainsKey(shardId))
                    return;

                foreach (var virtualNode in _shardVirtualNodes[shardId])
                {
                    _ring.Remove(virtualNode);
                }

                _shardWeights.Remove(shardId);
                _shardVirtualNodes.Remove(shardId);
            }
        }

        public void UpdateShardWeight(string shardId, int weight)
        {
            lock (_lock)
            {
                if (!_shardWeights.ContainsKey(shardId))
                    return;

                RemoveShard(shardId);
                AddShard(shardId, weight);
            }
        }

        public IReadOnlyList<string> GetAllShards()
        {
            lock (_lock)
            {
                return _shardWeights.Keys.ToList();
            }
        }

        public Dictionary<string, List<string>> GetRebalanceMapping(string newShardId)
        {
            // Implementation for rebalancing logic
            return new Dictionary<string, List<string>>();
        }

        private void InitializeRing(ShardingOptions options)
        {
            for (int i = 0; i < options.ShardConnectionStrings.Count; i++)
            {
                var shardId = $"shard_{i}";
                AddShard(shardId, 100);
            }
        }

        private uint ComputeHash(string input)
        {
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
