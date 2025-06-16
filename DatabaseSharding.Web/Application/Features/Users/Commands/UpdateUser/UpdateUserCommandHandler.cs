using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Users.DTOs;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Features.Users.Commands.UpdateUser
{
    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result<UserDto>>
    {
        private readonly IUserRepository _userRepository;

        public UpdateUserCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Result<UserDto>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
                if (user == null)
                {
                    return Result.Failure<UserDto>("User not found");
                }

                user.UpdateName(request.FirstName, request.LastName);

                var updatedUser = await _userRepository.UpdateAsync(user, cancellationToken);

                var userDto = new UserDto
                {
                    Id = updatedUser.Id,
                    Email = updatedUser.Email.Value,
                    FirstName = updatedUser.FirstName,
                    LastName = updatedUser.LastName,
                    FullName = updatedUser.GetFullName(),
                    CreatedAt = updatedUser.CreatedAt,
                    UpdatedAt = updatedUser.UpdatedAt
                };

                return Result.Success(userDto);
            }
            catch (Exception ex)
            {
                return Result.Failure<UserDto>($"Failed to update user: {ex.Message}");
            }
        }
    }
}
