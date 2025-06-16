using Application.Features.Users.Commands.CreateUser;
using Application.Features.Users.Commands.DeleteUser;
using Application.Features.Users.Queries.GetUsers;
using Infrastructure.Interfaces;
using Infrastructure.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestingController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IShardMetricsService _metricsService;
        private readonly ILogger<TestingController> _logger;

        public TestingController(
            IMediator mediator,
            IShardMetricsService metricsService,
            ILogger<TestingController> logger)
        {
            _mediator = mediator;
            _metricsService = metricsService;
            _logger = logger;
        }

        [HttpPost("generate-test-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GenerateTestData(
            [FromQuery] int userCount = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Generating {UserCount} test users", userCount);

                var createdUsers = new List<object>();
                var domains = new[] { "example.com", "test.com", "demo.com", "sample.com" };
                var firstNames = new[] { "John", "Jane", "Bob", "Alice", "Charlie", "Diana", "Eve", "Frank" };
                var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };

                var random = new Random();

                for (int i = 0; i < userCount; i++)
                {
                    var firstName = firstNames[random.Next(firstNames.Length)];
                    var lastName = lastNames[random.Next(lastNames.Length)];
                    var domain = domains[random.Next(domains.Length)];
                    var email = $"{firstName.ToLower()}.{lastName.ToLower()}.{i}@{domain}";

                    var command = new CreateUserCommand(email, firstName, lastName);
                    var result = await _mediator.Send(command, cancellationToken);

                    if (result.IsSuccess)
                    {
                        createdUsers.Add(new
                        {
                            Id = result.Value.Id,
                            Email = result.Value.Email,
                            FullName = result.Value.FullName
                        });
                    }

                    // Add small delay to avoid overwhelming the system
                    if (i % 10 == 0)
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }

                _logger.LogInformation("Successfully created {Count} test users", createdUsers.Count);

                return Ok(new
                {
                    Message = $"Generated {createdUsers.Count} test users",
                    RequestedCount = userCount,
                    ActualCount = createdUsers.Count,
                    SampleUsers = createdUsers.Take(5).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating test data");
                return StatusCode(500, "An error occurred while generating test data");
            }
        }

        [HttpGet("performance-test")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> PerformanceTest(
            [FromQuery] int iterations = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Running performance test with {Iterations} iterations", iterations);

                var results = new
                {
                    StartTime = DateTime.UtcNow,
                    TotalIterations = iterations,
                    Operations = new List<object>(),
                    Summary = new
                    {
                        TotalTime = 0.0,
                        AverageTime = 0.0,
                        MinTime = double.MaxValue,
                        MaxTime = 0.0
                    }
                };

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var operationTimes = new List<double>();

                for (int i = 0; i < iterations; i++)
                {
                    var operationStopwatch = System.Diagnostics.Stopwatch.StartNew();

                    // Test user creation
                    var email = $"perf.test.{i}.{Guid.NewGuid():N}@performance.test";
                    var command = new CreateUserCommand(email, "Performance", $"Test{i}");
                    var result = await _mediator.Send(command, cancellationToken);

                    operationStopwatch.Stop();
                    var operationTime = operationStopwatch.ElapsedMilliseconds;
                    operationTimes.Add(operationTime);

                    results.Operations.Add(new
                    {
                        Iteration = i + 1,
                        Operation = "CreateUser",
                        Success = result.IsSuccess,
                        TimeMs = operationTime,
                        UserId = result.IsSuccess ? result.Value.Id.ToString() : null,
                        Email = email
                    });

                    // Small delay between operations
                    await Task.Delay(50, cancellationToken);
                }

                stopwatch.Stop();

                // Calculate summary statistics
                var totalTime = stopwatch.ElapsedMilliseconds;
                var averageTime = operationTimes.Average();
                var minTime = operationTimes.Min();
                var maxTime = operationTimes.Max();

                var summary = new
                {
                    TotalTime = totalTime,
                    AverageTime = Math.Round(averageTime, 2),
                    MinTime = minTime,
                    MaxTime = maxTime,
                    OperationsPerSecond = Math.Round(iterations / (totalTime / 1000.0), 2),
                    SuccessfulOperations = results.Operations.Count(o => ((dynamic)o).Success),
                    EndTime = DateTime.UtcNow
                };

                _logger.LogInformation("Performance test completed: {OpsPerSec} ops/sec, {AvgTime}ms avg",
                    summary.OperationsPerSecond, summary.AverageTime);

                return Ok(new
                {
                    results.StartTime,
                    results.TotalIterations,
                    Summary = summary,
                    Operations = results.Operations.Take(10).ToList(), // Return first 10 operations
                    Message = "Performance test completed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during performance test");
                return StatusCode(500, "An error occurred during performance test");
            }
        }

        [HttpGet("distribution-analysis")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> AnalyzeDistribution(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Analyzing data distribution");

                var systemMetrics = await _metricsService.GetSystemMetricsAsync(cancellationToken);
                var shardMetrics = await _metricsService.GetAllShardMetricsAsync(cancellationToken);

                // Calculate distribution quality
                var userCounts = shardMetrics.Select(s => s.UserCount).ToList();
                var average = userCounts.Average();
                var variance = userCounts.Sum(count => Math.Pow(count - average, 2)) / userCounts.Count;
                var standardDeviation = Math.Sqrt(variance);
                var coefficientOfVariation = average > 0 ? standardDeviation / average : 0;

                // Distribution quality assessment
                string distributionQuality;
                if (coefficientOfVariation <= 0.1)
                    distributionQuality = "Excellent";
                else if (coefficientOfVariation <= 0.2)
                    distributionQuality = "Good";
                else if (coefficientOfVariation <= 0.3)
                    distributionQuality = "Fair";
                else
                    distributionQuality = "Poor";

                var analysis = new
                {
                    Timestamp = DateTime.UtcNow,
                    TotalUsers = systemMetrics.TotalUsers,
                    TotalShards = systemMetrics.TotalShards,
                    AverageUsersPerShard = Math.Round(average, 2),
                    StandardDeviation = Math.Round(standardDeviation, 2),
                    CoefficientOfVariation = Math.Round(coefficientOfVariation, 4),
                    DistributionQuality = distributionQuality,
                    ShardDetails = shardMetrics.Select(s => new
                    {
                        s.ShardId,
                        s.UserCount,
                        Percentage = systemMetrics.TotalUsers > 0 ?
                            Math.Round((double)s.UserCount / systemMetrics.TotalUsers * 100, 2) : 0,
                        DeviationFromAverage = Math.Round(s.UserCount - average, 2),
                        s.IsHealthy
                    }).OrderByDescending(s => s.UserCount).ToList(),
                    Recommendations = GenerateRecommendations(coefficientOfVariation, shardMetrics)
                };

                return Ok(analysis);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing distribution");
                return StatusCode(500, "An error occurred while analyzing distribution");
            }
        }

        private List<string> GenerateRecommendations(double coefficientOfVariation, List<ShardMetrics> shardMetrics)
        {
            var recommendations = new List<string>();

            if (coefficientOfVariation > 0.3)
            {
                recommendations.Add("Consider rebalancing data across shards - distribution is uneven");
            }

            var unhealthyShards = shardMetrics.Where(s => !s.IsHealthy).ToList();
            if (unhealthyShards.Any())
            {
                recommendations.Add($"Address {unhealthyShards.Count} unhealthy shard(s): {string.Join(", ", unhealthyShards.Select(s => s.ShardId))}");
            }

            var maxUsers = shardMetrics.Max(s => s.UserCount);
            var minUsers = shardMetrics.Min(s => s.UserCount);
            if (maxUsers > minUsers * 2)
            {
                recommendations.Add("Large variation in shard sizes detected - consider adding more virtual nodes");
            }

            var totalUsers = shardMetrics.Sum(s => s.UserCount);
            var averagePerShard = totalUsers / (double)shardMetrics.Count;
            if (averagePerShard > 10000)
            {
                recommendations.Add("Consider adding more shards as user count grows");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Distribution looks good - no immediate action required");
            }

            return recommendations;
        }

        [HttpDelete("cleanup-test-data")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CleanupTestData(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Cleaning up test data");

                // Get all users with test domains
                var testDomains = new[] { "test.com", "performance.test", "example.com", "demo.com", "sample.com" };
                var usersQuery = new GetUsersQuery(1, 1000); // Get first 1000 users
                var usersResult = await _mediator.Send(usersQuery, cancellationToken);

                if (usersResult.IsFailure)
                {
                    return BadRequest("Failed to retrieve users for cleanup");
                }

                var testUsers = usersResult.Value.Items
                    .Where(u => testDomains.Any(domain => u.Email.EndsWith(domain)))
                    .ToList();

                var deletedCount = 0;
                foreach (var user in testUsers)
                {
                    try
                    {
                        var deleteCommand = new DeleteUserCommand(user.Id);
                        var deleteResult = await _mediator.Send(deleteCommand, cancellationToken);

                        if (deleteResult.IsSuccess)
                        {
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete test user {UserId}", user.Id);
                    }
                }

                _logger.LogInformation("Cleaned up {DeletedCount} test users", deletedCount);

                return Ok(new
                {
                    Message = "Test data cleanup completed",
                    UsersFound = testUsers.Count,
                    UsersDeleted = deletedCount,
                    TestDomains = testDomains
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during test data cleanup");
                return StatusCode(500, "An error occurred during cleanup");
            }
        }
    }
}
