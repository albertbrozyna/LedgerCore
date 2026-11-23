using System.Text.RegularExpressions;
using LedgerCore.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace LedgerCore.Api.Infrastructure;

/// <summary>Tworzenie i rotacja kopii zapasowych bazy (SQLite VACUUM INTO – spójna kopia także podczas pracy aplikacji).</summary>
public class BackupManager(IConfiguration configuration, IServiceScopeFactory scopes, ILogger<BackupManager> logger)
{
    public static readonly Regex NamePattern = new(@"^ledgercore-\d{4}-\d{2}-\d{2}-\d{6}(-manual)?\.db$", RegexOptions.Compiled);

    public string Directory_ => Path.Combine(configuration["DataDirectory"]!, "backups");

    public async Task<string> CreateAsync(bool manual, CancellationToken ct)
    {
        Directory.CreateDirectory(Directory_);
        var name = $"ledgercore-{DateTime.Now:yyyy-MM-dd-HHmmss}{(manual ? "-manual" : "")}.db";
        var path = Path.Combine(Directory_, name);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
        // ścieżka jest budowana wyłącznie z naszych stałych i daty – bez danych od użytkownika
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{path.Replace("'", "''")}'", ct);
        logger.LogInformation("Utworzono kopię zapasową {Name}", name);
        Rotate();
        return name;
    }

    public List<(string Name, long Size, DateTime Created)> List() =>
        !Directory.Exists(Directory_) ? [] : new DirectoryInfo(Directory_).GetFiles("*.db")
            .Where(f => NamePattern.IsMatch(f.Name)).OrderByDescending(f => f.Name)
            .Select(f => (f.Name, f.Length, f.CreationTime)).ToList();

    /// <summary>Zostawia najnowsze N kopii automatycznych (kopie ręczne nie są kasowane).</summary>
    void Rotate()
    {
        var keep = configuration.GetValue("Backup:KeepAutomatic", 14);
        foreach (var old in List().Where(b => !b.Name.EndsWith("-manual.db")).Skip(keep))
        {
            try { File.Delete(Path.Combine(Directory_, old.Name)); } catch (IOException e) { logger.LogWarning(e, "Nie udało się usunąć starej kopii {Name}", old.Name); }
        }
    }

    public bool AutomaticDoneToday() =>
        List().Any(b => !b.Name.EndsWith("-manual.db") && b.Name.StartsWith($"ledgercore-{DateTime.Now:yyyy-MM-dd}-"));
}

/// <summary>Codzienna automatyczna kopia: pierwsza próba kilka sekund po starcie, potem sprawdzanie co godzinę.</summary>
public class BackupHostedService(BackupManager manager, IConfiguration configuration, ILogger<BackupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuration.GetValue("Backup:Enabled", true)) return;
        try { await Task.Delay(TimeSpan.FromSeconds(configuration.GetValue("Backup:StartupDelaySeconds", 20)), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try { if (!manager.AutomaticDoneToday()) await manager.CreateAsync(false, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { logger.LogError(e, "Automatyczna kopia zapasowa nie powiodła się"); }
            try { await Task.Delay(TimeSpan.FromHours(1), ct); } catch (OperationCanceledException) { return; }
        }
    }
}
