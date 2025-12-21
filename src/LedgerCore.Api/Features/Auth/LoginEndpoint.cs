using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Accounts.Commands.Create;
using LedgerCore.Application.Features.Auth.Commands.Login;
using MediatR;

namespace LedgerCore.Api.Features.Auth
{
    public class LoginEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = RouteGroupExtensions.MapVersionedGroup(app, "auth").WithTags("Auth");

            group.MapPost("login", async (Login.Command command, ISender sender) =>
            {
                var result = await sender.Send(command);
                return result.IsSuccess ? Results.Ok(result.Value) : Results.BadRequest(result.Error);
            })
             .WithName("Login")
              .WithSummary("Log in user to system")
              .WithDescription("Log in user to system and...")
              .Produces<Login.Response>()
              .ProducesValidationProblem()
              .Accepts<CreateAccount.Command>("application/json")
              .AllowAnonymous();
        }
    }
}
