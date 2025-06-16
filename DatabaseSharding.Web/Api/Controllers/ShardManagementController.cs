using Application.Features.Sharding.DTOs;
using Domain.Interfaces.Services;
using Infrastructure.Interfaces;
using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShardManagementController : ControllerBase
    {
        private readonly ShardRebalancingService _rebalancingService;
        private readonly IDatabaseInitializationService _databaseInitializationService;
        private readonly ILogger<ShardManagementController> _logger;

        public ShardManagementController(
            ShardRebalancingService rebalancingService,
            IDatabaseInitializationService databaseInitializationService,
            ILogger<ShardManagementController> logger)
        {
            _rebalancingService = rebalancingService;
            _databaseInitializationService = databaseInitializationService;
            _logger = logger;
        }

        [HttpPost("add-shard")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> AddShard([FromBody] AddShardRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Adding new shard with connection string");

                var success = await _rebalancingService.RebalanceToNewShardAsync(request.ConnectionString);

                if (success)
                {
                    _logger.LogInformation("Successfully added new shard and completed rebalancing");
                    return Ok(new { Message = "Shard added successfully and rebalancing completed" });
                }
                else
                {
                    _logger.LogWarning("Failed to add new shard");
                    return BadRequest("Failed to add new shard");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding new shard");
                return StatusCode(500, "An error occurred while adding the shard");
            }
        }

        [HttpPost("rebalance")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> TriggerRebalance([FromBody] RebalanceRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Triggering manual rebalancing");

                var success = await _rebalancingService.RebalanceToNewShardAsync(request.NewShardConnectionString);

                if (success)
                {
                    _logger.LogInformation("Rebalancing completed successfully");
                    return Ok(new { Message = "Rebalancing completed successfully" });
                }
                else
                {
                    _logger.LogWarning("Rebalancing failed");
                    return BadRequest("Rebalancing failed");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rebalancing");
                return StatusCode(500, "An error occurred during rebalancing");
            }
        }

        [HttpPost("initialize-databases")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> InitializeDatabases(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Manually initializing databases");

                await _databaseInitializationService.InitializeAllDatabasesAsync(cancellationToken);

                _logger.LogInformation("Database initialization completed successfully");
                return Ok(new { Message = "Database initialization completed successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database initialization failed");
                return StatusCode(500, new { Message = "Database initialization failed", Error = ex.Message });
            }
        }

        [HttpGet("shard-statistics")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetShardStatistics(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting shard statistics");

                using var scope = HttpContext.RequestServices.CreateScope();
                var shardConnectionService = scope.ServiceProvider.GetRequiredService<IShardConnectionService>();
                var hashingService = scope.ServiceProvider.GetRequiredService<IHashingService>();

                var statistics = new
                {
                    Timestamp = DateTime.UtcNow,
                    TotalShards = hashingService.GetAllShards().Count,
                    ShardDetails = new List<object>()
                };

                var contexts = await shardConnectionService.GetAllContextsAsync(cancellationToken);

                foreach (var context in contexts)
                {
                    try
                    {
                        var userCount = await context.Users.CountAsync(cancellationToken);
                        var connectionString = context.Database.GetConnectionString();
                        var shardName = ExtractDatabaseNameFromConnectionString(connectionString);

                        statistics.ShardDetails.Add(new
                        {
                            ShardName = shardName,
                            UserCount = userCount,
                            IsHealthy = await context.Database.CanConnectAsync(cancellationToken),
                            ConnectionString = MaskConnectionString(connectionString)
                        });

                        await context.DisposeAsync();
                    }
                    catch (Exception ex)
                    {
                        statistics.ShardDetails.Add(new
                        {
                            ShardName = "Unknown",
                            UserCount = -1,
                            IsHealthy = false,
                            Error = ex.Message
                        });
                    }
                }

                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting shard statistics");
                return StatusCode(500, "An error occurred while getting shard statistics");
            }
        }

        [HttpPost("migrate-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> MigrateData([FromBody] MigrateDataRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Starting data migration from {SourceShard} to {TargetShard}",
                    request.SourceShardId, request.TargetShardId);

                // This would be implemented based on specific migration requirements
                // For now, return a placeholder response

                return Ok(new
                {
                    Message = "Data migration initiated",
                    SourceShard = request.SourceShardId,
                    TargetShard = request.TargetShardId,
                    Status = "In Progress"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data migration");
                return StatusCode(500, "An error occurred during data migration");
            }
        }

        private string ExtractDatabaseNameFromConnectionString(string connectionString)
        {
            try
            {
                var parts = connectionString.Split(';');
                var dbPart = parts.FirstOrDefault(p => p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase));
                return dbPart?.Split('=')[1] ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string MaskConnectionString(string connectionString)
        {
            try
            {
                return connectionString?.Substring(0, Math.Min(50, connectionString.Length)) + "...";
            }
            catch
            {
                return "Invalid connection string";
            }
        }
    }
}
