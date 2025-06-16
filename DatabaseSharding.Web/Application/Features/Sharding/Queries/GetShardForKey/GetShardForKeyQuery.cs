using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Common.Models;
using MediatR;

namespace Application.Features.Sharding.Queries.GetShardForKey
{
    public record GetShardForKeyQuery(string Key) : IRequest<Result<string>>;
}
