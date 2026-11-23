using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Api.Infrastructure;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Application.Features.Templates;
using LedgerCore.Application.Features.Users;
using LedgerCore.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Api.Features.Extras
{
    public record ChangePasswordBody(string CurrentPassword, string NewPassword);
    public record TemplateBody(string Name, string? Description, List<TemplateLine> Lines);

    public class ExtrasEndpoints : ICarterModule
    {
        const long MaxAttachmentBytes = 10 * 1024 * 1024;
        static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf", [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        };

        /// <summary>Sprawdza zawartość pliku (a nie tylko rozszerzenie), żeby nie dało się podszyć np. pod PDF.</summary>
        static bool MatchesSignature(string ext, byte[] head) => ext.ToLowerInvariant() switch
        {
            ".pdf" => head.Length >= 4 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46,
            ".png" => head.Length >= 4 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
            ".jpg" or ".jpeg" => head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
            _ => false,
        };

        public void AddRoutes(IEndpointRouteBuilder app)
        {
            // --- zmiana własnego hasła ---
            app.MapVersionedGroup("auth").WithTags("Auth").MapPost("change-password", async (ChangePasswordBody b, ISender s, CancellationToken ct) =>
            {
                var r = await s.Send(new ChangeOwnPassword.Command(b.CurrentPassword, b.NewPassword), ct);
                return r.IsFailure ? Results.Problem(r.Error, statusCode: 400) : Results.NoContent();
            });

            // --- kopie zapasowe ---
            var admin = app.MapVersionedGroup("admin").WithTags("Admin");
            admin.MapGet("backups", (BackupManager m) =>
                Results.Ok(m.List().Select(b => new { name = b.Name, size = b.Size, created = b.Created, manual = b.Name.EndsWith("-manual.db") })))
                .RequireAuthorization(Policies.AdminOnly);
            admin.MapPost("backups", async (BackupManager m, AuditLog audit, IAppDbContext db, CancellationToken ct) =>
            {
                var name = await m.CreateAsync(true, ct);
                await audit.Add("Kopia zapasowa", $"Utworzono kopię zapasową {name}", ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { name });
            }).RequireAuthorization(Policies.AdminOnly);
            admin.MapGet("backups/{name}", (string name, BackupManager m) =>
            {
                if (!BackupManager.NamePattern.IsMatch(name)) return Results.NotFound(); // blokuje ścieżki typu ..\
                var path = Path.Combine(m.Directory_, name);
                return File.Exists(path) ? Results.File(File.OpenRead(path), "application/octet-stream", name) : Results.NotFound();
            }).RequireAuthorization(Policies.AdminOnly);

            // --- szablony księgowań ---
            var templates = app.MapVersionedGroup("templates").WithTags("Templates");
            templates.MapGet("", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new ListTemplates.Query(), ct)));
            templates.MapPost("", async (TemplateBody b, ISender s, CancellationToken ct) =>
                Results.Ok(new { id = await s.Send(new SaveTemplate.Command(b.Name, b.Description ?? "", b.Lines), ct) }))
                .RequireAuthorization(Policies.CanWrite);
            templates.MapDelete("{id:guid}", async (Guid id, ISender s, CancellationToken ct) =>
            {
                await s.Send(new DeleteTemplate.Command(id), ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);

            // --- załączniki do faktur ---
            var invoices = app.MapVersionedGroup("invoices").WithTags("Invoices");
            invoices.MapGet("{id:guid}/attachments", async (Guid id, IAppDbContext db, CancellationToken ct) =>
                Results.Ok(await db.Attachments.AsNoTracking().Where(a => a.InvoiceId == id).OrderBy(a => a.UploadedAt)
                    .Select(a => new { a.Id, a.FileName, a.ContentType, a.Size, a.UploadedAt }).ToListAsync(ct)));

            invoices.MapPost("{id:guid}/attachments", async (Guid id, IFormFile file, IAppDbContext db, IConfiguration cfg, IUserDetails me,
                AuditLog audit, CancellationToken ct) =>
            {
                var inv = await db.Invoices.AsNoTracking().Where(i => i.Id == id).Select(i => i.Number).FirstOrDefaultAsync(ct);
                if (inv is null) return Results.Problem("Nie znaleziono faktury", statusCode: 404);
                if (file.Length == 0) return Results.Problem("Plik jest pusty", statusCode: 400);
                if (file.Length > MaxAttachmentBytes) return Results.Problem("Plik jest za duży (limit 10 MB)", statusCode: 400);
                var ext = Path.GetExtension(file.FileName);
                if (!AllowedTypes.TryGetValue(ext, out var contentType))
                    return Results.Problem("Dozwolone formaty: PDF, PNG, JPG", statusCode: 400);

                var head = new byte[8];
                await using (var s = file.OpenReadStream()) { _ = await s.ReadAsync(head, ct); }
                if (!MatchesSignature(ext, head)) return Results.Problem("Zawartość pliku nie odpowiada jego rozszerzeniu", statusCode: 400);

                var safeName = Path.GetFileName(file.FileName);
                var att = new Attachment(id, safeName, contentType, file.Length, me.User?.Id);
                var dir = Path.Combine(cfg["DataDirectory"]!, "attachments");
                Directory.CreateDirectory(dir);
                await using (var fs = File.Create(Path.Combine(dir, att.Id.ToString("N")))) { await file.CopyToAsync(fs, ct); }

                db.Attachments.Add(att);
                await audit.Add("Załącznik", $"Dodano załącznik „{safeName}” do faktury {inv}", ct);
                await db.SaveChangesAsync(ct);
                return Results.Ok(new { att.Id });
            }).RequireAuthorization(Policies.CanWrite).DisableAntiforgery();

            app.MapVersionedGroup("attachments").WithTags("Invoices").MapGet("{id:guid}", async (Guid id, IAppDbContext db, IConfiguration cfg, CancellationToken ct) =>
            {
                var a = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
                var path = a is null ? null : Path.Combine(cfg["DataDirectory"]!, "attachments", a.Id.ToString("N"));
                return a is null || !File.Exists(path) ? Results.NotFound() : Results.File(File.OpenRead(path), a.ContentType, a.FileName);
            });

            app.MapVersionedGroup("attachments").WithTags("Invoices").MapDelete("{id:guid}", async (Guid id, IAppDbContext db, IConfiguration cfg, AuditLog audit, CancellationToken ct) =>
            {
                var a = await db.Attachments.FirstOrDefaultAsync(x => x.Id == id, ct);
                if (a is null) return Results.NotFound();
                var path = Path.Combine(cfg["DataDirectory"]!, "attachments", a.Id.ToString("N"));
                if (File.Exists(path)) File.Delete(path);
                db.Attachments.Remove(a);
                await audit.Add("Załącznik", $"Usunięto załącznik „{a.FileName}”", ct);
                await db.SaveChangesAsync(ct);
                return Results.NoContent();
            }).RequireAuthorization(Policies.CanWrite);
        }
    }
}
