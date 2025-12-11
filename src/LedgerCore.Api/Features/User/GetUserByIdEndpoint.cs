using Carter;
using LedgerCore.Api.Extensions;
using LedgerCore.Application.Features.Users.Queries.GetUserByEmail;
using MediatR;

namespace LedgerCore.Api.Features.User
{
    public class GetUserByIdEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = RouteGroupExtensions.MapVersionedGroup(app, "users").WithTags("users");

            group.MapGet("{Id:guid}", async (Guid Id, ISender sender) =>
            {
                var query = new GetUserById.Query(Id);

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
