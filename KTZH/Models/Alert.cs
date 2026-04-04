namespace KTZH.Models;

/// <summary>
/// Уровень серьёзности алерта
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <summary>
/// Алерт при пересечении порогового значения параметра
/// </summary>
public class Alert
{
    /// <summary>Уникальный идентификатор алерта</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>ID локомотива</summary>
    public string LocomotiveId { get; set; } = string.Empty;

    /// <summary>Серьёзность: Info, Warning, Critical</summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>Название параметра (напр. "Температура масла")</summary>
    public string Parameter { get; set; } = string.Empty;

    /// <summary>Человекочитаемое сообщение на русском</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Текущее значение параметра</summary>
    public double Value { get; set; }

    /// <summary>Время срабатывания</summary>
    public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;

    /// <summary>Активен ли алерт</summary>
    public bool IsActive { get; set; } = true;
}