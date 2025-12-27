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
    public static class ResendVerificationCode
    {
        public record Command(Guid UserId) : IRequest<Result<Response>>; 
        public record Response();

        public class Validator : AbstractValidator<Command>
        {
            public Validator()
            {
                RuleFor(x => x.UserId).NotEmpty().WithMessage("User Id cannot be empty");
            }
        }

        public class Handler(IVerificationCodeService verificationCodeService, IEmailSender emailSender,IIdentityService identityService) : IRequestHandler<Command, Result<Response>>
        {
            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                var user = await identityService.GetUserById(request.UserId.ToString());

                if(user is null)
                {
                    return Result.Failure<Response>("User with this id doesn't exist");
                }

                var code = await verificationCodeService.GenerateCode(user, cancellationToken);

                var topic = "Verify your account";

                var content = $"Hello, your verification code to ledgerCoreApp is: {code}";

                await emailSender.SendEmail(topic, content, user.Email);

                return Result.Success(new Response());
            }
        }
    }
}