using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.Interfaces.Repositories;
using Domain.Models;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly IShardConnectionService _shardConnectionService;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(
            IShardConnectionService shardConnectionService,
            ILogger<UserRepository> logger)
        {
            _shardConnectionService = shardConnectionService;
            _logger = logger;
        }

        public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
        {
            using var context = await _shardConnectionService.GetContextAsync(user.GetShardKey(), cancellationToken);

            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created user {UserId} with email {Email}", user.Id, user.Email);
            return user;
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var contexts = await _shardConnectionService.GetAllContextsAsync(cancellationToken);

            foreach (var context in contexts)
            {
                try
                {
                    var user = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
                    if (user != null)
                    {
                        return user;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching for user {UserId} in shard", id);
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }

            return null;
        }

        public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default)
        {
            var shardKey = new ShardKey(email.Value);
            using var context = await _shardConnectionService.GetContextAsync(shardKey, cancellationToken);

            return await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            using var context = await _shardConnectionService.GetContextAsync(user.GetShardKey(), cancellationToken);

            context.Users.Update(user);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Updated user {UserId}", user.Id);
            return user;
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(id, cancellationToken);
            if (user == null)
                return false;

            using var context = await _shardConnectionService.GetContextAsync(user.GetShardKey(), cancellationToken);

            context.Users.Remove(user);
            var deletedCount = await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted user {UserId}", id);
            return deletedCount > 0;
        }

        public async Task<IReadOnlyList<User>> GetAllAsync(int skip, int take, CancellationToken cancellationToken = default)
        {
            var contexts = await _shardConnectionService.GetAllContextsAsync(cancellationToken);
            var allUsers = new List<User>();

            foreach (var context in contexts)
            {
                try
                {
                    var users = await context.Users
                        .OrderBy(u => u.CreatedAt)
                        .ToListAsync(cancellationToken);
                    allUsers.AddRange(users);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting users from shard");
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }

            return allUsers
                .OrderBy(u => u.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
        {
            var contexts = await _shardConnectionService.GetAllContextsAsync(cancellationToken);
            var totalCount = 0;

            foreach (var context in contexts)
            {
                try
                {
                    var count = await context.Users.CountAsync(cancellationToken);
                    totalCount += count;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting count from shard");
                }
                finally
                {
                    await context.DisposeAsync();
                }
            }

            return totalCount;
        }
    }
}
