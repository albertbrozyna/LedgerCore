using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Features.Collections;
using LedgerCore.Domain.Entities;
using MediatR;

namespace LedgerCore.Api.Features.Collections
{
    public record IssueNoticeBody(NoticeBuilder.Request Request, NoticeChannel Channel, string? To, string? Subject, string? Body);
    public record MailSettingsBody(string Host, int Port, bool UseTls, string? UserName, string? Password, string FromAddress, string? FromName,
        string? ReminderSubject, string? ReminderBody, string? InterestSubject, string? InterestBody);
    public record MailTestBody(string To);
    public record InterestRateBody(DateTime ValidFrom, decimal AnnualPercent, string? Note);

    public class CollectionsEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            var g = app.MapVersionedGroup("collections").WithTags("Collections");
            g.MapGet("overdue", async (DateTime? asOf, Guid? contractorId, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ListOverdue.Query(asOf ?? DateTime.Today, contractorId), ct)));
            g.MapPost("preview", async (NoticeBuilder.Request r, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new PreviewNotice.Query(r), ct)))
                .RequireAuthorization(Policies.CanWrite);
            g.MapPost("", async (IssueNoticeBody b, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new IssueNotice.Command(b.Request, b.Channel, b.To, b.Subject, b.Body), ct))).RequireAuthorization(Policies.CanWrite);
            g.MapGet("", async (Guid? contractorId, Guid? invoiceId, ISender s, CancellationToken ct) =>
                Results.Ok(await s.Send(new ListNotices.Query(contractorId, invoiceId), ct)));
            g.MapGet("{id:guid}", async (Guid id, ISender s, CancellationToken ct) => Results.Ok(await s.Send(new GetNotice.Query(id), ct)));

            var admin = app.MapVersionedGroup("admin").WithTags("Admin");
            admin.MapGet("mail", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new MailConfig.GetQuery(), ct))).RequireAuthorization(Policies.AdminOnly);
            admin.MapPut("mail", async (MailSettingsBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new MailConfig.SaveCommand(b.Host, b.Port, b.UseTls, b.UserName, b.Password, b.FromAddress, b.FromName,
                    b.ReminderSubject, b.ReminderBody, b.InterestSubject, b.InterestBody), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);
            admin.MapPost("mail/test", async (MailTestBody b, ISender s, CancellationToken ct) =>
            {
                await s.Send(new MailConfig.TestCommand(b.To), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);

            admin.MapGet("interest-rates", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new InterestRates.ListQuery(), ct)));
            admin.MapPost("interest-rates", async (InterestRateBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(new InterestRates.AddCommand(b.ValidFrom, b.AnnualPercent, b.Note), ct) })).RequireAuthorization(Policies.AdminOnly);
            admin.MapDelete("interest-rates/{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            {
                await s.Send(new InterestRates.DeleteCommand(id), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.AdminOnly);
        }
    }
}
