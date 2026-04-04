namespace KTZH.Models;

/// <summary>
/// Грейд состояния локомотива
/// </summary>
public enum HealthGrade
{
    /// <summary>90–100: Отличное состояние</summary>
    A,
    /// <summary>75–89: Хорошее, плановое ТО</summary>
    B,
    /// <summary>60–74: Требует внимания</summary>
    C,
    /// <summary>40–59: Повышенный риск</summary>
    D,
    /// <summary>0–39: Критическое, немедленная остановка</summary>
    E
}

/// <summary>
/// Индекс здоровья локомотива (0–100)
/// </summary>
public class HealthScore
{
    /// <summary>ID локомотива</summary>
    public string LocomotiveId { get; set; } = string.Empty;

    /// <summary>Общий балл 0–100</summary>
    public int Score { get; set; }

    /// <summary>Грейд A/B/C/D/E</summary>
    public HealthGrade Grade { get; set; }

    /// <summary>Покомпонентные оценки (название → балл 0–100)</summary>
    public Dictionary<string, int> ComponentScores { get; set; } = new();

    /// <summary>Активные алерты (человекочитаемые сообщения)</summary>
    public List<string> ActiveAlerts { get; set; } = new();

    /// <summary>Метка времени расчёта</summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}