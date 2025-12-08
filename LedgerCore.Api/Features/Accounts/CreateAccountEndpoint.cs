using Carter;
using LedgerCore.Application.Features.Accounts.Commands.Create;
using MediatR;

namespace LedgerCore.Api.Features.Accounts
{
    public class CreateAccounttEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("api/v1/accounts", async (CreateAccount.Command command, ISender sender) =>
            {
                var result = await sender.Send(command);
                return Results.Ok(result);
            }
            ).WithTags("Accounts")
             .WithName("CreateAccount")
              .WithSummary("Tworzy nowe konto")
              .WithDescription("Creates a new account")
              .Produces<CreateAccount.Result>(StatusCodes.Status200OK)
              .ProducesValidationProblem()
              .Accepts<CreateAccount.Command>("application/json");

        }
    }
}
