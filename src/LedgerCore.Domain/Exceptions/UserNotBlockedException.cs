using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Exceptions
{
    internal class UserNotBlockedException : DomainException
    {
        public UserNotBlockedException(Guid guid) : base($"User: {guid} is not blocked now!")
        {
        }
    }
}
