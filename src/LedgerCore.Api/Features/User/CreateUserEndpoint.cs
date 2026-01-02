using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Users.Commands.Create;
using LedgerCore.Domain.Enums;
using MediatR;

namespace LedgerCore.Api.Features.User
{
    public class CreateUserEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapVersionedGroup("users").WithTags("users");

            group.MapPost("create", async (CreateUser.Command command, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(command);

                return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
            }).RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));

        }
    }
}
