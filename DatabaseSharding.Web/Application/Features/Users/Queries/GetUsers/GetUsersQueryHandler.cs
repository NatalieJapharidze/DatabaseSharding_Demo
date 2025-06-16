using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Users.DTOs;
using Application.Features.Users.Queries.GetUser;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers
{
    public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, Result<PaginatedList<UserDto>>>
    {
        private readonly IUserRepository _userRepository;

        public GetUsersQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Result<PaginatedList<UserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var skip = (request.Page - 1) * request.PageSize;
                var users = await _userRepository.GetAllAsync(skip, request.PageSize, cancellationToken);
                var totalCount = await _userRepository.GetCountAsync(cancellationToken);

                List<UserDto> userDtos = users.Select(user => new UserDto
                {
                    Id = user.Id,
                    Email = user.Email.Value,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    FullName = user.GetFullName(),
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    ShardKey = user.GetShardKey()
                }).ToList();

                var paginatedResult = new PaginatedList<UserDto>(userDtos, totalCount, request.Page, request.PageSize);

                return Result.Success(paginatedResult);
            }
            catch (Exception ex)
            {

                return Result.Failure<PaginatedList<UserDto>>($"Failed to get users: {ex.Message}");
            }
            
        }
    }
}
