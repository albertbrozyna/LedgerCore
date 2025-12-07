using Carter;
using Microsoft.EntityFrameworkCore;
using LedgerCore.Application.Common.Interfaces;
var builder = WebApplication.CreateBuilder(args);


/// Adding carter to project
builder.Services.AddCarter();

// This program is the main starting point for app
// Add services to the container.

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();



var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

void DatabaseConfig(DbContextOptionsBuilder options)
{
    options.UseNpgsql(connectionString);
}

// Rejestrujemy bazê danych w systemie.
// Mówimy: "U¿ywaj Postgresa i weŸ has³o z pliku appsettings.json"
builder.Services.AddDbContext<LedgerCore.Infrastructure.Persistence.LedgerDbContext>(DatabaseConfig);

// To dodaj koniecznie:
builder.Services.AddScoped<IAppDbContext>(provider =>provider.GetRequiredService<LedgerCore.Infrastructure.Persistence.LedgerDbContext>());

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(LedgerCore.Application.Common.Interfaces.IAppDbContext).Assembly));

var app = builder.Build();

// Map endpoints from Carter modules
app.MapCarter();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Alternative way to register DbContext
//builder.Services.AddDbContext<LedgerCore.Infrastructure.Persistence.LedgerDbContext>((options =>
//    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))));

app.UseHttpsRedirection();

//app.UseAuthorization();


app.Run();
