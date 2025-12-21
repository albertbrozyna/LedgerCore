using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using Microsoft.AspNetCore.Identity;
namespace LedgerCore.Infrastructure.Services.Identity.Register
{
    public class RegisterUserService(UserManager<User> userManager) : IRegisterUserService
    {
        public async Task<Result<User>> RegisterUser(User user,string password,UserRole role)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return Result.Failure<User>($"User email cannot be empty");
            }

            var result = await userManager.FindByEmailAsync(user.Email);

            if (result is not null)
            {
                return Result.Failure<User>($"User with this email: {user.Email} already exist");
            }

            IdentityResult identityResult = await userManager.CreateAsync(user,password);

            if (!identityResult.Succeeded)
            {
                var errors = string.Join(",", identityResult.Errors.Select(e => e.Description));
                return Result.Failure<User>(errors);
            }

            var assignResult = await userManager.AddToRoleAsync(user,role.ToString());

            if (!assignResult.Succeeded)
            {
                var errors = string.Join(",", assignResult.Errors.Select(e => e.Description));
                return Result.Failure<User>(errors);
            }

            return Result.Success(user);
        }
    }
}
