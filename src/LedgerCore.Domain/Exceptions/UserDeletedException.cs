using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Exceptions
{
    public class UserDeletedException : DomainException
    {
        public UserDeletedException(Guid userId) : base($"User: {userId} is deleted.")
        {
        }
    }
}
