using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Bank;
using LedgerCore.Domain.Entities;
using MediatR;

namespace LedgerCore.Api.Features.Bank
{
    public record ImportBody(Guid AccountId, BankFormat Format, CsvProfile? Profile, string Content, string? FileName, string? OwnAccount, bool? ApplyRules);
    public record PostInvoiceBody(Guid InvoiceId);
    public record SkipBody(bool Skip);
    public record ApplyRulesBody(Guid AccountId);
    public record RuleBody(string Name, string MatchText, RuleDirection Direction, Guid TargetAccountId, string? Description, bool? IsActive);

    public class BankEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var g = app.MapVersionedGroup("bank").WithTags("Bank");
            g.MapPost("import", async (ImportBody b, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ImportStatement.Command(b.AccountId, b.Format, b.Profile ?? CsvProfile.Auto, b.Content, b.FileName, b.OwnAccount, b.ApplyRules ?? true), ct)))
                .RequireAuthorization(Policies.CanWrite);
            g.MapGet("transactions", async (Guid? accountId, BankTransactionStatus? status, Guid? statementId, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new BankTransactions.ListQuery(accountId, status, statementId), ct)));
            g.MapPost("transactions/{id:guid}/post-invoice", async (Guid id, PostInvoiceBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { amount = await s.Send(new BankTransactions.PostToInvoiceCommand(id, b.InvoiceId), ct) })).RequireAuthorization(Policies.CanWrite);
            g.MapPut("transactions/{id:guid}/skip", async (Guid id, SkipBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new BankTransactions.SetSkippedCommand(id, b.Skip), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);
            g.MapPost("apply-rules", async (ApplyRulesBody b, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ApplyBankRules.Command(b.AccountId), ct))).RequireAuthorization(Policies.CanWrite);

            g.MapGet("rules", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new BankRules.ListQuery(), ct)));
            g.MapPost("rules", async (RuleBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(new BankRules.SaveCommand(null, b.Name, b.MatchText, b.Direction, b.TargetAccountId, b.Description, b.IsActive ?? true), ct) }))
                .RequireAuthorization(Policies.CanWrite);
            g.MapPut("rules/{id:guid}", async (Guid id, RuleBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(new BankRules.SaveCommand(id, b.Name, b.MatchText, b.Direction, b.TargetAccountId, b.Description, b.IsActive ?? true), ct) }))
                .RequireAuthorization(Policies.CanWrite);
            g.MapDelete("rules/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            {
                await s.Send(new BankRules.DeleteCommand(id), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);

            g.MapGet("statements", async (Guid accountId, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new BankReconciliation.StatementsQuery(accountId), ct)));
            g.MapGet("reconciliation", async (Guid accountId, DateTime? asOf, decimal? closingBalance, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new BankReconciliation.Query(accountId, asOf, closingBalance), ct)));
        }
    }
}
