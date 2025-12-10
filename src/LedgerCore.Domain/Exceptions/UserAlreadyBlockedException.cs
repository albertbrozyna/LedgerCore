using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Exceptions
{
    internal class UserAlreadyBlockedException : DomainException
    {
        public UserAlreadyBlockedException(Guid guid) : base($"User: {guid} is already blocked now!")
        {
        }
    }
}
