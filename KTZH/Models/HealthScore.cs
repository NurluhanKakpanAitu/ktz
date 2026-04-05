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

    /// <summary>Топ-3 параметра с наименьшим вкладом (отсортированы по Score ASC)</summary>
    public List<HealthFactor> TopWorstFactors { get; set; } = new();
}

/// <summary>
/// Фактор Health Score — отдельный параметр с его вкладом в общий балл.
/// </summary>
public class HealthFactor
{
    /// <summary>Название параметра (на русском)</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>Балл параметра 0–100</summary>
    public double Score { get; set; }

    /// <summary>Текущее сырое значение</summary>
    public double CurrentValue { get; set; }

    /// <summary>Единица измерения</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>"above" — плохо когда выше; "below" — плохо когда ниже</summary>
    public string Direction { get; set; } = "above";
}