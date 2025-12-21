using Asp.Versioning;
using LedgerCore.Api.Infrastructure;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace LedgerCore.Api.Common.Extensions
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
        {

            var jwtOptions = configuration.GetSection("jwt").Get<JwtOptions>();

            // Authentication
            services.AddAuthorization();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.Zero

                };

            });



            return services;
        }

        public static IServiceCollection AddVersioning(this IServiceCollection services, IConfiguration configuration)
        {

            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1);
                // Return to users a api available api version in headers
                options.ReportApiVersions = true;
                // Use default version when it is unspecified
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();

            }).AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV"; // Format: v1
                options.SubstituteApiVersionInUrl = true; // Podmienia {version} w URL Swaggera na konkretny numer
            });
            return services;
        }

        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IUserDetails, CurrentUserDetails>();
            services.AddExceptionHandler<ValidationExceptionHandler>();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails(configure => {
                configure.CustomizeProblemDetails = options => {
                    options.ProblemDetails.Extensions.TryAdd("requestId", options.HttpContext.TraceIdentifier);
                };
            });
            return services;
        }

        public static IServiceCollection AddCustomTimeouts(this IServiceCollection services)
        {
            services.AddRequestTimeouts(options =>
            {
                // Ustawiamy domyślną politykę dla całej aplikacji
                options.DefaultPolicy = new RequestTimeoutPolicy
                {
                    Timeout = TimeSpan.FromSeconds(240),
                    WriteTimeoutResponse = async (context) =>
                    {
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync("{\"error\": \"Przekroczono czas oczekiwania (Timeout).\"}");
                    }
                };

                // Możesz też dodać nazwane polityki dla konkretnych endpointów
                options.AddPolicy("LongRunning", TimeSpan.FromMinutes(5));
            });
            return services;
        }

        public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("Frontend", builder =>
                {
                    builder.WithOrigins("https://localhost:3000");
                    builder.AllowAnyHeader();
                    builder.AllowAnyMethod();
                    builder.AllowCredentials();
                });
            });

            return services;
        }

    }
}
