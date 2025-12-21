using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace LedgerCore.Api.Common.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(o =>
        {
            // Opcjonalne: Poprawia wyświetlanie nazw schematów w Swaggerze
            o.CustomSchemaIds(id => id.FullName!.Replace('+', '-'));

            // 1. Definicja zabezpieczenia (Security Definition)
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "JWT Authentication",
                Description = "Wprowadź swój token JWT w polu poniżej. Przykład: 'eyJhbGciOiJIUzI1...'",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = JwtBearerDefaults.AuthenticationScheme, // "bearer"
                BearerFormat = "JWT"
            };
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "LedgerCore API", Version = "v1" });
            o.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);

            // 2. Wymagania zabezpieczeń (Security Requirement)
            // Mówi Swaggerowi, aby używał powyższej definicji globalnie
            var securityRequirement = new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtBearerDefaults.AuthenticationScheme
                        }
                    },
                    Array.Empty<string>()
                }
            };

            o.AddSecurityRequirement(securityRequirement);
        });

        return services;
    }
}