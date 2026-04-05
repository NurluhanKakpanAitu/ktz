using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KTZH.Data;
using KTZH.Models;
using KTZH.Services;

namespace KTZH.Controllers;

/// <summary>
/// API локомотивов: список, детали, история телеметрии, health score
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class LocomotivesController : ControllerBase
{
    private readonly TelemetrySimulatorService _simulator;
    private readonly AppDbContext _db;

    public LocomotivesController(TelemetrySimulatorService simulator, AppDbContext db)
    {
        _simulator = simulator;
        _db = db;
    }

    /// <summary>
    /// Получить список всех 10 локомотивов с текущим Health Score
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<LocomotiveDto>), StatusCodes.Status200OK)]
    public ActionResult<List<LocomotiveDto>> GetAll()
    {
        var result = _simulator.Fleet.Values.Select(s => new LocomotiveDto
        {
            Id = s.Locomotive.Id,
            Name = s.Locomotive.Name,
            Type = s.Locomotive.Type.ToString(),
            DepotCity = s.Locomotive.DepotCity,
            Route = s.Locomotive.CurrentRoute,
            Latitude = s.Locomotive.Latitude,
            Longitude = s.Locomotive.Longitude,
            HealthScore = s.LastHealth.Score,
            HealthGrade = s.LastHealth.Grade.ToString()
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Получить детали локомотива с последней телеметрией и Health Score
    /// </summary>
    /// <param name="id">ID локомотива (напр. TE33A-001)</param>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(LocomotiveDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<LocomotiveDetailDto> GetById(string id)
    {
        if (!_simulator.Fleet.TryGetValue(id, out var state))
            return NotFound(new { error = $"Локомотив {id} не найден" });

        var result = new LocomotiveDetailDto
        {
            Id = state.Locomotive.Id,
            Name = state.Locomotive.Name,
            Type = state.Locomotive.Type.ToString(),
            SerialNumber = state.Locomotive.SerialNumber,
            DepotCity = state.Locomotive.DepotCity,
            Route = state.Locomotive.CurrentRoute,
            Latitude = state.Locomotive.Latitude,
            Longitude = state.Locomotive.Longitude,
            LastTelemetry = state.LastTelemetry,
            LastHealth = state.LastHealth
        };

        return Ok(result);
    }

    /// <summary>
    /// Получить историю телеметрии локомотива
    /// </summary>
    /// <param name="id">ID локомотива</param>
    /// <param name="hours">Количество часов (1–24, по умолчанию 1)</param>
    [HttpGet("{id}/history")]
    [ProducesResponseType(typeof(List<TelemetryHistory>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TelemetryHistory>>> GetHistory(string id, [FromQuery] int hours = 1)
    {
        if (!_simulator.Fleet.ContainsKey(id))
            return NotFound(new { error = $"Локомотив {id} не найден" });

        hours = Math.Clamp(hours, 1, 24);
        var since = DateTime.UtcNow.AddHours(-hours);

        var history = await _db.TelemetryHistory
            .Where(h => h.LocomotiveId == id && h.Timestamp >= since)
            .OrderByDescending(h => h.Timestamp)
            .ToListAsync();

        return Ok(history);
    }

    /// <summary>
    /// Получить данные для Replay-режима за последние N минут (5/10/15).
    /// Отсортировано по Timestamp ASC — удобно для последовательного воспроизведения.
    /// </summary>
    /// <param name="id">ID локомотива</param>
    /// <param name="minutes">Окно в минутах (5, 10 или 15). По умолчанию 10.</param>
    [HttpGet("{id}/replay")]
    [ProducesResponseType(typeof(List<TelemetryHistory>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TelemetryHistory>>> GetReplay(string id, [FromQuery] int minutes = 10)
    {
        if (!_simulator.Fleet.ContainsKey(id))
            return NotFound(new { error = $"Локомотив {id} не найден" });

        // Принимаем только 5/10/15, иначе округляем к ближайшему допустимому
        if (minutes != 5 && minutes != 10 && minutes != 15)
            minutes = 10;

        var since = DateTime.UtcNow.AddMinutes(-minutes);

        var data = await _db.TelemetryHistory
            .Where(h => h.LocomotiveId == id && h.Timestamp >= since)
            .OrderBy(h => h.Timestamp)
            .ToListAsync();

        return Ok(data);
    }

    /// <summary>
    /// Экспорт телеметрии в CSV за последние N минут (5–60).
    /// Колонки: Timestamp, LocomotiveId, SpeedKmh, Temperature, Pressure, FuelOrVoltage, HealthScore, HealthGrade.
    /// </summary>
    /// <param name="id">ID локомотива</param>
    /// <param name="minutes">Окно в минутах (5–60). По умолчанию 15.</param>
    [HttpGet("{id}/export")]
    [Produces("text/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportCsv(string id, [FromQuery] int minutes = 15)
    {
        if (!_simulator.Fleet.ContainsKey(id))
            return NotFound(new { error = $"Локомотив {id} не найден" });

        minutes = Math.Clamp(minutes, 5, 60);
        var since = DateTime.UtcNow.AddMinutes(-minutes);

        var rows = await _db.TelemetryHistory
            .Where(h => h.LocomotiveId == id && h.Timestamp >= since)
            .OrderBy(h => h.Timestamp)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Timestamp,LocomotiveId,SpeedKmh,Temperature,Pressure,FuelOrVoltage,HealthScore,HealthGrade");

        foreach (var r in rows)
        {
            // Для ТЭ33А: температура=масло, давление=масло, fuel=уровень топлива
            // Для KZ8A:  температура=трансформатор, давление=тормозная, fuel=напряжение КС
            var isTe33a = r.LocomotiveType == LocomotiveType.TE33A;
            var temp = isTe33a ? r.OilTemperature : r.TransformerTemperature;
            var press = isTe33a ? r.OilPressure : (double?)r.BrakePressure;
            var fuelOrVoltage = isTe33a ? r.FuelLevel : r.CatenaryVoltage;

            sb.Append(r.Timestamp.ToString("O")).Append(',')
              .Append(r.LocomotiveId).Append(',')
              .Append(r.Speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
              .Append(temp?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(press?.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(fuelOrVoltage?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(r.HealthScore).Append(',')
              .AppendLine(r.HealthGrade.ToString());
        }

        // UTF-8 BOM для корректного открытия в Excel
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var payload = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, payload, 0, bom.Length);
        Buffer.BlockCopy(body, 0, payload, bom.Length, body.Length);

        var filename = $"loco-{id}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(payload, "text/csv", filename);
    }

    /// <summary>
    /// Получить текущий Health Score с полной расшифровкой компонентов
    /// </summary>
    /// <param name="id">ID локомотива</param>
    [HttpGet("{id}/health")]
    [ProducesResponseType(typeof(HealthScore), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<HealthScore> GetHealth(string id)
    {
        if (!_simulator.Fleet.TryGetValue(id, out var state))
            return NotFound(new { error = $"Локомотив {id} не найден" });

        return Ok(state.LastHealth);
    }
}

// ── DTO ──

/// <summary>Краткая информация о локомотиве для списка</summary>
public class LocomotiveDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DepotCity { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int HealthScore { get; set; }
    public string HealthGrade { get; set; } = string.Empty;
}

/// <summary>Полная информация о локомотиве</summary>
public class LocomotiveDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string DepotCity { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public TelemetrySnapshot LastTelemetry { get; set; } = new();
    public HealthScore LastHealth { get; set; } = new();
}