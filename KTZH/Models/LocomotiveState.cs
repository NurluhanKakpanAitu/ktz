namespace KTZH.Models;

/// <summary>
/// Полное текущее состояние локомотива (in-memory, обновляется каждую секунду)
/// </summary>
public class LocomotiveState
{
    /// <summary>Статические данные локомотива</summary>
    public Locomotive Locomotive { get; set; } = new();

    /// <summary>Последний снимок телеметрии</summary>
    public TelemetrySnapshot LastTelemetry { get; set; } = new();

    /// <summary>Текущий Health Score</summary>
    public HealthScore LastHealth { get; set; } = new();
}