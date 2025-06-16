using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Services;
using Domain.Models;
using Infrastructure.Configuration;
using Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class ShardConnectionService : IShardConnectionService
    {
        private readonly IHashingService _hashingService;
        private readonly ShardingOptions _options;
        private readonly Dictionary<string, string> _shardConnections;

        public ShardConnectionService(
            IHashingService hashingService,
            IOptions<ShardingOptions> options)
        {
            _hashingService = hashingService;
            _options = options.Value;
            _shardConnections = new Dictionary<string, string>();

            InitializeShardConnections();
        }

        public async Task<ShardDbContext> GetContextAsync(ShardKey shardKey, CancellationToken cancellationToken = default)
        {
            var shardId = _hashingService.GetShard(shardKey);
            return await GetContextForShardAsync(shardId, cancellationToken);
        }

        public async Task<ShardDbContext> GetContextForShardAsync(string shardId, CancellationToken cancellationToken = default)
        {
            if (!_shardConnections.TryGetValue(shardId, out var connectionString))
            {
                throw new InvalidOperationException($"Shard {shardId} not found");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ShardDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            var context = new ShardDbContext(optionsBuilder.Options);

            // Ensure database exists
            await context.Database.EnsureCreatedAsync(cancellationToken);

            return context;
        }

        public async Task<List<ShardDbContext>> GetAllContextsAsync(CancellationToken cancellationToken = default)
        {
            var contexts = new List<ShardDbContext>();

            foreach (var (shardId, connectionString) in _shardConnections)
            {
                try
                {
                    var optionsBuilder = new DbContextOptionsBuilder<ShardDbContext>();
                    optionsBuilder.UseNpgsql(connectionString);

                    var context = new ShardDbContext(optionsBuilder.Options);

                    // Test connectivity
                    await context.Database.CanConnectAsync(cancellationToken);
                    contexts.Add(context);
                }
                catch
                {
                    // Skip unhealthy shards
                }
            }

            return contexts;
        }

        private void InitializeShardConnections()
        {
            for (int i = 0; i < _options.ShardConnectionStrings.Count; i++)
            {
                var shardId = $"shard_{i}";
                _shardConnections[shardId] = _options.ShardConnectionStrings[i];
            }
        }
    }
}
