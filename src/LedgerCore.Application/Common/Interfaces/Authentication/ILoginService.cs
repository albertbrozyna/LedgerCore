using CSharpFunctionalExtensions;
using LedgerCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces.Authentication
{
    public interface ILoginUserService
    {
        public Task<Result<User>> LoginUser(string email, string password);
        public Task<IList<string>> GetUserRoles(User user);

    }
}