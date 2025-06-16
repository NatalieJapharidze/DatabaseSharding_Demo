using Application.Features.Users.Commands.CreateUser;
using Application.Features.Users.Queries.GetUser;
using Application.Features.Users.Queries.GetUsers;
using Domain;
using Domain.Models;
using Infrastructure.Data;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<UsersController> _logger;

        public UsersController(IMediator mediator, ILogger<UsersController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Creating user with email: {Email}", command.Email);

                var result = await _mediator.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to create user: {Error}", result.Error);
                    return BadRequest(result.Error);
                }

                _logger.LogInformation("Successfully created user {UserId}", result.Value.Id);
                return CreatedAtAction(nameof(GetUser), new { id = result.Value.Id }, result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user with email: {Email}", command.Email);
                return StatusCode(500, "An error occurred while creating the user");
            }
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting user with ID");

                var result = await _mediator.Send(new GetUserQuery(id), cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("User not found");
                    return NotFound(result.Error);
                }

                _logger.LogInformation("Successfully retrieved user {UserId}",id);
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user with ID");
                return StatusCode(500, "An error occurred while retrieving the user");
            }
        }

        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting users");

                var result = await _mediator.Send(new GetUsersQuery(), cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("User not found");
                    return NotFound(result.Error);
                }

                _logger.LogInformation("Successfully retrieved users");
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user with ID");
                return StatusCode(500, "An error occurred while retrieving the user");
            }
        }
    }
}
