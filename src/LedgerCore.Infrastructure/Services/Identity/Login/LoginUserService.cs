using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Infrastructure.Services.Identity.Login
{
    public class LoginUserService(UserManager<User> userManager, SignInManager<User> signInManager) : ILoginUserService
    {
        public async Task<Result<User>> LoginUser(string email, string password)
        {

            var user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                return Result.Failure<User>("Invalid email or password.");
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, password, false);

            if (result.Succeeded)
            {
                return Result.Success(user);
            }

            if (result.IsLockedOut)
            {
                return Result.Failure<User>("User account locked.");
            }

            return Result.Failure<User>("Invalid email or password.");
        }

        public async Task<IList<string>> GetUserRoles(User user)
        {
            return await userManager.GetRolesAsync(user);
        }
    }
}
