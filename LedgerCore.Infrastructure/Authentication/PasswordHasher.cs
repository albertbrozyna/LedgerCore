using BCrypt.Net;
using LedgerCore.Application.Common.Interfaces.Authentication;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Infrastructure.Authentication
{
    public class PasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        public bool Verify(string password, string hashedPassword)
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
    }
}
