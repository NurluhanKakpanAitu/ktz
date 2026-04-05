namespace KTZH.Configuration;

/// <summary>
/// Конфигурация порогов Warning/Critical для параметров телеметрии.
/// Загружается из appsettings.json → ThresholdConfig.
/// Позволяет менять пороги без перекомпиляции.
/// </summary>
public class ThresholdConfig
{
    /// <summary>Пороги тепловоза ТЭ33А</summary>
    public TE33AThresholds TE33A { get; set; } = new();

    /// <summary>Пороги электровоза KZ8A</summary>
    public KZ8AThresholds KZ8A { get; set; } = new();
}

/// <summary>Пороги ТЭ33А (тепловоз)</summary>
public class TE33AThresholds
{
    public Threshold OilTemperature { get; set; } = new();
    public Threshold OilPressure { get; set; } = new();
    public Threshold CoolantTemperature { get; set; } = new();
    public Threshold BrakeLinePressure { get; set; } = new();
    public Threshold FuelTank { get; set; } = new();
    public Threshold EngineRpm { get; set; } = new();
}

/// <summary>Пороги KZ8A (электровоз)</summary>
public class KZ8AThresholds
{
    public Threshold ContactVoltage { get; set; } = new();
    public Threshold TransformerTemp { get; set; } = new();
    public Threshold TractionMotorTemp { get; set; } = new();
    public Threshold IgbtTemp { get; set; } = new();
}

/// <summary>Порог параметра: Warning, Critical, направление</summary>
public class Threshold
{
    /// <summary>Граница предупреждения</summary>
    public double Warning { get; set; }

    /// <summary>Граница критического состояния</summary>
    public double Critical { get; set; }

    /// <summary>"above" — плохо когда выше; "below" — плохо когда ниже</summary>
    public string Direction { get; set; } = "above";
}
