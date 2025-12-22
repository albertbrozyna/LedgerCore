using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Entities
{
    public class RefreshToken
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public User User { get; private set; }

        public string Token { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public bool IsRevoked { get; private set; }

        private RefreshToken() { } 

        public RefreshToken(string token, DateTime expiresAt, Guid userId)
        {
            Id = Guid.NewGuid();
            Token = token;
            ExpiresAt = expiresAt;
            UserId = userId;
            IsRevoked = false;
        }

        public void Revoke()
        {
            IsRevoked = true;
        }

        public bool IsExpired() => DateTime.UtcNow >= ExpiresAt;
    }
}
