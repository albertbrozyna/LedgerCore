using CSharpFunctionalExtensions;

namespace LedgerCore.Application.Common.Interfaces
{
    public interface IIdentityService
    {
        Task<Result> UpdateUserProfilePhoto(Guid userId, string profilePhotoUrl);

        Task<IList<string>> GetUserRoles(Guid userId);
    }
}
