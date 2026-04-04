using KTZH.Data;
using Microsoft.EntityFrameworkCore;

namespace KTZH.Services;

/// <summary>
/// Фоновый сервис: каждый час удаляет записи телеметрии старше 24 часов
/// </summary>
public class HistoryCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HistoryCleanupService> _logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24);
    private static readonly TimeSpan AlertRetentionPeriod = TimeSpan.FromHours(48);

    public HistoryCleanupService(IServiceScopeFactory scopeFactory, ILogger<HistoryCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HistoryCleanupService запущен, интервал: {Interval}", CleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CleanupInterval, stoppingToken);

            try
            {
                await CleanupOldRecords(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке истории телеметрии");
            }
        }
    }

    private async Task CleanupOldRecords(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Удалить телеметрию старше 24 часов
        var telemetryCutoff = DateTime.UtcNow - RetentionPeriod;
        var deletedTelemetry = await db.TelemetryHistory
            .Where(h => h.Timestamp < telemetryCutoff)
            .ExecuteDeleteAsync(ct);

        if (deletedTelemetry > 0)
            _logger.LogInformation("Очистка телеметрии: удалено {Count} записей старше {Cutoff}", deletedTelemetry, telemetryCutoff);

        // Удалить неактивные алерты старше 48 часов
        var alertCutoff = DateTime.UtcNow - AlertRetentionPeriod;
        var deletedAlerts = await db.Alerts
            .Where(a => !a.IsActive && a.TriggeredAt < alertCutoff)
            .ExecuteDeleteAsync(ct);

        if (deletedAlerts > 0)
            _logger.LogInformation("Очистка алертов: удалено {Count} неактивных записей старше {Cutoff}", deletedAlerts, alertCutoff);
    }
}