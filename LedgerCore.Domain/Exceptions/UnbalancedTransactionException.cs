using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Exceptions
{
    public class UnbalancedTransactionException : DomainException
    {
        public UnbalancedTransactionException(decimal debit,decimal credit) : base($"Unbalanced transaction! sum WN: {debit}, sum MA:{credit} ")
        {

        }
    }
}
