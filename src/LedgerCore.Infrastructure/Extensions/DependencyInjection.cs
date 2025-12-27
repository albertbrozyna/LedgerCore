using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Domain.Entities;
using LedgerCore.Infrastructure.Persistence;
using LedgerCore.Infrastructure.Persistence.Context;
using LedgerCore.Infrastructure.Services;
using LedgerCore.Infrastructure.Services.Identity;
using LedgerCore.Infrastructure.Services.Identity.Login;
using LedgerCore.Infrastructure.Services.Identity.Register;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LedgerCore.Infrastructure.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IFileStorageService, LocalFileStorageService>();
            services.AddScoped<IRegisterUserService, RegisterUserService>();
            services.AddScoped<ILoginUserService, LoginUserService>();
            services.AddScoped<IVerificationCodeService, VerificationCodeService>();
            services.AddScoped<IEmailSender, EmailSender>();



            return services;
        }

        public static IServiceCollection AddInitalizer(this IServiceCollection services)
        {
            services.AddScoped<DatabaseInitializer>();

            return services;
        }


        public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<LedgerDbContext>(options =>
                options.UseNpgsql(connectionString));

            services.AddScoped<IAppDbContext>(provider =>
                provider.GetRequiredService<LedgerDbContext>());

            return services;
        }

        public static IServiceCollection AddIdentity(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddIdentityCore<User>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            })
           .AddRoles<IdentityRole<Guid>>()
            .AddRoleManager<RoleManager<IdentityRole<Guid>>>()
            .AddUserManager<UserManager<User>>()
            .AddSignInManager<SignInManager<User>>()
            .AddEntityFrameworkStores<LedgerDbContext>();



            services.AddScoped<ITokenService, TokenService>();
            return services;
        }

    }
}
