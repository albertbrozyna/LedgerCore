using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using MediatR;

namespace LedgerCore.Application.Features.Auth.Commands.VerifyEmail
{
    public static class VerifyEmail
    {
        public record Command(string VerficationCode,Guid UserId) : IRequest<Result<Response>>;
        public record Response();

        public class Handler( IVerificationCodeService verificationCodeService) : IRequestHandler<Command, Result<Response>>
        {
            public async Task<Result<Response>> Handle(Command request, CancellationToken cancellationToken)
            {
                var result = await verificationCodeService.VerifyUserEmail(request.UserId, request.VerficationCode, cancellationToken);

                if (result.IsFailure)
                {
                    return Result.Failure<Response>(result.Error);
                }
                return Result.Success(new Response());

            }
        }


    }
}
