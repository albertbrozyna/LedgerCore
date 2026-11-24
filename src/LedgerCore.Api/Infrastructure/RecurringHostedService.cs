using LedgerCore.Application.Common.Interfaces;
using LedgerCore.Application.Features.Invoicing;
using LedgerCore.Infrastructure.Persistence.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Api.Infrastructure;

/// <summary>
/// Opcjonalne generowanie zaległych faktur cyklicznych przy starcie aplikacji dla wszystkich aktywnych firm
/// (<c>RecurringInvoices:GenerateOnStartup</c>, domyślnie wyłączone – faktury powstają wtedy bez udziału człowieka, wpis w audycie: „system”).
/// </summary>
public class RecurringHostedService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<RecurringHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuration.GetValue("RecurringInvoices:GenerateOnStartup", false)) return;
        try { await Task.Delay(TimeSpan.FromSeconds(configuration.GetValue("RecurringInvoices:StartupDelaySeconds", 15)), ct); } catch (OperationCanceledException) { return; }

        List<Guid> companies;
        using (var scope = scopes.CreateScope())
            companies = await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Companies.AsNoTracking().Where(c => c.IsActive).Select(c => c.Id).ToListAsync(ct);

        foreach (var id in companies)
        {
            try
            {
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ICompanyContext>().CompanyId = id;
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RecurringInvoices.GenerateCommand(null), ct);
                logger.LogInformation("Faktury cykliczne (firma {Company}): wygenerowano {Count}, pominięto {Skipped}", id, result.Generated.Count, result.Skipped.Count);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Generowanie faktur cyklicznych nie powiodło się (firma {Company})", id);
            }
        }
    }
}
