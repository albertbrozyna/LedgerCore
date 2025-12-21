using LedgerCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces
{
    public interface IUserDetails
    {
        User User { get; set; }

    }
}
