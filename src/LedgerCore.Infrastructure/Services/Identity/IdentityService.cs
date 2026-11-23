using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using LedgerCore.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Infrastructure.Services.Identity
{
    public class IdentityService(UserManager<User> userManager) : IIdentityService
    {
        public async Task<User?> GetUserById(string userId) => await userManager.FindByIdAsync(userId);

        public async Task<IList<string>> GetUserRoles(Guid userId)
        {
            var user = await userManager.FindByIdAsync(userId.ToString())
                       ?? throw new UserNotFoundException($"Nie znaleziono użytkownika {userId}");
            return await userManager.GetRolesAsync(user);
        }

        public async Task<Result> UpdateUserProfilePhoto(Guid userId, string profilePhotoUrl)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());
            if (user is null) return Result.Failure("User not found");
            user.UpdateProfilePhotoUrl(profilePhotoUrl);
            var result = await userManager.UpdateAsync(user);
            return result.Succeeded ? Result.Success() : Result.Failure("Failed to update user");
        }

        private async Task<UserInfo> ToInfo(User u)
        {
            var roles = await userManager.GetRolesAsync(u);
            var role = Enum.TryParse<UserRole>(roles.FirstOrDefault(), out var r) ? r : UserRole.Viewer;
            return new UserInfo(u.Id, u.FirstName, u.LastName, u.Email ?? "", u.PhoneNumber, role, u.IsActive, u.CreatedAt,
                u.LastLogin == default ? null : u.LastLogin);
        }

        public async Task<List<UserInfo>> ListUsers(CancellationToken ct)
        {
            var users = await userManager.Users.AsNoTracking().Where(u => !u.IsDeleted).OrderBy(u => u.LastName).ToListAsync(ct);
            var list = new List<UserInfo>();
            foreach (var u in users) list.Add(await ToInfo(u));
            return list;
        }

        public async Task<UserInfo?> GetUserInfo(Guid userId, CancellationToken ct)
        {
            var u = await userManager.FindByIdAsync(userId.ToString());
            return u is null || u.IsDeleted ? null : await ToInfo(u);
        }

        public Task<bool> AnyUsers(CancellationToken ct) => userManager.Users.AnyAsync(ct);

        public async Task<Result> SetActive(Guid userId, bool active)
        {
            var u = await userManager.FindByIdAsync(userId.ToString());
            if (u is null) return Result.Failure("Nie znaleziono użytkownika");
            if (active) u.UnblockUser(); else u.BlockUser();
            var r = await userManager.UpdateAsync(u);
            return r.Succeeded ? Result.Success() : Result.Failure(string.Join(", ", r.Errors.Select(e => e.Description)));
        }

        public async Task<Result> SetRole(Guid userId, UserRole role)
        {
            var u = await userManager.FindByIdAsync(userId.ToString());
            if (u is null) return Result.Failure("Nie znaleziono użytkownika");
            var current = await userManager.GetRolesAsync(u);
            await userManager.RemoveFromRolesAsync(u, current);
            var r = await userManager.AddToRoleAsync(u, role.ToString());
            return r.Succeeded ? Result.Success() : Result.Failure(string.Join(", ", r.Errors.Select(e => e.Description)));
        }

        public async Task<Result> ChangePassword(Guid userId, string currentPassword, string newPassword)
        {
            var u = await userManager.FindByIdAsync(userId.ToString());
            if (u is null) return Result.Failure("Nie znaleziono użytkownika");
            var r = await userManager.ChangePasswordAsync(u, currentPassword, newPassword);
            return r.Succeeded ? Result.Success()
                : Result.Failure(r.Errors.Any(e => e.Code == "PasswordMismatch") ? "Aktualne hasło jest nieprawidłowe" : string.Join(", ", r.Errors.Select(e => e.Description)));
        }

        public async Task<Result> ResetPassword(Guid userId, string newPassword)
        {
            var u = await userManager.FindByIdAsync(userId.ToString());
            if (u is null) return Result.Failure("Nie znaleziono użytkownika");
            var hasPassword = await userManager.HasPasswordAsync(u);
            if (hasPassword) await userManager.RemovePasswordAsync(u);
            var r = await userManager.AddPasswordAsync(u, newPassword);
            if (r.Succeeded) await userManager.ResetAccessFailedCountAsync(u);
            return r.Succeeded ? Result.Success() : Result.Failure(string.Join(", ", r.Errors.Select(e => e.Description)));
        }
    }
}
