using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Users.DTOs;
using Domain.Interfaces.Repositories;
using Domain.Models;
using MediatR;

namespace Application.Features.Users.Commands.CreateUser
{
    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<UserDto>>
    {
        private readonly IUserRepository _userRepository;

        public CreateUserCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Result<UserDto>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var email = new Email(request.Email);

                // Check if user already exists
                var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
                if (existingUser != null)
                {
                    return Result.Failure<UserDto>("User with this email already exists");
                }

                var user = new User(email, request.FirstName, request.LastName);
                var createdUser = await _userRepository.CreateAsync(user, cancellationToken);

                var userDto = new UserDto
                {
                    Id = createdUser.Id,
                    Email = createdUser.Email.Value,
                    FirstName = createdUser.FirstName,
                    LastName = createdUser.LastName,
                    FullName = createdUser.GetFullName(),
                    CreatedAt = createdUser.CreatedAt,
                    UpdatedAt = createdUser.UpdatedAt
                };

                return Result.Success(userDto);
            }
            catch (Exception ex)
            {
                return Result.Failure<UserDto>($"Failed to create user: {ex.Message}");
            }
        }
    }
}
