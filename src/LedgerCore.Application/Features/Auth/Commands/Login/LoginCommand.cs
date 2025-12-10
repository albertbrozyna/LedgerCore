using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using CSharpFunctionalExtensions;

namespace LedgerCore.Application.Features.Auth.Commands.Login
{
    public partial class Login
    {
        public record Command(String Email,String Password) : IRequest<Result<Response>>;
        public record Response(Guid UserId);
    }
}
