using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces.Authentication;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Features.Auth.Commands.Refresh
{
    public static  class RefreshToken
    {
        public record Command(string RefreshToken) : IRequest<Result<Response>>;
        public record Response(string AccessToken,string RefreshToken);

        public class Handler(ITokenService tokenService) : IRequestHandler<Command,Result<Response>>
        {
            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                (string accessToken,string refreshToken) = await tokenService.RefreshAsync(request.RefreshToken, cancellationToken);

                return Result.Success(new Response(accessToken, refreshToken));
            }
        }

    }
}
