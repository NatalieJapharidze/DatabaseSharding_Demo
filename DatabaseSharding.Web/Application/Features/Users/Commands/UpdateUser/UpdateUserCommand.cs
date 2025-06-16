using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Users.DTOs;
using MediatR;

namespace Application.Features.Users.Commands.UpdateUser
{
    public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName) : IRequest<Result<UserDto>>;
}
