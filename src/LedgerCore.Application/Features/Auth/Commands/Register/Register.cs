using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Auth.Commands.Register
{
    public static class Register
    {
        public record Command(string FirstName, string LastName, string Email, string Password, UserRole Role, string PhoneNumber) : IRequest<Result<Response>>;
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

        public class Handler(IRegisterUserService registerUserService, IVerificationCodeService verificationCodeService, IEmailSender emailSender) : IRequestHandler<Command, Result<Response>>
        {
            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                User newUser = new(request.FirstName, request.LastName, request.Email, request.PhoneNumber);

                var result = await registerUserService.RegisterUser(newUser, request.Password, request.Role).Map(user => new Response(user.Id));

                if (result.IsFailure)
                {
                    return result;
                }

                var code = await verificationCodeService.GenerateCode(newUser, cancellationToken);

                var topic = "Verify your account";

                var content = $"Hello, your verification code to ledgerCoreApp is: {code}";

                await emailSender.SendEmail(topic, content, newUser.Email);

                return result;
            }
        }
    }
}