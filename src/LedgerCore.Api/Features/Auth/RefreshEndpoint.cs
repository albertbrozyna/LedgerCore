using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Accounts.Commands.Create;
using LedgerCore.Application.Features.Auth.Commands.Login;
using LedgerCore.Application.Features.Auth.Commands.Refresh;
using MediatR;

namespace LedgerCore.Api.Features.Auth
{
    public class RefreshEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = RouteGroupExtensions.MapVersionedGroup(app, "auth").WithTags("Auth");

            group.MapPost("refresh", async (RefreshToken.Command command, ISender sender) =>
            {
                var result = await sender.Send(command);
                return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
            })
             .WithName("Refresh")
              .WithSummary("Refresh access and refresh tokens")
              .WithDescription("")
              .Produces<RefreshToken.Response>()
              .ProducesValidationProblem()
              .Accepts<RefreshToken.Command>("application/json")
              .AllowAnonymous();
        }
    }
}
