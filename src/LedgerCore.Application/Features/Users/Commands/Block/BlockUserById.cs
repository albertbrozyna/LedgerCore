using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Features.Users.Commands.Block
{
    public static class BlockUser
    {
        public record Command(Guid Id) : IRequest<Result<Response>>;
        public record Response(Guid Id);
        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.Id).NotEmpty().WithName("User id cannot be empty");
            }
        }

        public class BlockUserHandler : IRequestHandler<Command, Result<Response>>
        {
            private readonly IAppDbContext _appDbContext;

            public BlockUserHandler(IAppDbContext appDbContext)
            {
                _appDbContext = appDbContext;
            }

            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                var user = await _appDbContext.Users.FirstOrDefaultAsync(user => user.Id == request.Id);


                return await user.AsMaybe().ToResult($"User with Id: {request.Id} was not found")
                    .Ensure(user => user.IsActive, $"User with Id {request.Id} is already blocked")
                    .Tap(user => user.BlockUser())
                    .Tap(async _ => await _appDbContext.SaveChangesAsync(cancellationToken))
                    .Map(user => new Response(user.Id));
            }
        }
    }
}
