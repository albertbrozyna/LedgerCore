using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Entities;

namespace LedgerCore.Api
{
    public class CurrentUserDetails(IHttpContextAccessor httpContextAccessor) : IUserDetails
    {
        public User User
        {
            get
            {
                var sub = httpContextAccessor.HttpContext?.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                return Guid.TryParse(sub, out var id) ? new User { Id = id } : null!;
            }
            set => throw new NotImplementedException("You cannot set user data manually here.");
        }
    }
}
