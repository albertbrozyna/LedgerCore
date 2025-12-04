using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// This program is the main starting point for app
// Add services to the container.

builder.Services.AddControllers();
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

var app = builder.Build();

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

app.UseAuthorization();

app.MapControllers();

app.Run();
