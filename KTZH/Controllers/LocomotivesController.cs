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