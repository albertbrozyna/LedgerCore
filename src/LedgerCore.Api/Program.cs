using Carter;
using LedgerCore.Api.Common.Extensions;
using LedgerCore.Application.Extensions;
using LedgerCore.Infrastructure.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCarter().AddPersistence(builder.Configuration).AddInfrastructure(builder.Configuration)
    .AddIdentity(builder.Configuration)
    .AddAuth(builder.Configuration).AddVersioning(builder.Configuration).AddSwagger()
    .AddApplication(builder.Configuration)
    .AddCustomTimeouts()
    .AddPresentation()
    .AddCorsPolicy()
    .AddInitalizer();



var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    await app.UseDatabaseInitializer();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var descriptions = app.DescribeApiVersions();
        foreach (var description in descriptions)
        {
            options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
        }
    });
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestTimeouts();
app.MapCarter();


await app.RunAsync();
