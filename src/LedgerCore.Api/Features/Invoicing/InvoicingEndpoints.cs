using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Administration;
using LedgerCore.Application.Features.Contractors;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Domain.Enums;
using MediatR;

namespace LedgerCore.Api.Features.Invoicing
{
    public record ContractorBody(string Name, string? Nip, string? Address, string? Email, bool? IsActive);
    public record PaymentBody(DateTime Date, decimal Amount, Guid AccountId);
    public record CorrectionBody(string? Number, DateTime IssueDate, string? Reason, List<CreateCorrection.LineDto> Lines);
    public record PeriodLockBody(DateTime? LockedThrough);
    public record CompanyBody(string Name, string? Nip, string? Address, string? Email, string? Phone, string? BankAccount, string? InvoiceFooter);
    public record CloseYearBody(int Year);

    public class InvoicingEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var contractors = app.MapVersionedGroup("contractors").WithTags("Contractors");
            contractors.MapGet("", async (string? search, bool? includeInactive, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ListContractors.Query(search, includeInactive ?? false), ct)));
            contractors.MapPost("", async (ContractorBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(new SaveContractor.Command(null, b.Name, b.Nip, b.Address, b.Email), ct) }))
                .RequireAuthorization(Policies.CanWrite);
            contractors.MapPut("{id:guid}", async (Guid id, ContractorBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new SaveContractor.Command(id, b.Name, b.Nip, b.Address, b.Email, b.IsActive ?? true), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);

            var invoices = app.MapVersionedGroup("invoices").WithTags("Invoices");
            invoices.MapGet("", async (InvoiceType? type, Guid? contractorId, DateTime? from, DateTime? to, string? status, string? search,
                    int? page, int? pageSize, string? sort, string? dir, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ListInvoices.Query(type, contractorId, from, to, status, search, page ?? 1, pageSize ?? 25, sort, dir), ct)));
            invoices.MapGet("{id:guid}", async (Guid id, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetInvoice.Query(id), ct)));
            invoices.MapPost("", async (CreateInvoice.Command c, ISender s, CancellationToken ct) => Results.Ok(await s.Send(c, ct)))
                .RequireAuthorization(Policies.CanWrite);
            invoices.MapPost("{id:guid}/payments", async (Guid id, PaymentBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new RecordPayment.Command(id, b.Date, b.Amount, b.AccountId), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);
            invoices.MapPost("{id:guid}/corrections", async (Guid id, CorrectionBody b, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new CreateCorrection.Command(id, b.Number, b.IssueDate, b.Reason, b.Lines), ct)))
                .RequireAuthorization(Policies.CanWrite);
            invoices.MapPost("{id:guid}/cancel", async (Guid id, ISender s, CancellationToken ct) =>
            {
                await s.Send(new CancelInvoice.Command(id), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);

            var reports = app.MapVersionedGroup("reports").WithTags("Reports");
            reports.MapGet("vat-register", async (InvoiceType type, DateTime from, DateTime to, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new VatRegister.Query(type, from, to), ct)));
            reports.MapGet("vat-summary", async (DateTime from, DateTime to, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new VatSummary.Query(from, to), ct)));
            reports.MapGet("balance-sheet", async (DateTime asOf, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new LedgerCore.Application.Features.Reports.BalanceSheet.Query(asOf), ct)));
            reports.MapGet("settlements", async (InvoiceType type, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new Settlements.Query(type), ct)));

            var admin = app.MapVersionedGroup("admin").WithTags("Admin");
            admin.MapGet("period-lock", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetPeriodLock.Query(), ct)));
            admin.MapPut("period-lock", async (PeriodLockBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new SetPeriodLock.Command(b.LockedThrough), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);
            admin.MapGet("company", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetCompany.Query(), ct)));
            admin.MapPut("company", async (CompanyBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new SaveCompany.Command(b.Name, b.Nip, b.Address, b.Email, b.Phone, b.BankAccount, b.InvoiceFooter), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);
            admin.MapPost("close-year", async (CloseYearBody b, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new CloseYear.Command(b.Year), ct))).RequireAuthorization(Policies.AdminOnly);
            admin.MapGet("audit", async (string? search, int? page, int? pageSize, string? sort, string? dir, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ListAudit.Query(search, page ?? 1, pageSize ?? 50, sort, dir), ct))).RequireAuthorization(Policies.AdminOnly);
        }
    }
}
