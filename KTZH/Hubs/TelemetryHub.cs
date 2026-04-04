using Microsoft.AspNetCore.SignalR;

namespace KTZH.Hubs;

/// <summary>
/// SignalR Hub для передачи телеметрии локомотивов в реальном времени.
/// Endpoint: /hubs/telemetry
///
/// Группы:
///   "fleet"       — диспетчерский вид (все 10 локомотивов)
///   "loco-{id}"   — детальный вид конкретного локомотива
///
/// Клиентские методы:
///   "ReceiveTelemetry" — TelemetrySnapshot одного локомотива
///   "ReceiveFleet"     — List&lt;LocomotiveState&gt; всего парка (каждые 5 сек)
///   "ReceiveAlert"     — Alert при пересечении порога
/// </summary>
public class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// При подключении: клиент с query ?locomotiveId=X попадает в группу loco-X,
    /// иначе — в группу fleet (диспетчерский вид).
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var locomotiveId = httpContext?.Request.Query["locomotiveId"].FirstOrDefault();

        if (!string.IsNullOrEmpty(locomotiveId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"loco-{locomotiveId}");
            _logger.LogInformation("Клиент {ConnId} подключён к loco-{LocoId}", Context.ConnectionId, locomotiveId);
        }
        else
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "fleet");
            _logger.LogInformation("Клиент {ConnId} подключён к fleet", Context.ConnectionId);
        }

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Клиент запрашивает переключение на группу конкретного локомотива
    /// </summary>
    public async Task JoinLocomotiveGroup(string locomotiveId)
    {
        // Убрать из fleet
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "fleet");

        // Добавить в группу локомотива
        await Groups.AddToGroupAsync(Context.ConnectionId, $"loco-{locomotiveId}");
        _logger.LogInformation("Клиент {ConnId} переключился на loco-{LocoId}", Context.ConnectionId, locomotiveId);
    }

    /// <summary>
    /// Клиент возвращается в диспетчерский вид
    /// </summary>
    public async Task JoinFleetGroup(string? locomotiveId = null)
    {
        if (!string.IsNullOrEmpty(locomotiveId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"loco-{locomotiveId}");

        await Groups.AddToGroupAsync(Context.ConnectionId, "fleet");
        _logger.LogInformation("Клиент {ConnId} вернулся в fleet", Context.ConnectionId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Клиент {ConnId} отключён", Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}