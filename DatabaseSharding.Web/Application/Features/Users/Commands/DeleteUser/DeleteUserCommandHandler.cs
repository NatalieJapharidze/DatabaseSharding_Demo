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

namespace Application.Features.Users.Commands.DeleteUser
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, Result>
    {
        private readonly IUserRepository _userRepository;

        public DeleteUserCommandHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var deleted = await _userRepository.DeleteAsync(request.Id, cancellationToken);

                if (!deleted)
                {
                    return Result.Failure("User not found");
                }

                return Result.Success();
            }
            catch (Exception ex)
            {
                return Result.Failure<UserDto>($"Failed to Delete user: {ex.Message}");
            }
        }
    }
}
