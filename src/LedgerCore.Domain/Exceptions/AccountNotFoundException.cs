using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Exceptions
{
    public class AccountNotFoundException : DomainException
    {
        public AccountNotFoundException(Guid accountId) : base($"Account with ID: {accountId} was not found")
        {

        }
        }
}
