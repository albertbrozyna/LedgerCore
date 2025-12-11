using Asp.Versioning;
using Asp.Versioning.Builder;

// Namespace wskazuje na Twój nowy folder
namespace LedgerCore.Api.Extensions
{
    public static class RouteGroupExtensions
    {
        private static ApiVersionSet? _apiVersionSet;

        public static RouteGroupBuilder MapVersionedGroup(this IEndpointRouteBuilder app, string groupName)
        {
            // Tworzymy zestaw wersji tylko raz (Singleton)
            if (_apiVersionSet == null)
            {
                _apiVersionSet = app.NewApiVersionSet()
                    .HasApiVersion(new ApiVersion(1))
                    // .HasApiVersion(new ApiVersion(2))
                    .ReportApiVersions()
                    .Build();
            }

            // Tworzymy grupę: api/v1/auth, api/v1/users itp.
            return app
                .MapGroup($"api/v{{version:apiVersion}}/{groupName}")
                .WithApiVersionSet(_apiVersionSet);
        }
    }
}