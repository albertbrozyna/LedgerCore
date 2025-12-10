using CSharpFunctionalExtensions;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;
using FluentValidation;

namespace LedgerCore.Application.Features.Users.Commands.Delete
{
    public partial class DeleteUser
    {
        public record Command(Guid UserId) : IRequest<Result<Response>>;
        public record Response();
    }

    public class Validator : AbstractValidator<DeleteUser.Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty().WithMessage("User id cannot be empty.");
        }
    }

}
