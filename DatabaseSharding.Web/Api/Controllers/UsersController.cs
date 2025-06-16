using Application.Features.Users.Commands.CreateUser;
using Application.Features.Users.Commands.DeleteUser;
using Application.Features.Users.Commands.UpdateUser;
using Application.Features.Users.DTOs;
using Application.Features.Users.Queries.GetUser;
using Application.Features.Users.Queries.GetUserByEmail;
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

        [HttpGet("by-email/{email}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserByEmail(string email, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting user with email: {Email}", email);

                var result = await _mediator.Send(new GetUserByEmailQuery(email), cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("User not found with email: {Email}", email);
                    return NotFound(result.Error);
                }

                _logger.LogInformation("Successfully retrieved user with email {Email}", email);
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user with email: {Email}", email);
                return StatusCode(500, "An error occurred while retrieving the user");
            }
        }

        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting users list - Page: {Page}, PageSize: {PageSize}", page, pageSize);

                var result = await _mediator.Send(new GetUsersQuery(page, pageSize), cancellationToken);

                if (result.IsFailure)
                {
                    _logger.LogWarning("Failed to get users: {Error}", result.Error);
                    return BadRequest(result.Error);
                }

                _logger.LogInformation("Successfully retrieved {Count} users", result.Value.Items.Count);
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users list");
                return StatusCode(500, "An error occurred while retrieving users");
            }
        }

        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Updating user {UserId}", id);

                var command = new UpdateUserCommand(id, request.FirstName, request.LastName);
                var result = await _mediator.Send(command, cancellationToken);

                if (result.IsFailure)
                {
                    if (result.Error.Contains("not found"))
                    {
                        _logger.LogWarning("User not found for update: {UserId}", id);
                        return NotFound(result.Error);
                    }

                    _logger.LogWarning("Failed to update user {UserId}: {Error}", id, result.Error);
                    return BadRequest(result.Error);
                }

                _logger.LogInformation("Successfully updated user {UserId}", id);
                return Ok(result.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", id);
                return StatusCode(500, "An error occurred while updating the user");
            }
        }

        [HttpDelete("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Deleting user {UserId}", id);

                var result = await _mediator.Send(new DeleteUserCommand(id), cancellationToken);

                if (result.IsFailure)
                {
                    if (result.Error.Contains("not found"))
                    {
                        _logger.LogWarning("User not found for deletion: {UserId}", id);
                        return NotFound(result.Error);
                    }

                    _logger.LogWarning("Failed to delete user {UserId}: {Error}", id, result.Error);
                    return BadRequest(result.Error);
                }

                _logger.LogInformation("Successfully deleted user {UserId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", id);
                return StatusCode(500, "An error occurred while deleting the user");
            }
        }
    }
}
