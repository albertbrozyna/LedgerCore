using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Accounts.Commands.Create;
using LedgerCore.Application.Features.Auth.Commands.Login;
using LedgerCore.Application.Features.Auth.Commands.Refresh;
using LedgerCore.Application.Features.Auth.Commands.VerifyEmail;
using MediatR;

namespace LedgerCore.Api.Features.Auth
{
    public class VerifyEmailEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = RouteGroupExtensions.MapVersionedGroup(app, "auth").WithTags("Auth");

            group.MapPost("verify", async (VerifyEmail.Command command, ISender sender) =>
            {
                var result = await sender.Send(command);
                return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
            })
             .WithName("Verify emails")
              .WithSummary("Verify user email")
              .WithDescription("")
              .Produces<VerifyEmail.Response>()
              .ProducesValidationProblem()
              .Accepts<VerifyEmail.Command>("application/json")
              .AllowAnonymous();
        }
    }
}
