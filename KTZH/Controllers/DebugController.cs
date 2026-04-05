using Microsoft.AspNetCore.Mvc;
using KTZH.Services;

namespace KTZH.Controllers;

/// <summary>
/// Отладочные endpoints (доступны только в Development)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DebugController : ControllerBase
{
    private readonly TelemetrySimulatorService _simulator;
    private readonly IWebHostEnvironment _env;

    public DebugController(TelemetrySimulatorService simulator, IWebHostEnvironment env)
    {
        _simulator = simulator;
        _env = env;
    }

    /// <summary>
    /// Highload тест: симулирует x10 burst — 100 событий телеметрии за ~500мс.
    /// Доступно только в Development окружении.
    /// </summary>
    [HttpPost("burst")]
    [ProducesResponseType(typeof(BurstResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BurstResponse>> Burst(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Доступно только в Development" });

        var (events, ms) = await _simulator.SimulateBurst(ct);
        return Ok(new BurstResponse { EventsGenerated = events, DurationMs = ms });
    }
}

public class BurstResponse
{
    public int EventsGenerated { get; set; }
    public long DurationMs { get; set; }
}
