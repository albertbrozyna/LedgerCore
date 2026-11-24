using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Extensions;
using LedgerCore.Infrastructure.Extensions;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// --- Katalog danych: baza SQLite, klucz JWT, wgrane pliki. Wszystko, co trzeba skopiować, żeby zrobić backup. ---
if (string.IsNullOrWhiteSpace(builder.Configuration["urls"]) && string.IsNullOrWhiteSpace(builder.Configuration["http_ports"]))
    builder.WebHost.UseUrls("http://localhost:5000");

var dataDir = builder.Configuration["DataDirectory"];
if (string.IsNullOrWhiteSpace(dataDir))
    dataDir = Environment.GetEnvironmentVariable("LEDGERCORE_DATA_DIR");
if (string.IsNullOrWhiteSpace(dataDir))
    dataDir = Path.Combine(AppContext.BaseDirectory, "data");
dataDir = Path.GetFullPath(dataDir);
Directory.CreateDirectory(dataDir);
builder.Configuration["DataDirectory"] = dataDir;
builder.Configuration["ConnectionStrings:DefaultConnection"] = $"Data Source={Path.Combine(dataDir, "ledgercore.db")}";

// Klucz JWT generowany raz przy pierwszym uruchomieniu (każda instalacja ma własny)
if (string.IsNullOrWhiteSpace(builder.Configuration["Jwt:SecretKey"]))
{
    var keyFile = Path.Combine(dataDir, "jwt.key");
    if (!File.Exists(keyFile))
        File.WriteAllText(keyFile, Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
    builder.Configuration["Jwt:SecretKey"] = File.ReadAllText(keyFile).Trim();
}

// klucze szyfrowania sekretów (np. hasło SMTP) leżą w folderze danych, obok bazy
builder.Services.AddDataProtection().SetApplicationName("LedgerCore").PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));
builder.Services.AddCarter().AddPersistence(builder.Configuration).AddInfrastructure(builder.Configuration)
    .AddIdentity(builder.Configuration)
    .AddAuth(builder.Configuration).AddVersioning(builder.Configuration).AddSwagger()
    .AddApplication(builder.Configuration)
    .AddCustomTimeouts()
    .AddPresentation()
    .AddCorsPolicy()
    .AddInitalizer();

builder.Services.AddSingleton<LedgerCore.Api.Infrastructure.BackupManager>();
builder.Services.AddHostedService<LedgerCore.Api.Infrastructure.BackupHostedService>();
builder.Services.AddHostedService<LedgerCore.Api.Infrastructure.RecurringHostedService>();

var app = builder.Build();

app.UseExceptionHandler();
await app.UseDatabaseInitializer();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in app.DescribeApiVersions())
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
    });
}

// Interfejs (zbudowany React) serwowany z wwwroot – cała aplikacja działa pod jednym adresem
app.UseDefaultFiles();
app.UseStaticFiles();
var uploads = Path.Combine(dataDir, "uploads");
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(uploads), RequestPath = "/uploads" });

app.UseCors("Frontend");
app.UseAuthentication();
app.UseMiddleware<LedgerCore.Api.Infrastructure.CompanyContextMiddleware>();
app.UseAuthorization();
app.UseRequestTimeouts();
app.MapCarter();

// SPA: nieznane ścieżki (poza /api) zwracają index.html, routing robi React
app.MapFallback(async ctx =>
{
    var index = Path.Combine(app.Environment.WebRootPath ?? "", "index.html");
    if (ctx.Request.Path.StartsWithSegments("/api") || !File.Exists(index))
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    ctx.Response.ContentType = "text/html; charset=utf-8";
    await ctx.Response.SendFileAsync(index);
}).AllowAnonymous();

await app.RunAsync();

public partial class Program;
