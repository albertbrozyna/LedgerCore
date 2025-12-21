using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Users.Commands.Block;
using LedgerCore.Domain.Enums;
using MediatR;

namespace LedgerCore.Api.Features.User
{
    public class BlockUserEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapVersionedGroup("users").WithTags("users");

            group.MapPost("{Id:guid}/block", async (Guid Id, ISender sender,CancellationToken ct) =>
            {
                var command = new BlockUser.Command(Id);
                var result = await sender.Send(command);

                return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
            }).RequireAuthorization(policy => policy.RequireRole(UserRole.Admin.ToString()));
        }
    }
}
