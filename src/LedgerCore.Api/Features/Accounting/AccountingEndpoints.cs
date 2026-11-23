using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Accounts.Commands.Create;
using LedgerCore.Application.Features.Accounts.Commands.Update;
using LedgerCore.Application.Features.Accounts.Queries.List;
using LedgerCore.Application.Features.Reports;
using LedgerCore.Application.Features.Transactions.Commands.Create;
using LedgerCore.Application.Features.Transactions.Commands.Reverse;
using LedgerCore.Application.Features.Transactions.Queries;
using MediatR;

namespace LedgerCore.Api.Features.Accounting
{
    public record UpdateAccountBody(string Name, bool IsActive);
    public record ReverseBody(DateTime? Date);

    public class AccountingEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var accounts = app.MapVersionedGroup("accounts").WithTags("Accounts");
            accounts.MapGet("", async (bool? includeInactive, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ListAccounts.Query(includeInactive ?? false), ct)));
            accounts.MapPost("", async (CreateAccount.Command c, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(c, ct))).RequireAuthorization(Policies.CanWrite);
            accounts.MapPut("{id:guid}", async (Guid id, UpdateAccountBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new UpdateAccount.Command(id, b.Name, b.IsActive), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);
            accounts.MapGet("{id:guid}/ledger", async (Guid id, DateTime? from, DateTime? to, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new AccountLedger.Query(id, from, to), ct)));

            var tx = app.MapVersionedGroup("transactions").WithTags("Transactions");
            tx.MapGet("", async (DateTime? from, DateTime? to, Guid? accountId, string? search, int? page, int? pageSize,
                    ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ListTransactions.Query(from, to, accountId, search, page ?? 1, pageSize ?? 25), ct)));
            tx.MapGet("{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new GetTransaction.Query(id), ct)));
            tx.MapPost("", async (CreateTransaction.Command c, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(c, ct))).RequireAuthorization(Policies.CanWrite);
            tx.MapPost("{id:guid}/reverse", async (Guid id, ReverseBody? b, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ReverseTransaction.Command(id, b?.Date), ct))).RequireAuthorization(Policies.CanWrite);
            tx.MapGet("export", async (DateTime? from, DateTime? to, ISender s, CancellationToken ct) =>
                Results.File(await s.Send(new ExportJournal.Query(from, to), ct), "text/csv; charset=utf-8",
                    $"dziennik-{DateTime.Today:yyyy-MM-dd}.csv"));

            var reports = app.MapVersionedGroup("reports").WithTags("Reports");
            reports.MapGet("dashboard", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new Dashboard.Query(), ct)));
            reports.MapGet("trial-balance", async (DateTime? from, DateTime? to, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new TrialBalance.Query(from, to), ct)));
            reports.MapGet("profit-and-loss", async (DateTime from, DateTime to, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ProfitAndLoss.Query(from, to), ct)));
        }
    }
}
