using LedgerCore.Domain.Enums;
using LedgerCore.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LedgerCore.Infrastructure.Persistence
{
    public class DatabaseInitializer(LedgerDbContext appDbContext, RoleManager<IdentityRole<Guid>> roleManager, ILogger<DatabaseInitializer> logger)
    {
        public async Task InitializeAsync()
        {
            // Tworzy plik bazy przy pierwszym uruchomieniu i stosuje migracje przy aktualizacjach
            await appDbContext.Database.MigrateAsync();
            await SeedRolesAsync();
            logger.LogInformation("Baza danych gotowa.");
        }

        private async Task SeedRolesAsync()
        {
            foreach (var roleName in Enum.GetNames<UserRole>())
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                    await roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
            }
        }
    }
}
