using AutoMapper;
using FluentValidation;
using LedgerCore.Application.Common.Behaviors;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace LedgerCore.Application.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services,IConfiguration configuration)
        {

            services.AddValidatorsFromAssembly(typeof(IAppDbContext).Assembly);

            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(LedgerCore.Application.Common.Interfaces.IAppDbContext).Assembly);

                cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
            }

                );

            // Filestorage settings
            services.Configure<FileStorageSettings>(configuration.GetSection("FileStorageSettings"));


            services.AddAutoMapper(cfg => { },Assembly.GetExecutingAssembly());
            return services;
        } 

    }
}
