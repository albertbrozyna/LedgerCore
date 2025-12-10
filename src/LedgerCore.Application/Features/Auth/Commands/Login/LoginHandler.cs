using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Auth.Commands.Login
{
    public partial class Login
    {
        public class Handler : IRequestHandler<Command, Result<Response>>
        {
            private readonly IAppDbContext _appDbContext;
            private readonly IPasswordHasher _passwordHasher;

            public Handler(IAppDbContext appDbContext, IPasswordHasher passwordHasher)
            {
                _appDbContext = appDbContext;
                _passwordHasher = passwordHasher;
            }

            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                var userOrNull = await _appDbContext.Users.FirstOrDefaultAsync(u => u.Email.Equals(request.Email), cancellationToken);

                var result = Maybe.From(userOrNull)
                    .ToResult("Incorrect email or password")
                    .Ensure(user => user.IsActive,
                    "User account is blocked temporarily")
                    .Ensure(user => _passwordHasher.Verify(request.Password, user.PasswordHash),
                    "Incorrect email or password"
                    ).Map(user => new Response(user.Id));

                if (result.IsFailure)
                {
                    Maybe.From(userOrNull).Execute(user => user.AddFailedLoginAttempt());

                }
                else
                {
                    Maybe.From(userOrNull).Execute(user => user.SetLastLoginNow());
                }
                return result;

            }
        }

    }
}
