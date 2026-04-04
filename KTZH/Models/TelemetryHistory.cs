using System.ComponentModel.DataAnnotations;

namespace KTZH.Models;

/// <summary>
/// EF Core entity для хранения истории телеметрии в SQLite
/// </summary>
public class TelemetryHistory
{
    /// <summary>Автоинкрементный первичный ключ</summary>
    [Key]
    public long Id { get; set; }

    /// <summary>ID локомотива</summary>
    public string LocomotiveId { get; set; } = string.Empty;

    /// <summary>Тип локомотива</summary>
    public LocomotiveType LocomotiveType { get; set; }

    /// <summary>Метка времени</summary>
    public DateTime Timestamp { get; set; }

    // ── Общие ──

    public double Speed { get; set; }
    public double BrakePressure { get; set; }
    public double TractionMotorCurrent { get; set; }

    // ── ТЭ33А ──

    public double? OilTemperature { get; set; }
    public double? CoolantTemperature { get; set; }
    public double? OilPressure { get; set; }
    public double? FuelLevel { get; set; }
    public double? DieselRpm { get; set; }

    // ── KZ8A ──

    public double? TransformerTemperature { get; set; }
    public double? TractionMotorTemperature { get; set; }
    public double? CatenaryVoltage { get; set; }
    public double? TractiveEffort { get; set; }

    // ── Расширенные (TASK-015) ──
    public bool WheelSlip { get; set; }
    public double TripDistance { get; set; }
    public double MainReservoirPressure { get; set; }
    public double? BrakeCylinderPressure { get; set; }
    public int ActiveErrorCount { get; set; }
    public double? EngineHours { get; set; }
    public double? CoolantPressure { get; set; }
    public double? AirFilterPressure { get; set; }
    public double? FuelTank1Level { get; set; }
    public double? FuelTank2Level { get; set; }
    public double? InstantFuelRate { get; set; }
    public double? TotalFuelConsumed { get; set; }
    public string? EngineMode { get; set; }
    public double? TractiveEffortTE { get; set; }
    public double? CatenaryCurrent { get; set; }
    public double? ShaftPower { get; set; }
    public double? PowerFactor { get; set; }
    public double? IgbtTemperature { get; set; }

    /// <summary>Health Score на момент записи</summary>
    public int HealthScore { get; set; }

    /// <summary>Health Grade на момент записи</summary>
    public HealthGrade HealthGrade { get; set; }

    /// <summary>Создать из TelemetrySnapshot</summary>
    public static TelemetryHistory FromSnapshot(TelemetrySnapshot snapshot, HealthScore health)
    {
        return new TelemetryHistory
        {
            LocomotiveId = snapshot.LocomotiveId,
            LocomotiveType = snapshot.LocomotiveType,
            Timestamp = snapshot.Timestamp,
            Speed = snapshot.Speed,
            BrakePressure = snapshot.BrakePressure,
            TractionMotorCurrent = snapshot.TractionMotorCurrent,
            OilTemperature = snapshot.OilTemperature,
            CoolantTemperature = snapshot.CoolantTemperature,
            OilPressure = snapshot.OilPressure,
            FuelLevel = snapshot.FuelLevel,
            DieselRpm = snapshot.DieselRpm,
            TransformerTemperature = snapshot.TransformerTemperature,
            TractionMotorTemperature = snapshot.TractionMotorTemperature,
            CatenaryVoltage = snapshot.CatenaryVoltage,
            TractiveEffort = snapshot.TractiveEffort,
            WheelSlip = snapshot.WheelSlip,
            TripDistance = snapshot.TripDistance,
            MainReservoirPressure = snapshot.MainReservoirPressure,
            BrakeCylinderPressure = snapshot.BrakeCylinderPressure,
            ActiveErrorCount = snapshot.ActiveErrorCount,
            EngineHours = snapshot.EngineHours,
            CoolantPressure = snapshot.CoolantPressure,
            AirFilterPressure = snapshot.AirFilterPressure,
            FuelTank1Level = snapshot.FuelTank1Level,
            FuelTank2Level = snapshot.FuelTank2Level,
            InstantFuelRate = snapshot.InstantFuelRate,
            TotalFuelConsumed = snapshot.TotalFuelConsumed,
            EngineMode = snapshot.EngineMode,
            TractiveEffortTE = snapshot.TractiveEffortTE,
            CatenaryCurrent = snapshot.CatenaryCurrent,
            ShaftPower = snapshot.ShaftPower,
            PowerFactor = snapshot.PowerFactor,
            IgbtTemperature = snapshot.IgbtTemperature,
            HealthScore = health.Score,
            HealthGrade = health.Grade
        };
    }
}