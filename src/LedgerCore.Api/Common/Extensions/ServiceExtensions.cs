using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using LedgerCore.Api.Infrastructure;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Common.Settings;
using LedgerCore.Domain.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.IdentityModel.Tokens;

namespace LedgerCore.Api.Common.Extensions
{
    public static class Policies
    {
        public const string AdminOnly = "AdminOnly";
        /// <summary>Księgowanie i edycja planu kont: Admin lub Accountant (Viewer ma tylko odczyt).</summary>
        public const string CanWrite = "CanWrite";
    }

    public static class ServiceExtensions
    {
        public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>()
                             ?? throw new InvalidOperationException("Brak sekcji Jwt w konfiguracji");

            services.AddAuthorization(o =>
            {
                // Domyślnie każdy endpoint wymaga zalogowania; publiczne oznaczamy .AllowAnonymous()
                o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                o.AddPolicy(Policies.AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)));
                o.AddPolicy(Policies.CanWrite, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.Accountant)));
            });

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
            {
                o.MapInboundClaims = false; // nie zamieniaj nazw claimów na długie nazwy XML
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.Zero
                };
            });

            return services;
        }

        public static IServiceCollection AddVersioning(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1);
                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            }).AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
            return services;
        }

        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.AddScoped<IUserDetails, CurrentUserDetails>();
            services.AddExceptionHandler<ValidationExceptionHandler>();
            services.AddExceptionHandler<GlobalExceptionHandler>();
            services.AddProblemDetails(configure =>
            {
                configure.CustomizeProblemDetails = options =>
                {
                    options.ProblemDetails.Extensions.TryAdd("requestId", options.HttpContext.TraceIdentifier);
                };
            });
            // Enumy jako tekst ("Asset", "Debit"); przy odczycie akceptowane są też liczby
            services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
            return services;
        }

        public static IServiceCollection AddCustomTimeouts(this IServiceCollection services)
        {
            services.AddRequestTimeouts(options =>
            {
                options.DefaultPolicy = new RequestTimeoutPolicy { Timeout = TimeSpan.FromSeconds(240) };
            });
            return services;
        }

        public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
        {
            // Tylko dla trybu deweloperskiego (vite dev server). W produkcji UI jest serwowane z tego samego adresu.
            services.AddCors(options => options.AddPolicy("Frontend", builder =>
                builder.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
            return services;
        }
    }
}
