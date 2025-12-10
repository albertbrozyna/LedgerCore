using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Features.Auth.Commands.Register
{
    public partial class Register
    {
        public record Command(string FirstName,string LastName,string Email,string Password, UserRole Role,string PhoneNumber) :IRequest<Result<Response>>;
        public record Response(Guid UserId);


        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.FirstName).NotEmpty().WithMessage("First name is required").MaximumLength(50);
                RuleFor(x => x.LastName).NotEmpty().WithMessage("Last name is required").MaximumLength(50);
                RuleFor(x => x.Email).NotEmpty().WithMessage("Email is required").EmailAddress().WithMessage("Incorrect email format").MaximumLength(100);
                RuleFor(x => x.Role).IsInEnum().WithMessage("Role is incorrect");
                RuleFor(x => x.Password).NotEmpty().WithMessage("Password cannot be empty!").MinimumLength(8).WithMessage("Password have to be at least 8 letters long");
            }
        }
    }
}

