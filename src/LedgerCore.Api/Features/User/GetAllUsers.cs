using Carter;
using LedgerCore.Api.Extensions;
using LedgerCore.Application.Features.Users.Commands.Block;
using LedgerCore.Application.Features.Users.Queries.GetAllUsers;
using MediatR;

namespace LedgerCore.Api.Features.User
{
    public class GetAllUsersEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapVersionedGroup("users").WithTags("users");

            group.MapGet("", async (ISender sender,CancellationToken ct) =>
            {
                var query = GetAllUsers.Query();
                var result = await sender.Send(query,ct);

                return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
            });
        }
    }
}
