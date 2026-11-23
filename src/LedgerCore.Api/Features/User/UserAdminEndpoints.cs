using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Features.Setup;
using LedgerCore.Application.Features.Users;
using LedgerCore.Application.Features.Users.Queries.GetAllUsers;
using LedgerCore.Domain.Enums;
using LedgerCore.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Api.Features.User
{
    public record RoleBody(UserRole Role);
    public record PasswordBody(string NewPassword);

    public class UserAdminEndpoints : ICarterModule
    {
        public void AddRoutes(IEndpointRouteBuilder app)
        {
            // --- pierwsze uruchomienie (publiczne, działa tylko dopóki nie ma użytkowników) ---
            var setup = app.MapVersionedGroup("setup").WithTags("Setup");
            setup.MapGet("status", async (ISender s, CancellationToken ct) => Results.Ok(await s.Send(new SetupStatus.Query(), ct)))
                .AllowAnonymous();
            setup.MapPost("", async (InitialSetup.Command c, ISender s, CancellationToken ct) =>
            {
                var r = await s.Send(c, ct);
                return r.IsFailure ? Results.BadRequest(new { detail = r.Error }) : Results.Ok(new { userId = r.Value });
            }).AllowAnonymous();

            // --- bieżący użytkownik ---
            app.MapVersionedGroup("auth").WithTags("Auth").MapGet("me", async (IUserDetails me, IIdentityService identity, CancellationToken ct) =>
            {
                var info = me.User is null ? null : await identity.GetUserInfo(me.User.Id, ct);
                return info is null ? Results.Unauthorized() : Results.Ok(info);
            });

            // --- administracja użytkownikami ---
            var users = app.MapVersionedGroup("users").WithTags("users");
            users.MapGet("", async (ISender s, CancellationToken ct) =>
            {
                var r = await s.Send(new GetAllUsers.Query(), ct);
                return Results.Ok(r.Value.Users);
            }).RequireAuthorization(Policies.AdminOnly);

            users.MapPost("{id:guid}/block", async (Guid id, ISender s, CancellationToken ct) =>
                Respond(await s.Send(new SetUserActive.Command(id, false), ct))).RequireAuthorization(Policies.AdminOnly);
            users.MapPost("{id:guid}/unblock", async (Guid id, ISender s, CancellationToken ct) =>
                Respond(await s.Send(new SetUserActive.Command(id, true), ct))).RequireAuthorization(Policies.AdminOnly);
            users.MapPut("{id:guid}/role", async (Guid id, RoleBody b, ISender s, CancellationToken ct) =>
                Respond(await s.Send(new SetUserRole.Command(id, b.Role), ct))).RequireAuthorization(Policies.AdminOnly);
            users.MapPost("{id:guid}/password", async (Guid id, PasswordBody b, ISender s, CancellationToken ct) =>
                Respond(await s.Send(new ResetUserPassword.Command(id, b.NewPassword), ct))).RequireAuthorization(Policies.AdminOnly);

            // --- kopia zapasowa bazy (pojedynczy plik SQLite) ---
            app.MapVersionedGroup("admin").WithTags("Admin").MapGet("backup", async (LedgerDbContext db, CancellationToken ct) =>
            {
                var tmp = Path.Combine(Path.GetTempPath(), $"ledgercore-backup-{Guid.NewGuid():N}.db");
                try
                {
                    // VACUUM INTO tworzy spójną kopię nawet podczas pracy aplikacji; ścieżka jest generowana przez nas
                    await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{tmp.Replace("'", "''")}'", ct);
                    var bytes = await File.ReadAllBytesAsync(tmp, ct);
                    return Results.File(bytes, "application/octet-stream", $"ledgercore-backup-{DateTime.Now:yyyy-MM-dd-HHmm}.db");
                }
                finally { if (File.Exists(tmp)) File.Delete(tmp); }
            }).RequireAuthorization(Policies.AdminOnly);
        }

        private static IResult Respond(CSharpFunctionalExtensions.Result r) =>
            r.IsFailure ? Results.BadRequest(new { detail = r.Error }) : Results.NoContent();
    }
}
