using Microsoft.AspNetCore.Mvc;
using KTZH.Services;

namespace KTZH.Controllers;

/// <summary>
/// Healthcheck сервиса
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly TelemetrySimulatorService _simulator;

    public HealthController(TelemetrySimulatorService simulator)
    {
        _simulator = simulator;
    }

    /// <summary>
    /// Проверка работоспособности сервиса
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthCheckResponse> Get()
    {
        return Ok(new HealthCheckResponse
        {
            Status = "ok",
            LocomotivesCount = _simulator.Fleet.Count,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = "ok";
    public int LocomotivesCount { get; set; }
    public DateTime Timestamp { get; set; }
}