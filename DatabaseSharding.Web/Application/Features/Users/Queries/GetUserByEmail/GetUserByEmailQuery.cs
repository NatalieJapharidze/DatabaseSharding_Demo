using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Common.Models;
using Application.Features.Users.DTOs;
using MediatR;

namespace Application.Features.Users.Queries.GetUserByEmail
{
    public record GetUserByEmailQuery(string Email) : IRequest<Result<UserDto>>;
}
