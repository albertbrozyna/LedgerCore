using CSharpFunctionalExtensions;
using FluentValidation;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Ledger;
using LedgerCore.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Application.Features.Users;

internal static class UserAudit
{
    public static async Task<string> Email(IAppDbContext db, Guid id, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Email).FirstOrDefaultAsync(ct) ?? id.ToString();

    /// <summary>Zapisuje wpis audytu tylko po udanej operacji.</summary>
    public static async Task<Result> Run(IAppDbContext db, AuditLog audit, Result result, string action, string details, CancellationToken ct)
    {
        if (result.IsSuccess)
        {
            await audit.Add(action, details, ct);
            await db.SaveChangesAsync(ct);
        }
        return result;
    }
}

/// <summary>Blokowanie / odblokowanie / zmiana roli / reset hasła. Administrator nie może zmieniać własnego konta (ochrona przed odcięciem się).</summary>
public static class SetUserActive
{
    public record Command(Guid Id, bool Active) : IRequest<Result>;
    public class Handler(IIdentityService identity, IUserDetails me, AuditLog audit, IAppDbContext db) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command r, CancellationToken ct)
        {
            if (r.Id == me.User?.Id) return Result.Failure("Nie możesz zablokować własnego konta");
            var res = await identity.SetActive(r.Id, r.Active);
            return await UserAudit.Run(db, audit, res, "Użytkownik",
                $"{(r.Active ? "Odblokowano" : "Zablokowano")} użytkownika {await UserAudit.Email(db, r.Id, ct)}", ct);
        }
    }
}

public static class SetUserRole
{
    public record Command(Guid Id, UserRole Role) : IRequest<Result>;
    public class Handler(IIdentityService identity, IUserDetails me, AuditLog audit, IAppDbContext db) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command r, CancellationToken ct)
        {
            if (r.Id == me.User?.Id) return Result.Failure("Nie możesz zmienić własnej roli");
            if (!Enum.IsDefined(r.Role)) return Result.Failure("Nieprawidłowa rola");
            var res = await identity.SetRole(r.Id, r.Role);
            return await UserAudit.Run(db, audit, res, "Użytkownik",
                $"Zmieniono rolę użytkownika {await UserAudit.Email(db, r.Id, ct)} na {r.Role}", ct);
        }
    }
}

public static class ResetUserPassword
{
    public record Command(Guid Id, string NewPassword) : IRequest<Result>;
    public class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).WithMessage("Hasło musi mieć co najmniej 8 znaków");
    }
    public class Handler(IIdentityService identity, AuditLog audit, IAppDbContext db) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command r, CancellationToken ct)
        {
            var res = await identity.ResetPassword(r.Id, r.NewPassword);
            return await UserAudit.Run(db, audit, res, "Użytkownik", $"Zmieniono hasło użytkownika {await UserAudit.Email(db, r.Id, ct)}", ct);
        }
    }
}

public static class ChangeOwnPassword
{
    public record Command(string CurrentPassword, string NewPassword) : IRequest<Result>;
    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Podaj aktualne hasło");
            RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).WithMessage("Nowe hasło musi mieć co najmniej 8 znaków");
        }
    }
    public class Handler(IIdentityService identity, IUserDetails me, AuditLog audit, IAppDbContext db) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command r, CancellationToken ct)
        {
            if (me.User is null) return Result.Failure("Brak zalogowanego użytkownika");
            if (r.CurrentPassword == r.NewPassword) return Result.Failure("Nowe hasło musi różnić się od aktualnego");
            var res = await identity.ChangePassword(me.User.Id, r.CurrentPassword, r.NewPassword);
            return await UserAudit.Run(db, audit, res, "Użytkownik", "Użytkownik zmienił własne hasło", ct);
        }
    }
}
