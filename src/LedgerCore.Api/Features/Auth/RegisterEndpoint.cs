using Carter;
using CSharpFunctionalExtensions;
using LedgerCore.Api.Extensions;
using LedgerCore.Application.Features.Auth.Commands.Register;
using MediatR;

namespace LedgerCore.Api.Features.Auth
{
    public class RegisterEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {

            var group = RouteGroupExtensions.MapVersionedGroup(app, "auth").WithTags("Auth");
            group.MapPost("register", async (Register.Command command, ISender sender) =>
            {

                

                var result = await sender.Send(command);

                if (result.IsFailure)
                {
                    return Results.BadRequest(result.Error);
                }

                return Results.Created($"/api/v1/users/{result.Value.UserId}", result.Value);
            }).WithName("RegisterUser")
                .WithSummary("Register a new user")
                .WithDescription("Creates new user and returns his id")
              .Produces<Register.Response>(StatusCodes.Status201Created)
              .Produces(StatusCodes.Status400BadRequest)
              .AllowAnonymous();
        }
    }
}
