using LedgerCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces.Authentication
{
    public interface ITokenService
    {
        public string GenerateAccessToken(User user, IEnumerable<string> roles);
        public Task<string> GenerateRefreshTokenAsync(Guid userId,CancellationToken cancellationToken);
        Task<(string AccessToken, string RefreshToken)> RefreshAsync(string token,CancellationToken cancellationToken);

    }
}
