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
                var hash = ComputeSHA256Hash(key.Value);
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
                    var hash = ComputeSHA256Hash(virtualNodeKey);

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
            var mapping = new Dictionary<string, List<string>>();

            lock (_lock)
            {
                // Create a copy of current ring
                var currentRing = new SortedDictionary<uint, string>(_ring);

                // Simulate adding the new shard
                var tempRing = new SortedDictionary<uint, string>(_ring);
                var virtualNodes = 100 * _virtualNodesPerWeight / 100;

                for (int i = 0; i < virtualNodes; i++)
                {
                    var virtualNodeKey = $"{newShardId}:{i}";
                    var hash = ComputeSHA256Hash(virtualNodeKey);
                    tempRing[hash] = newShardId;
                }

                // Sample key space to determine what would move
                var sampleSize = 10000;
                var keysToMove = new Dictionary<string, List<string>>();

                for (int i = 0; i < sampleSize; i++)
                {
                    var sampleKey = $"sample_key_{i}";
                    var currentShard = GetShardFromRing(currentRing, sampleKey);
                    var newShard = GetShardFromRing(tempRing, sampleKey);

                    if (currentShard != newShard && newShard == newShardId)
                    {
                        if (!keysToMove.ContainsKey(currentShard))
                        {
                            keysToMove[currentShard] = new List<string>();
                        }
                        keysToMove[currentShard].Add(sampleKey);
                    }
                }

                // Convert to the expected format
                foreach (var kvp in keysToMove)
                {
                    mapping[kvp.Key] = kvp.Value;
                }
            }

            return mapping;
        }
        private string GetShardFromRing(SortedDictionary<uint, string> ring, string key)
        {
            var hash = ComputeSHA256Hash(key);
            var shard = ring.FirstOrDefault(kvp => kvp.Key >= hash);
            return shard.Key != 0 ? shard.Value : ring.First().Value;
        }

        private void InitializeRing(ShardingOptions options)
        {
            for (int i = 0; i < options.ShardConnectionStrings.Count; i++)
            {
                var shardId = $"shard_{i}";
                AddShard(shardId, 100);
            }
        }

        private uint ComputeSHA256Hash(string input)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            // Take first 4 bytes and convert to uint
            return BitConverter.ToUInt32(hash, 0);
        }
    }
}
