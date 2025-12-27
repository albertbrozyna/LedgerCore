using CSharpFunctionalExtensions;
using LedgerCore.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces.Authentication
{
    public interface IVerificationCodeService
    {
        Task<string> GenerateCode(User user,CancellationToken cancellationToken);
        public Task<Result> VerifyUserEmail(Guid userId, string verificationCode, CancellationToken cancellationToken);

    }
}
