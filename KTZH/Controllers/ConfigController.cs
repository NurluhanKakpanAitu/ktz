using KTZH.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KTZH.Controllers;

/// <summary>
/// Конфигурация системы: пороги Warning/Critical и пр.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ConfigController : ControllerBase
{
    private readonly ThresholdConfig _thresholds;

    public ConfigController(IOptions<ThresholdConfig> thresholds)
    {
        _thresholds = thresholds.Value;
    }

    /// <summary>
    /// Получить текущие пороги Warning/Critical для всех параметров (ТЭ33А и KZ8A)
    /// </summary>
    [HttpGet("thresholds")]
    [ProducesResponseType(typeof(ThresholdConfig), StatusCodes.Status200OK)]
    public ActionResult<ThresholdConfig> GetThresholds()
    {
        return Ok(_thresholds);
    }
}
