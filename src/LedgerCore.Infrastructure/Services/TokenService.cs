using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LedgerCore.Infrastructure.Services
{
    public class TokenService(IConfiguration configuration,IAppDbContext appDbContext,IIdentityService identityService) : ITokenService
    {
        public string GenerateAccessToken(User user, IEnumerable<string> roles)
        {
            string secretKey = configuration["Jwt:SecretKey"];

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);



            var roleClaims = new List<Claim>();

            foreach (var role in roles)
            {
                roleClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var tokenDescriptor = new SecurityTokenDescriptor
            {

                Subject = new ClaimsIdentity([..roleClaims,
                    new Claim(JwtRegisteredClaimNames.Sub,user.Id.ToString()),
                    new Claim(JwtRegisteredClaimNames.Email,user.Email.ToString()),
                    new Claim(JwtRegisteredClaimNames.EmailVerified,user.EmailConfirmed.ToString())
                ]),
                Expires = DateTime.UtcNow.AddMinutes(configuration.GetValue<int>("Jwt:ExpiryMinutes")),
                SigningCredentials = credentials,
                Issuer = configuration["Jwt:Issuer"],
                Audience = configuration["Jwt:Audience"]

            };

            var handler = new JsonWebTokenHandler();

            var token = handler.CreateToken(tokenDescriptor);

            return token;
        }

        public async Task<string> GenerateRefreshTokenAsync(Guid userId,CancellationToken cancellationToken)
        {
            // Generujemy bezpieczny, losowy ciąg znaków
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            var tokenString = Convert.ToBase64String(randomNumber);

            // Tworzymy encję i zapisujemy do bazy
            var refreshToken = new RefreshToken(
                tokenString,
                DateTime.UtcNow.AddDays(7), // Żyje np. 7 dni
                userId
            );

            appDbContext.RefreshTokens.Add(refreshToken);
            await appDbContext.SaveChangesAsync(cancellationToken);

            return tokenString;
        }

        public async Task<(string AccessToken, string RefreshToken)> RefreshAsync(string token,CancellationToken cancellationToken)
        {
            // 1. Szukamy tokena w bazie
            var storedToken = await appDbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == token);

            // 2. Weryfikacja
            if (storedToken == null || storedToken.IsRevoked || storedToken.IsExpired())
            {
                throw new Exception("Invalid or expired refresh token");
            }

            // 3. Pobieramy użytkownika
            var user = await appDbContext.Users.FindAsync(storedToken.UserId);

            var userRoles = await identityService.GetUserRoles(user.Id);
            storedToken.Revoke();

            // 5. Generujemy nowy zestaw
            var newAccessToken = GenerateAccessToken(user!,userRoles);
            var newRefreshToken = await GenerateRefreshTokenAsync(user!.Id,cancellationToken);

            return (newAccessToken, newRefreshToken);
        }
    }
}