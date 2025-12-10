using LedgerCore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Infrastructure.Persistence
{
    public class DatabaseInitializer(LedgerDbContext appDbContext,ILogger<DatabaseInitializer> logger)
    {
        
        public async Task InitializeAsync()
        {
            logger.LogInformation("Checking database connection...");

            if (!await appDbContext.Database.CanConnectAsync())
                throw new Exception("Cannot connect to database");

            logger.LogInformation("Connected to database.");

            // Automatyczne migracje
            await appDbContext.Database.MigrateAsync();

            logger.LogInformation("Migrations applied successfully.");
        }

    }
}
