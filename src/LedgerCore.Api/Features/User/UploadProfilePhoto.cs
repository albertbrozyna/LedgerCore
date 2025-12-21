using Carter;
using MediatR;
using LedgerCore.Application.Features.Users.Commands.UploadProfilePhoto;
using LedgerCore.Api.Common.Extensions;
namespace LedgerCore.Api.Features.User
{
    public class UploadProfilePhotoEndpoint : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var group = app.MapVersionedGroup("users").WithTags("users");

            group.MapPost("upload", async (IFormFile file, ISender sender, CancellationToken ct) =>
            {
                var query = new UploadProfilePhoto.Command(file);
                var result = await sender.Send(query, ct);

                return result.IsFailure ? Results.BadRequest(result.Error) : Results.Ok(result.Value);
            }).RequireAuthorization()
            .DisableAntiforgery();
        }
    }
}
