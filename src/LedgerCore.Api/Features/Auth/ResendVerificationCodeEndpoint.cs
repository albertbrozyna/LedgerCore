using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Auth.Commands.Register;
using MediatR;

namespace LedgerCore.Api.Features.Auth
{
    public class ResendVerficationCodeEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {

            var group = RouteGroupExtensions.MapVersionedGroup(app, "auth").WithTags("Auth");
            group.MapPost("resend-verification-code", async (ResendVerificationCode.Command command, ISender sender) =>
            {
                var result = await sender.Send(command);

                if (result.IsFailure)
                {
                    return Results.Problem(result.Error, statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.Ok(result.Value);
            }).WithName("RegisterUser")
                .WithSummary("Resend verification code")
                .WithDescription("Resend verification code to users email")
              .Produces(StatusCodes.Status400BadRequest)
              .AllowAnonymous()
              .RequireCors("Frontend");
        }
    }
}
