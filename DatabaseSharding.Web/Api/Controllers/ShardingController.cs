using Application.Features.Sharding.Queries.GetShardForKey;
using Infrastructure.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        public async Task<IActionResult> GetShardForKey(Guid key, CancellationToken cancellationToken)
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

                var testGuids = new[]
                {
                    new Guid("4ba8f58f-efda-4d89-8b66-0754184e36c5"),
                    new Guid("6ebb9d02-91c4-4f9d-99c9-b34e25f7b069"),
                    new Guid("4c7e9d4c-a5b6-4692-9b32-c3b25492b6b5"),
                    new Guid("b3e7666c-951c-4e79-96e6-940faba53ba0"),
                    new Guid("fefc1328-f442-4bd0-9554-3017fb800dbe")
                };

                var shardDistribution = new Dictionary<string, List<Guid>>();

                foreach (var id in testGuids)
                {
                    var result = await _mediator.Send(new GetShardForKeyQuery(id), cancellationToken);
                    if (result.IsSuccess)
                    {
                        if (!shardDistribution.ContainsKey(result.Value))
                        {
                            shardDistribution[result.Value] = new List<Guid>();
                        }
                        shardDistribution[result.Value].Add(id);
                    }
                }

                _logger.LogInformation("Sharding test completed");
                return Ok(new
                {
                    Message = "Sharding test completed",
                    Distribution = shardDistribution,
                    TotalEmails = testGuids.Length,
                    ShardsUsed = shardDistribution.Keys.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing sharding");
                return StatusCode(500, "An error occurred while testing sharding");
            }
        }

        [HttpGet("diagnose")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> DiagnoseSharding(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Running sharding diagnostics");

                var diagnostics = new
                {
                    Timestamp = DateTime.UtcNow,
                    DatabaseConnections = new List<object>(),
                    ShardDistribution = new Dictionary<string, List<string>>(),
                    TestQueries = new List<object>()
                };

                // Test database connections
                using var scope = HttpContext.RequestServices.CreateScope();
                var shardConnectionService = scope.ServiceProvider.GetRequiredService<IShardConnectionService>();

                try
                {
                    var contexts = await shardConnectionService.GetAllContextsAsync(cancellationToken);
                    _logger.LogInformation("Successfully retrieved {Count} database connections", contexts.Count);

                    foreach (var context in contexts)
                    {
                        try
                        {
                            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
                            var userCount = await context.Users.CountAsync(cancellationToken);

                            diagnostics.DatabaseConnections.Add(new
                            {
                                ConnectionString = context.Database.GetConnectionString()?.Substring(0, 50) + "...",
                                CanConnect = canConnect,
                                UserCount = userCount,
                                Status = "Healthy"
                            });

                            await context.DisposeAsync();
                        }
                        catch (Exception ex)
                        {
                            diagnostics.DatabaseConnections.Add(new
                            {
                                ConnectionString = "Error retrieving connection",
                                CanConnect = false,
                                UserCount = -1,
                                Status = "Error",
                                Error = ex.Message
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to retrieve database connections");
                    diagnostics.DatabaseConnections.Add(new
                    {
                        Error = "Failed to retrieve database connections: " + ex.Message
                    });
                }

                _logger.LogInformation("Sharding diagnostics completed");
                return Ok(diagnostics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running sharding diagnostics");
                return StatusCode(500, "An error occurred while running diagnostics");
            }
        }

        [HttpPost("force-initialize")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ForceInitialize(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Force initializing databases");

                using var scope = HttpContext.RequestServices.CreateScope();
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();

                await dbInitializer.InitializeAllDatabasesAsync(cancellationToken);

                _logger.LogInformation("Force initialization completed");
                return Ok(new { Message = "Force initialization completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Force initialization failed");
                return StatusCode(500, new { Message = "Force initialization failed", Error = ex.Message });
            }
        }
    }
}
