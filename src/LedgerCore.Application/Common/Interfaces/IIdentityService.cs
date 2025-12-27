using CSharpFunctionalExtensions;
using LedgerCore.Domain.Entities;

namespace LedgerCore.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<Result> UpdateUserProfilePhoto(Guid userId, string profilePhotoUrl);

        Task<IList<string>> GetUserRoles(Guid userId);

        Task<User?> GetUserById(string userId);
    }
}
