using LedgerCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces.Authentication
{
    public interface IJwtTokenProvider
    {
        public string Generate(User user,IEnumerable<string> roles);
    }
}
