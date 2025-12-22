using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Exceptions;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Infrastructure.Services.Identity
{
    public class IdentityService(UserManager<User>userManager) : IIdentityService
    {
        public async Task<IList<string>> GetUserRoles(Guid userId)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());

            if(user is null)
            {
                throw new UserNotFoundException($"User with id: {userId} was not found");
            }

            return await userManager.GetRolesAsync(user);
        }

        public async Task<Result> UpdateUserProfilePhoto(Guid userId, string profilePhotoUrl)
        {
            var user = await userManager.FindByIdAsync(userId.ToString());

            if (user is null)
            {
                return Result.Failure("User not found");
            }

            user.UpdateProfilePhotoUrl(profilePhotoUrl);

            IdentityResult result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return Result.Failure("Failed to update user");
            }

            return Result.Success();
        }
    }
}
