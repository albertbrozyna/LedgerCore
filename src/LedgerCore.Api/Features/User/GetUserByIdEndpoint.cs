using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Users.Queries.GetUserByEmail;
using MediatR;

namespace LedgerCore.Api.Features.User
{
    public class GetUserByIdEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = RouteGroupExtensions.MapVersionedGroup(app, "users").WithTags("users");

            group.MapGet("{Id:guid}", async (Guid Id, ISender sender, CancellationToken ct) =>
            {
                var query = new GetUserById.Query(Id);

                var result = await sender.Send(query);

                return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
            });

        }
    }
}
