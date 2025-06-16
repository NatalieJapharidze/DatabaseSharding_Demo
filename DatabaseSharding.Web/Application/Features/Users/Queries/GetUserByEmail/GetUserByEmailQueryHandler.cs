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

namespace Application.Features.Users.Queries.GetUserByEmail
{
    public class GetUserByEmailQueryHandler : IRequestHandler<GetUserByEmailQuery, Result<UserDto>>
    {
        private readonly IUserRepository _userRepository;

        public GetUserByEmailQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Result<UserDto>> Handle(GetUserByEmailQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var email = new Email(request.Email);
                var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

                if (user == null)
                {
                    return Result.Failure<UserDto>("User not found");
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email.Value,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.GetFullName(),
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt
                };

                return Result.Success(userDto);
            }
            catch (Exception ex)
            {
                return Result.Failure<UserDto>($"Failed to get user by email: {ex.Message}");
            }
        }
    }
}
