using Carter;
using CSharpFunctionalExtensions;
using LedgerCore.Application.Features.Auth.Commands.Register;
using MediatR;

namespace LedgerCore.Api.Features.Auth
{
    public class RegisterEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            app.MapPost("api/v1/auth/register", async (Register.Command command, ISender sender) =>
            {

                var result = await sender.Send(command);

                if (result.IsFailure)
                {
                    return Results.BadRequest(result.Error);
                }

                return Results.Ok(result);
            }).WithName("RegisterUser")
                .WithTags("Authentication")
                .WithSummary("Register a new user")
                .WithDescription("Creates new user and returns his id")
              .Produces<Register.Response>(StatusCodes.Status201Created)
              .Produces(StatusCodes.Status400BadRequest)
              .AllowAnonymous();
        }
    }
}
