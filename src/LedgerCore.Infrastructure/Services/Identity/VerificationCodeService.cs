using CSharpFunctionalExtensions;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace LedgerCore.Infrastructure.Services.Identity;

public class VerificationCodeService : IVerificationCodeService
{
    private readonly IAppDbContext _db;
    private readonly UserManager<User> _userManager;


    public VerificationCodeService(IAppDbContext db,UserManager<User> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<string> GenerateCode(
        User user,
        CancellationToken cancellationToken)
    {
        var code = GenerateNumericCode(6);

        var hash = HashCode(code);
        var oldTokens = await _db.UserVerificationTokens
            .Where(x => x.UserId == user.Id
                     && x.Type == UserVerificationTokenType.Email
                     && x.UsedAt == null).ToListAsync() ;

        foreach (var t in oldTokens)
            t.MarkAsUsed();

        var token = new UserVerificationToken(
            user.Id,
            hash,
            UserVerificationTokenType.Email,
            DateTime.UtcNow.AddMinutes(15)
        );

        _db.UserVerificationTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);
        return code;
    }

    private static string GenerateNumericCode(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return string.Concat(bytes.Select(b => (b % 10).ToString()));
    }

    private static string HashCode(string code)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(
            sha.ComputeHash(Encoding.UTF8.GetBytes(code)));
    }


    public async Task<Result> VerifyUserEmail(Guid userId, string verificationCode, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return Result.Failure($"User with id: {userId} not found");
        }

        if (user.IsEmailVerified())
        {
            return Result.Failure($"User with id: {userId} is already verified");
        }


        var hashedCode = HashCode(verificationCode);
        var token = await _db.UserVerificationTokens.FirstOrDefaultAsync(t => t.UserId.Equals(user.Id) && t.TokenHash == hashedCode && t.UsedAt == null);

        if (token is null)
        {
            return Result.Failure("Incorrect verification code");
        }

        if (token.IsExpired())
        {
            return Result.Failure("Verification code has expired");
        }

        token.MarkAsUsed();


        user.VerifyEmail();

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            return Result.Failure("Failed to update user status in Identity.");
        }
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
