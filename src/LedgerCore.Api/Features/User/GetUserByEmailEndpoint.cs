using Carter;
using LedgerCore.Api.Extensions;
using LedgerCore.Application.Features.Users.Queries.GetUserByEmail;
using MediatR;

namespace LedgerCore.Api.Features.User
{
    public class GetUserByEmailEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = RouteGroupExtensions.MapVersionedGroup(app, "users").WithTags("users");

            group.MapGet("", async ([AsParameters] GetUserByEmail.Query query, ISender sender,CancellationToken ct) =>
            {
                var result = await sender.Send(query);

                if (result.IsFailure)
                {
                    return Results.BadRequest(result.Error);
                }

                return Results.Ok(result.Value);
            });

        }
    }
}
