using LedgerCore.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Application.Common.Interfaces
{
    public interface IAppDbContext
    {
        DbSet<Account>Accounts{get;}
        DbSet<Transaction> Transactions { get; }
        DbSet<User> Users { get; }
        DbSet<RefreshToken>RefreshTokens { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
