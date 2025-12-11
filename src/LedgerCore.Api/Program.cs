using Asp.Versioning;
using Carter;
using FluentValidation;
using LedgerCore.Api.Infrastructure;
using LedgerCore.Application;
using LedgerCore.Application.Common.Behaviors;
using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Infrastructure;
using LedgerCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddApiVersioning(options =>
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
}
    );

/// Adding carter to project
builder.Services.AddCarter();

// This program is the main starting point for app
// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(configure =>
{
    configure.CustomizeProblemDetails = options =>
    {
        options.ProblemDetails.Extensions.TryAdd("requestId", options.HttpContext.TraceIdentifier); // Add request id to problem details
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.ToString().Replace("+", "."));
});


var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

void DatabaseConfig(DbContextOptionsBuilder options)
{
    options.UseNpgsql(connectionString);
}

// Rejestrujemy bazê danych w systemie.
// Mówimy: "U¿ywaj Postgresa i weŸ has³o z pliku appsettings.json"
builder.Services.AddDbContext<LedgerDbContext>(DatabaseConfig);

// To dodaj koniecznie:
builder.Services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<LedgerDbContext>());


// 1. Rejestracja FluentValidation
// Skanuje projekt Application i znajduje wszystkie klasy dziedzicz¹ce po AbstractValidator
builder.Services.AddValidatorsFromAssembly(typeof(LedgerCore.Application.Common.Interfaces.IAppDbContext).Assembly);

builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LedgerCore.Application.Common.Interfaces.IAppDbContext).Assembly);

    // Dodajemy nasze zachowanie do ruroci¹gu
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
}

    );



// 1. REJESTRACJA US£UGI TIMEOUTÓW
builder.Services.AddRequestTimeouts(options =>
{
    // Ustawiamy domyœln¹ politykê dla ca³ej aplikacji
    options.DefaultPolicy = new RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(240),
        WriteTimeoutResponse = async (context) =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\": \"Przekroczono czas oczekiwania (Timeout).\"}");
        }
    };

    // Mo¿esz te¿ dodaæ nazwane polityki dla konkretnych endpointów
    options.AddPolicy("LongRunning", TimeSpan.FromMinutes(5));
});

var app = builder.Build();

// Check if database has started
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Opcjonalnie: Dodaje wygodne menu wyboru wersji w Swagger UI
        // (Jeœli bêdziesz mia³ v1 i v2, pojawi¹ siê na liœcie)
        var descriptions = app.DescribeApiVersions();
        foreach (var description in descriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
    });
}

// Map endpoints from Carter modules
app.MapCarter();


app.UseRequestTimeouts();


await app.RunAsync();
