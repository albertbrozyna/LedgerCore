using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace LedgerCore.Infrastructure.Services.Identity.Login
{
    public class LoginUserService(UserManager<User> userManager, SignInManager<User> signInManager) : ILoginUserService
    {
        public async Task<Result<User>> LoginUser(string email, string password)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user is null || user.IsDeleted)
            {
                return Result.Failure<User>("Nieprawidłowy e-mail lub hasło.");
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                return Result.Failure<User>("Konto jest tymczasowo zablokowane po zbyt wielu próbach logowania. Spróbuj za kilka minut.");
            }

            if (!result.Succeeded)
            {
                return Result.Failure<User>("Nieprawidłowy e-mail lub hasło.");
            }

            if (!user.IsActive)
            {
                return Result.Failure<User>("Konto zostało zablokowane przez administratora.");
            }

            user.SetLastLoginNow();
            await userManager.UpdateAsync(user);

            return Result.Success(user);
        }

        public async Task<IList<string>> GetUserRoles(User user)
        {
            return await userManager.GetRolesAsync(user);
        }
    }
}
