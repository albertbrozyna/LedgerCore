using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Search;
using MediatR;

namespace LedgerCore.Api.Features.Search
{
    public class SearchEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            // tylko odczyt – dostępne także dla roli Podgląd; wyniki zawsze z bieżącej firmy (X-Company-Id)
            app.MapVersionedGroup("search").WithTags("Search")
                .MapGet("", async (string? q, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GlobalSearch.Query(q), ct)));
        }
    }
}
