using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Auth.Commands.Login
{
    public static class Login
    {
        public record Command(string Email, string Password) : IRequest<Result<Response>>;
        public record Response(Guid UserId,string AccessToken,string RefreshToken);
        public class Handler(ILoginUserService loginService,ITokenService tokenService) : IRequestHandler<Command, Result<Response>>
        {
            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                var result = await loginService.LoginUser(request.Email, request.Password);

                if (result.IsFailure) {

                    return Result.Failure<Response>(result.Error);
                }
                var user = result.Value;
                var userRoles = await loginService.GetUserRoles(user);


                var accessToken = tokenService.GenerateAccessToken(user, userRoles);
                var refreshToken = await tokenService.GenerateRefreshTokenAsync(user.Id, cancellationToken);


                return Result.Success(new Response(user.Id, accessToken,refreshToken));
            }

        }

    }
}
