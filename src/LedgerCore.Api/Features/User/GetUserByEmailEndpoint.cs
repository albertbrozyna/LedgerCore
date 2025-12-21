using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Users.Queries.GetUserByEmail;
using MediatR;

namespace LedgerCore.Api.Features.User
{
    public class GetUserByEmailEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = RouteGroupExtensions.MapVersionedGroup(app, "users").WithTags("users");

            group.MapGet("{Email}", async (string email, ISender sender,CancellationToken ct) =>
            {
                var query = new GetUserByEmail.Query(email);
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
