using KTZH.Models;

namespace KTZH.Services;

/// <summary>
/// Движок расчёта индекса здоровья локомотива (0–100, грейд A–E).
/// Прозрачная логика: каждый компонент нормируется линейно между Warning и Critical.
/// </summary>
public static class HealthScoreEngine
{
    /// <summary>
    /// Рассчитать Health Score для снимка телеметрии
    /// </summary>
    public static HealthScore Calculate(TelemetrySnapshot snapshot)
    {
        var components = snapshot.LocomotiveType == LocomotiveType.TE33A
            ? CalculateTE33A(snapshot)
            : CalculateKZ8A(snapshot);

        var weightedSum = components.Sum(c => c.Score * c.Weight);
        var totalWeight = components.Sum(c => c.Weight);
        var score = (int)Math.Round(Math.Clamp(weightedSum / totalWeight, 0, 100));
        var grade = ScoreToGrade(score);
        var alerts = BuildAlerts(snapshot, components);

        return new HealthScore
        {
            LocomotiveId = snapshot.LocomotiveId,
            Score = score,
            Grade = grade,
            ComponentScores = components.ToDictionary(c => c.Name, c => (int)Math.Round(c.Score)),
            ActiveAlerts = alerts,
            CalculatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Преобразовать балл в грейд
    /// </summary>
    public static HealthGrade ScoreToGrade(int score) => score switch
    {
        >= 90 => HealthGrade.A,
        >= 75 => HealthGrade.B,
        >= 60 => HealthGrade.C,
        >= 40 => HealthGrade.D,
        _     => HealthGrade.E
    };

    /// <summary>
    /// Сформировать список алертов на русском языке
    /// </summary>
    public static List<string> BuildAlerts(TelemetrySnapshot snapshot, List<ComponentScore> components)
    {
        var alerts = new List<string>();

        foreach (var c in components)
        {
            if (c.Score < 30)
                alerts.Add($"КРИТИЧНО: {c.Name} = {c.RawValue:F1} {c.Unit} (критический порог)");
            else if (c.Score < 60)
                alerts.Add($"ВНИМАНИЕ: {c.Name} = {c.RawValue:F1} {c.Unit} (предупреждение)");
        }

        // Скорость — отдельная проверка
        if (snapshot.Speed > 120)
            alerts.Add($"КРИТИЧНО: Превышение скорости = {snapshot.Speed:F0} км/ч (макс. 120)");

        return alerts;
    }

    // ── ТЭ33А (тепловоз) ──

    private static List<ComponentScore> CalculateTE33A(TelemetrySnapshot s)
    {
        return new List<ComponentScore>
        {
            // Температура масла: норма 45–85, warning 85–95, critical >95
            Normalize("Температура масла", "°C", s.OilTemperature ?? 65, 85, 95, 0.20, ascending: true),

            // Температура ОЖ: норма 70–90, warning 90–100, critical >105
            Normalize("Температура ОЖ", "°C", s.CoolantTemperature ?? 80, 90, 105, 0.15, ascending: true),

            // Давление масла: норма 0.49–1.03, warning 0.30–0.49, critical <0.30 (инвертировано)
            Normalize("Давление масла", "МПа", s.OilPressure ?? 0.75, 0.49, 0.30, 0.20, ascending: false),

            // Давление тормозной: норма 0.50–0.90, warning 0.35–0.50, critical <0.35 (инвертировано)
            Normalize("Давление тормозной", "МПа", s.BrakePressure, 0.50, 0.35, 0.10, ascending: false),

            // Уровень топлива: норма 20–100, warning 10–20, critical <10 (инвертировано)
            Normalize("Уровень топлива", "%", s.FuelLevel ?? 80, 20, 10, 0.15, ascending: false),

            // Обороты дизеля: норма 320–1050, critical >1100
            Normalize("Обороты дизеля", "об/мин", s.DieselRpm ?? 700, 1050, 1100, 0.10, ascending: true),

            // Ток ТЭД: норма 0–900, warning 900–1000, critical >1000
            Normalize("Ток ТЭД", "А", s.TractionMotorCurrent, 900, 1000, 0.10, ascending: true),
        };
    }

    // ── KZ8A (электровоз) ──

    private static List<ComponentScore> CalculateKZ8A(TelemetrySnapshot s)
    {
        return new List<ComponentScore>
        {
            // Напряжение КС: норма 22–28, warning 18–22, critical <18 (инвертировано)
            Normalize("Напряжение КС", "кВ", s.CatenaryVoltage ?? 25, 22, 18, 0.25, ascending: false),

            // Температура трансформатора: норма 40–80, warning 80–90, critical >95
            Normalize("Температура трансформатора", "°C", s.TransformerTemperature ?? 60, 80, 95, 0.20, ascending: true),

            // Температура ТЭД: норма 0–80, warning 80–95, critical >100
            Normalize("Температура ТЭД", "°C", s.TractionMotorTemperature ?? 50, 80, 100, 0.15, ascending: true),

            // Давление тормозной: норма 0.50–0.90, warning 0.35–0.50, critical <0.35 (инвертировано)
            Normalize("Давление тормозной", "МПа", s.BrakePressure, 0.50, 0.35, 0.15, ascending: false),

            // Ток ТЭД: норма 0–1200, warning 1200–1400, critical >1400
            Normalize("Ток ТЭД", "А", s.TractionMotorCurrent, 1200, 1400, 0.15, ascending: true),

            // Коды ошибок — заглушка, всегда 100 (нет ошибок)
            new ComponentScore("Коды ошибок", "", 0, 100, 0.10),
        };
    }

    /// <summary>
    /// Нормализация значения в балл 0–100 линейно между Warning и Critical порогами.
    /// ascending=true: значение растёт → хуже (температура, обороты).
    /// ascending=false: значение падает → хуже (давление, топливо, напряжение).
    /// </summary>
    private static ComponentScore Normalize(
        string name, string unit, double value,
        double warningThreshold, double criticalThreshold,
        double weight, bool ascending)
    {
        double score;

        if (ascending)
        {
            // Чем выше значение, тем хуже
            if (value <= warningThreshold)
                score = 100;
            else if (value >= criticalThreshold)
                score = 0;
            else
                score = 100.0 * (criticalThreshold - value) / (criticalThreshold - warningThreshold);
        }
        else
        {
            // Чем ниже значение, тем хуже
            if (value >= warningThreshold)
                score = 100;
            else if (value <= criticalThreshold)
                score = 0;
            else
                score = 100.0 * (value - criticalThreshold) / (warningThreshold - criticalThreshold);
        }

        score = Math.Clamp(score, 0, 100);
        return new ComponentScore(name, unit, value, score, weight);
    }

    /// <summary>Компонент оценки здоровья</summary>
    public record ComponentScore(string Name, string Unit, double RawValue, double Score, double Weight);
}