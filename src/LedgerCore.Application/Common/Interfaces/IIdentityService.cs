using CSharpFunctionalExtensions;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;

namespace LedgerCore.Application.Common.Interfaces
{
    public record UserInfo(Guid Id, string FirstName, string LastName, string Email, string? PhoneNumber,
        UserRole Role, bool IsActive, DateTime CreatedAt, DateTime? LastLogin);

    public interface IIdentityService
    {
        Task<Result> UpdateUserProfilePhoto(Guid userId, string profilePhotoUrl);
        Task<IList<string>> GetUserRoles(Guid userId);
        Task<User?> GetUserById(string userId);

        Task<List<UserInfo>> ListUsers(CancellationToken ct);
        Task<UserInfo?> GetUserInfo(Guid userId, CancellationToken ct);
        Task<bool> AnyUsers(CancellationToken ct);
        Task<Result> SetActive(Guid userId, bool active);
        Task<Result> SetRole(Guid userId, UserRole role);
        Task<Result> ResetPassword(Guid userId, string newPassword);
        Task<Result> ChangePassword(Guid userId, string currentPassword, string newPassword);
    }
}
