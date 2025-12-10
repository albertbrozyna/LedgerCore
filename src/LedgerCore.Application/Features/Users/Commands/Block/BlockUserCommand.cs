using CSharpFunctionalExtensions;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Features.Users.Commands.Block
{
    public partial class BlockUser
    {

        public record Command(Guid UserId) : IRequest<Result<Response>>;
        public record Response(Guid UserId);


        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.UserId).NotEmpty().WithName("User id cannot be empty");
            }
        }
    }
}
