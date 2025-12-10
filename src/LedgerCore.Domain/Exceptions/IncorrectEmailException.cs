using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Exceptions
{
    public class IncorrectEmailException :DomainException
    {
        public IncorrectEmailException(string email) : base($"Given email: {email} is incorrect.")
        {

        }
    }
}
