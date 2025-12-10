using Carter;
using FluentValidation;
using LedgerCore.Api.Infrastructure;
using LedgerCore.Application.Common.Behaviors;
using LedgerCore.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using LedgerCore.Infrastructure;
using LedgerCore.Infrastructure.Persistence;


var builder = WebApplication.CreateBuilder(args);


/// Adding carter to project
builder.Services.AddCarter();

// This program is the main starting point for app
// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

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
    app.UseSwaggerUI();
}

// Map endpoints from Carter modules
app.MapCarter();





await app.RunAsync();
