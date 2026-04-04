namespace KTZH.Models;

/// <summary>
/// Мгновенный снимок телеметрии локомотива
/// </summary>
public class TelemetrySnapshot
{
    /// <summary>ID локомотива</summary>
    public string LocomotiveId { get; set; } = string.Empty;

    /// <summary>Тип локомотива (для выбора набора параметров)</summary>
    public LocomotiveType LocomotiveType { get; set; }

    /// <summary>Метка времени снимка</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // ── Общие параметры ──

    /// <summary>Скорость, км/ч (норма 0–120)</summary>
    public double Speed { get; set; }

    /// <summary>Давление тормозной магистрали, МПа (норма 0.50–0.90)</summary>
    public double BrakePressure { get; set; }

    /// <summary>Ток тяговых двигателей, А</summary>
    public double TractionMotorCurrent { get; set; }

    // ── Параметры ТЭ33А (тепловоз) ──

    /// <summary>Температура масла, °C (норма 45–85)</summary>
    public double? OilTemperature { get; set; }

    /// <summary>Температура охлаждающей жидкости, °C (норма 70–90)</summary>
    public double? CoolantTemperature { get; set; }

    /// <summary>Давление масла, МПа (норма 0.49–1.03)</summary>
    public double? OilPressure { get; set; }

    /// <summary>Уровень топлива, % (норма 20–100)</summary>
    public double? FuelLevel { get; set; }

    /// <summary>Обороты дизеля GEVO12, об/мин (норма 320–1050)</summary>
    public double? DieselRpm { get; set; }

    // ── Параметры KZ8A (электровоз) ──

    /// <summary>Температура трансформатора, °C (норма 40–80)</summary>
    public double? TransformerTemperature { get; set; }

    /// <summary>Температура тяговых двигателей, °C (норма 0–80)</summary>
    public double? TractionMotorTemperature { get; set; }

    /// <summary>Напряжение контактной сети, кВ (норма 22–28)</summary>
    public double? CatenaryVoltage { get; set; }

    /// <summary>Тяговое усилие, кН (норма 0–833)</summary>
    public double? TractiveEffort { get; set; }

    // ── Расширенные параметры (TASK-015) ──

    // Общие
    /// <summary>Боксование колёс (true = боксование)</summary>
    public bool WheelSlip { get; set; }

    /// <summary>Пробег рейса, км</summary>
    public double TripDistance { get; set; }

    /// <summary>Давление главных резервуаров, МПа</summary>
    public double MainReservoirPressure { get; set; }

    /// <summary>Давление тормозных цилиндров, МПа (KZ8A)</summary>
    public double? BrakeCylinderPressure { get; set; }

    /// <summary>Количество активных кодов ошибок</summary>
    public int ActiveErrorCount { get; set; }

    // ТЭ33А расширенные
    /// <summary>Моточасы ДГУ, ч</summary>
    public double? EngineHours { get; set; }

    /// <summary>Давление охлаждающей жидкости, МПа</summary>
    public double? CoolantPressure { get; set; }

    /// <summary>Давление воздушного фильтра, кПа</summary>
    public double? AirFilterPressure { get; set; }

    /// <summary>Уровень топлива бак 1, %</summary>
    public double? FuelTank1Level { get; set; }

    /// <summary>Уровень топлива бак 2, %</summary>
    public double? FuelTank2Level { get; set; }

    /// <summary>Мгновенный расход топлива, л/ч</summary>
    public double? InstantFuelRate { get; set; }

    /// <summary>Суммарный расход рейса, л</summary>
    public double? TotalFuelConsumed { get; set; }

    /// <summary>Режим двигателя: Idle / Optimal / Overload</summary>
    public string? EngineMode { get; set; }

    /// <summary>Тяговое усилие ТЭ33А, кН</summary>
    public double? TractiveEffortTE { get; set; }

    // KZ8A расширенные
    /// <summary>Ток контактной сети, А</summary>
    public double? CatenaryCurrent { get; set; }

    /// <summary>Мощность на валу, кВт</summary>
    public double? ShaftPower { get; set; }

    /// <summary>Коэффициент мощности cosφ</summary>
    public double? PowerFactor { get; set; }

    /// <summary>Температура IGBT преобразователя, °C</summary>
    public double? IgbtTemperature { get; set; }
}