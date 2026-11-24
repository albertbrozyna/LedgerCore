using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using MediatR;

namespace LedgerCore.Api.Features.Invoicing
{
    public record RecurringBody(string Name, Guid ContractorId, RecurrenceFrequency Frequency, int DayOfMonth, int DueDays, DateTime NextDate,
        DateTime? EndDate, Guid? ResultAccountId, KpirKind? Kind, decimal? RyczaltRate, string? Notes, List<RecurringInvoices.LineDto> Lines);
    public record RecurringActiveBody(bool Active);
    public record RecurringGenerateBody(DateTime? AsOf, Guid? TemplateId);
    public record NumberingBody(string Pattern, NumberReset Reset);
    public record NumberingPreviewBody(string Pattern, NumberReset Reset, DateTime? Date);

    public class RecurringEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var g = app.MapVersionedGroup("recurring-invoices").WithTags("RecurringInvoices");
            g.MapGet("", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new RecurringInvoices.ListQuery(), ct)));
            g.MapPost("", async (RecurringBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(ToCommand(null, b), ct) })).RequireAuthorization(Policies.CanWrite);
            g.MapPut("{id:guid}", async (Guid id, RecurringBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(ToCommand(id, b), ct) })).RequireAuthorization(Policies.CanWrite);
            g.MapPut("{id:guid}/active", async (Guid id, RecurringActiveBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new RecurringInvoices.SetActiveCommand(id, b.Active), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);
            g.MapDelete("{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            {
                await s.Send(new RecurringInvoices.DeleteCommand(id), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);
            g.MapPost("generate", async (RecurringGenerateBody b, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new RecurringInvoices.GenerateCommand(b.AsOf, b.TemplateId), ct))).RequireAuthorization(Policies.CanWrite);

            var admin = app.MapVersionedGroup("admin").WithTags("Admin");
            admin.MapGet("numbering", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new NumberingSettings.GetQuery(), ct)));
            admin.MapPost("numbering/preview", async (NumberingPreviewBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { example = await s.Send(new NumberingSettings.PreviewQuery(b.Pattern, b.Reset, b.Date), ct) })).RequireAuthorization(Policies.AdminOnly);
            admin.MapPut("numbering", async (NumberingBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new NumberingSettings.SaveCommand(b.Pattern, b.Reset), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);
        }

        static RecurringInvoices.SaveCommand ToCommand(Guid? id, RecurringBody b) =>
            new(id, b.Name, b.ContractorId, b.Frequency, b.DayOfMonth, b.DueDays, b.NextDate, b.EndDate, b.ResultAccountId, b.Kind, b.RyczaltRate, b.Notes, b.Lines ?? []);
    }
}
