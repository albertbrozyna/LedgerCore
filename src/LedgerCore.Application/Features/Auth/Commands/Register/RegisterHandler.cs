using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Features.Auth.Commands.Register
{
    public partial class Register
    {
        public class Handler : IRequestHandler<Command, Result<Response>>
        {
            private readonly IPasswordHasher _passwordHasher;
            private readonly IAppDbContext _appDbContext;

            public Handler(IAppDbContext appDbContext, IPasswordHasher passwordHasher)
            {
                _passwordHasher = passwordHasher;
                _appDbContext = appDbContext;
            }

            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                return await EnsureUserDoesNotExist(request.Email,cancellationToken)
                    .Bind(() => CreateUser(request))
                    .Bind(user => SaveUser(user,cancellationToken))
                    .Map(user => new Response(user.Id));
            }


            // -----------------------------------------------------
            //      PIPELINE STEPS
            // -----------------------------------------------------

            private async Task<Result> EnsureUserDoesNotExist(string email,CancellationToken cancellationToken)
            {
                var exists = await _appDbContext.Users.AnyAsync(u => u.Email == email,cancellationToken);

                return exists
                    ? Result.Failure($"User with this email: {email} already exists.")
                    : Result.Success();
            }

            private Result<User> CreateUser(Command request)
            {
                var hashedPassword = _passwordHasher.Hash(request.Password);

                var user = new User(
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    hashedPassword,
                    request.Role
                );

                return Result.Success(user);
            }

            private async Task<Result<User>> SaveUser(User user,CancellationToken cancellationToken)
            {
                _appDbContext.Users.Add(user);
                var changes = await _appDbContext.SaveChangesAsync(cancellationToken);

                return changes > 0
                    ? Result.Success(user)
                    : Result.Failure<User>("Failed to save user.");
            }

        }
    }
}