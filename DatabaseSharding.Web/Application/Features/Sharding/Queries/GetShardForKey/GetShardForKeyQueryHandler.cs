using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Common.Models;
using Domain.Interfaces.Services;
using Domain.Models;
using MediatR;

namespace Application.Features.Sharding.Queries.GetShardForKey
{
    public class GetShardForKeyQueryHandler : IRequestHandler<GetShardForKeyQuery, Result<string>>
    {
        private readonly IHashingService _hashingService;

        public GetShardForKeyQueryHandler(IHashingService hashingService)
        {
            _hashingService = hashingService;
        }

        public async Task<Result<string>> Handle(GetShardForKeyQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var shardKey = new ShardKey(request.Key.ToString());
                var shard = _hashingService.GetShard(shardKey);
                return Result.Success(shard);
            }
            catch (Exception ex)
            {
                return Result.Failure<string>($"Failed to get shard for key: {ex.Message}");
            }
        }
    }
}
