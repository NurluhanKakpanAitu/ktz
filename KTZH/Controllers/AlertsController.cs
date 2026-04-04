using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KTZH.Data;
using KTZH.Models;

namespace KTZH.Controllers;

/// <summary>
/// API алертов: активные и исторические предупреждения
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AlertsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Получить список алертов, отсортированных по серьёзности и времени
    /// </summary>
    /// <param name="active">Только активные алерты (по умолчанию true)</param>
    [HttpGet]
    [ProducesResponseType(typeof(List<Alert>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Alert>>> GetAlerts([FromQuery] bool active = true)
    {
        var query = _db.Alerts.AsQueryable();

        if (active)
            query = query.Where(a => a.IsActive);

        var alerts = await query
            .OrderByDescending(a => a.TriggeredAt)
            .Take(100)
            .ToListAsync();

        // Сортировка по Severity в памяти (enum → string в SQLite не сортируется правильно)
        alerts = alerts
            .OrderByDescending(a => a.Severity)
            .ThenByDescending(a => a.TriggeredAt)
            .ToList();

        return Ok(alerts);
    }
}