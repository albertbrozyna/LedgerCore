using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Registers;
using MediatR;

namespace LedgerCore.Api.Features.Registers
{
    public class RegistersEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            // tylko odczyt (także rola Podgląd); dane z bieżącej firmy
            var g = app.MapVersionedGroup("registers").WithTags("Registers");
            g.MapGet("kpir", async (DateTime from, DateTime to, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new KpirRegister.Query(from, to), ct)));
            g.MapGet("ryczalt", async (DateTime from, DateTime to, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new RyczaltRegister.Query(from, to), ct)));
        }
    }
}
