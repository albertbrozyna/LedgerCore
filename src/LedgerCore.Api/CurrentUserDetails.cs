using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;
using Microsoft.AspNetCore.Http;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace LedgerCore.Api
{
    public class CurrentUserDetails(IHttpContextAccessor httpContextAccessor) : IUserDetails
    {

        public User User
        {
            get
            {
                var principal = httpContextAccessor.HttpContext?.User;
                if (principal == null)
                {
                    return null;
                }

                return new User
                {
                    Id = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)),
                };
            }
            set => throw new NotImplementedException("You cannot set user data manually here.");
        }
    }
}