using Microsoft.Extensions.DependencyInjection;
using AutoMapper;
using System.Reflection;

namespace LedgerCore.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg => { },Assembly.GetExecutingAssembly());
            return services;
        } 

    }
}
