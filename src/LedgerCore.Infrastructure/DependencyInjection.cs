using LedgerCore.Application.Common.Interfaces.Authentication;
using LedgerCore.Infrastructure.Authentication;
using LedgerCore.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace LedgerCore.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            services.AddScoped<IPasswordHasher, PasswordHasher>();
            services.AddScoped<DatabaseInitializer>();
            return services;
        }

    }
}
