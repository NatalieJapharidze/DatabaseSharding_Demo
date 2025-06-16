using Application.Common.Models;
using Application.Features.Users.DTOs;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers
{
    public record GetUsersQuery(int Page = 1, int PageSize = 10) : IRequest<Result<PaginatedList<UserDto>>>;
}
