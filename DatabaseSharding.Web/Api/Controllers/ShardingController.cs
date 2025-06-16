using Application.Features.Sharding.Queries.GetShardForKey;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShardingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ShardingController> _logger;

        public ShardingController(IMediator mediator, ILogger<ShardingController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("shard-for-key/{key}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GetShardForKey(string key, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting shard for key: {Key}", key);

                var result = await _mediator.Send(new GetShardForKeyQuery(key), cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to get shard for key {Key}: {Error}", key, result.Error);
                    return BadRequest(result.Error);
                }

                _logger.LogInformation("Key {Key} maps to shard {ShardId}", key, result.Value);
                return Ok(new { Key = key, Shard = result.Value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shard for key: {Key}", key);
                return StatusCode(500, "An error occurred while determining the shard");
            }
        }

        [HttpGet("test-sharding")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> TestSharding(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Testing sharding distribution");

                var testEmails = new[]
                {
                "alice@example.com",
                "bob@example.com",
                "charlie@example.com",
                "diana@example.com",
                "eve@example.com",
                "frank@example.com",
                "grace@example.com",
                "henry@example.com"
            };

                var shardDistribution = new Dictionary<string, List<string>>();

                foreach (var email in testEmails)
                {
                    var result = await _mediator.Send(new GetShardForKeyQuery(email), cancellationToken);
                    if (result.IsSuccess)
                    {
                        if (!shardDistribution.ContainsKey(result.Value))
                        {
                            shardDistribution[result.Value] = new List<string>();
                        }
                        shardDistribution[result.Value].Add(email);
                    }
                }

                _logger.LogInformation("Sharding test completed");
                return Ok(new
                {
                    Message = "Sharding test completed",
                    Distribution = shardDistribution,
                    TotalEmails = testEmails.Length,
                    ShardsUsed = shardDistribution.Keys.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing sharding");
                return StatusCode(500, "An error occurred while testing sharding");
            }
        }
    }
}
