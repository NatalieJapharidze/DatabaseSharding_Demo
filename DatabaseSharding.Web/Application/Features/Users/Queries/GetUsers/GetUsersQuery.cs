using Application.Common.Models;
using Application.Features.Users.DTOs;
using MediatR;

namespace Application.Features.Users.Queries.GetUsers
{
    public record GetUsersQuery() : IRequest<Result<List<UserDto>>>;
}
