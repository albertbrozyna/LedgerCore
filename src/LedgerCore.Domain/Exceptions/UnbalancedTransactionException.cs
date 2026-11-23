using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Domain.Exceptions
{
    public class UnbalancedTransactionException : DomainException
    {
        public UnbalancedTransactionException(decimal debit,decimal credit) : base($"Księgowanie nie jest zbilansowane: suma Winien = {debit:0.00}, suma Ma = {credit:0.00}")
        {

        }
    }
}
