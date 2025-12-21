using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Infrastructure.Persistence
{
    public class DatabaseInitializer(LedgerDbContext appDbContext, RoleManager<IdentityRole<Guid>> roleManager, ILogger<DatabaseInitializer> logger)
    {

        public async Task InitializeAsync()
        {
            logger.LogInformation("Checking database connection...");

            if (!await appDbContext.Database.CanConnectAsync())
                throw new Exception("Cannot connect to database");

            logger.LogInformation("Connected to database.");

            // Automatyczne migracje
            await appDbContext.Database.MigrateAsync();
            await SeedRolesAsync();
            logger.LogInformation("Migrations applied successfully.");
        }


        private async Task SeedRolesAsync()
        {
            string[] roleNames = Enum.GetNames<UserRole>();

            foreach (var roleName in roleNames)
            {
                // Sprawdzamy czy rola już istnieje
                var roleExist = await roleManager.RoleExistsAsync(roleName);

                if (!roleExist)
                {
                    logger.LogInformation("Creating role: {RoleName}", roleName);
                    // Tworzymy nową rolę
                    await roleManager.CreateAsync(new IdentityRole<Guid>
                    {
                        Name = roleName
                    });
                }
            }

        }
    }
}
