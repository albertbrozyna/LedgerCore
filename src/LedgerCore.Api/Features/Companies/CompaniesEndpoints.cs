using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Companies;
using MediatR;

namespace LedgerCore.Api.Features.Companies
{
    public record CompanyCreateBody(string Name, string? Nip, string? Address, bool? SeedChartOfAccounts);
    public record CompanyStatusBody(string Name, bool IsActive);
    public record UserCompaniesBody(List<Guid> CompanyIds);

    public class CompaniesEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var companies = app.MapVersionedGroup("companies").WithTags("Companies");
            // lista firm dostępnych dla zalogowanego użytkownika (przełącznik firmy w interfejsie)
            companies.MapGet("mine", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new ListMyCompanies.Query(), ct)));
            companies.MapGet("", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new ListAllCompanies.Query(), ct)))
                .RequireAuthorization(Policies.AdminOnly);
            companies.MapPost("", async (CompanyCreateBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(new CreateCompany.Command(b.Name, b.Nip, b.Address, b.SeedChartOfAccounts ?? true), ct) }))
                .RequireAuthorization(Policies.AdminOnly);
            companies.MapPut("{id:guid}", async (Guid id, CompanyStatusBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new UpdateCompanyStatus.Command(id, b.Name, b.IsActive), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);

            var users = app.MapVersionedGroup("users").WithTags("users");
            users.MapGet("{id:guid}/companies", async (Guid id, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new UserCompanies.GetQuery(id), ct))).RequireAuthorization(Policies.AdminOnly);
            users.MapPut("{id:guid}/companies", async (Guid id, UserCompaniesBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new UserCompanies.SetCommand(id, b.CompanyIds), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);
        }
    }
}
