using CSharpFunctionalExtensions;
using LedgerCore.Domain.Entities;
using LedgerCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces.Authentication
{
    public interface IRegisterUserService
    {
        public Task<Result<User>> RegisterUser(User user,string password,UserRole role);
    }
}
