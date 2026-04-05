using KTZH.Configuration;
using KTZH.Models;
using Microsoft.Extensions.Options;

namespace KTZH.Services;

/// <summary>
/// Движок расчёта индекса здоровья локомотива (0–100, грейд A–E).
/// Прозрачная логика: каждый компонент нормируется линейно между Warning и Critical.
/// Пороги загружаются из <see cref="ThresholdConfig"/> (appsettings.json).
/// </summary>
public class HealthScoreEngine
{
    private readonly ThresholdConfig _thresholds;

    public HealthScoreEngine(IOptions<ThresholdConfig> thresholds)
    {
        _thresholds = thresholds.Value;
    }

    /// <summary>
    /// Рассчитать Health Score для снимка телеметрии
    /// </summary>
    public HealthScore Calculate(TelemetrySnapshot snapshot)
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
            else if (c.Score < 80)
                alerts.Add($"ВНИМАНИЕ: {c.Name} = {c.RawValue:F1} {c.Unit} (предупреждение)");
        }

        // Скорость — отдельная проверка
        if (snapshot.Speed > 120)
            alerts.Add($"КРИТИЧНО: Превышение скорости = {snapshot.Speed:F0} км/ч (макс. 120)");

        return alerts;
    }

    // ── ТЭ33А (тепловоз) ──

    private List<ComponentScore> CalculateTE33A(TelemetrySnapshot s)
    {
        var t = _thresholds.TE33A;
        return new List<ComponentScore>
        {
            // Температура масла
            NormalizeFromThreshold("Температура масла", "°C", s.OilTemperature ?? 65, t.OilTemperature, 0.20, normalBound: 45),

            // Температура ОЖ
            NormalizeFromThreshold("Температура ОЖ", "°C", s.CoolantTemperature ?? 80, t.CoolantTemperature, 0.15, normalBound: 70),

            // Давление масла (below)
            NormalizeFromThreshold("Давление масла", "МПа", s.OilPressure ?? 0.75, t.OilPressure, 0.20, normalBound: 1.03),

            // Давление тормозной (below) — берём из appsettings
            NormalizeFromThreshold("Давление тормозной", "МПа", s.BrakePressure, t.BrakeLinePressure, 0.10, normalBound: 0.90),

            // Уровень топлива (below)
            NormalizeFromThreshold("Уровень топлива", "%", s.FuelLevel ?? 80, t.FuelTank, 0.15, normalBound: 100),

            // Обороты дизеля
            NormalizeFromThreshold("Обороты дизеля", "об/мин", s.DieselRpm ?? 700, t.EngineRpm, 0.10, normalBound: 320),

            // Ток ТЭД — нет в конфиге, оставляем хардкод
            Normalize("Ток ТЭД", "А", s.TractionMotorCurrent, 900, 1000, 0.10, ascending: true, normalMin: 0),
        };
    }

    // ── KZ8A (электровоз) ──

    private List<ComponentScore> CalculateKZ8A(TelemetrySnapshot s)
    {
        var t = _thresholds.KZ8A;
        return new List<ComponentScore>
        {
            // Напряжение КС (below)
            NormalizeFromThreshold("Напряжение КС", "кВ", s.CatenaryVoltage ?? 25, t.ContactVoltage, 0.25, normalBound: 28),

            // Температура трансформатора
            NormalizeFromThreshold("Температура трансформатора", "°C", s.TransformerTemperature ?? 60, t.TransformerTemp, 0.20, normalBound: 40),

            // Температура ТЭД
            NormalizeFromThreshold("Температура ТЭД", "°C", s.TractionMotorTemperature ?? 50, t.TractionMotorTemp, 0.15, normalBound: 0),

            // Температура IGBT
            NormalizeFromThreshold("Температура IGBT", "°C", s.IgbtTemperature ?? 40, t.IgbtTemp, 0.10, normalBound: 20),

            // Давление тормозной — нет в KZ8A конфиге, используем общий хардкод
            Normalize("Давление тормозной", "МПа", s.BrakePressure, 0.50, 0.35, 0.15, ascending: false, normalMin: 0.90),

            // Ток ТЭД — нет в конфиге
            Normalize("Ток ТЭД", "А", s.TractionMotorCurrent, 1200, 1400, 0.15, ascending: true, normalMin: 0),
        };
    }

    /// <summary>Нормализация с порогом из конфига</summary>
    private static ComponentScore NormalizeFromThreshold(
        string name, string unit, double value, Threshold threshold, double weight, double normalBound)
    {
        var ascending = threshold.Direction.Equals("above", StringComparison.OrdinalIgnoreCase);
        return Normalize(name, unit, value, threshold.Warning, threshold.Critical, weight, ascending, normalBound);
    }

    /// <summary>
    /// Нормализация значения в балл 0–100 с плавной градацией по всему диапазону.
    /// ascending=true: значение растёт → хуже (температура, обороты).
    /// ascending=false: значение падает → хуже (давление, топливо, напряжение).
    /// </summary>
    private static ComponentScore Normalize(
        string name, string unit, double value,
        double warningThreshold, double criticalThreshold,
        double weight, bool ascending,
        double normalMin = double.NaN)
    {
        double score;

        if (ascending)
        {
            var safeMin = double.IsNaN(normalMin) ? warningThreshold * 0.5 : normalMin;

            if (value >= criticalThreshold)
                score = 0;
            else if (value >= warningThreshold)
                score = 80.0 - 50.0 * (value - warningThreshold) / (criticalThreshold - warningThreshold);
            else if (value <= safeMin)
                score = 100;
            else
                score = 100.0 - 20.0 * (value - safeMin) / (warningThreshold - safeMin);
        }
        else
        {
            var safeMax = double.IsNaN(normalMin) ? warningThreshold * 1.5 : normalMin;

            if (value <= criticalThreshold)
                score = 0;
            else if (value <= warningThreshold)
                score = 80.0 - 50.0 * (warningThreshold - value) / (warningThreshold - criticalThreshold);
            else if (value >= safeMax)
                score = 100;
            else
                score = 100.0 - 20.0 * (safeMax - value) / (safeMax - warningThreshold);
        }

        score = Math.Clamp(score, 0, 100);
        return new ComponentScore(name, unit, value, score, weight);
    }

    /// <summary>Компонент оценки здоровья</summary>
    public record ComponentScore(string Name, string Unit, double RawValue, double Score, double Weight);
}
