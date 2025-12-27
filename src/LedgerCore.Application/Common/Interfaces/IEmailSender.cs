using CSharpFunctionalExtensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces
{
    public interface IEmailSender
    {
        Task<Result> SendEmail(string title, string content, string userAdress);

    }
}
