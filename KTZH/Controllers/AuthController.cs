using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace KTZH.Controllers;

/// <summary>
/// Авторизация: JWT логин для диспетчеров КТЖ
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;

    // Единственный пользователь — диспетчер КТЖ
    private const string ValidLogin = "dispatcher";
    private const string ValidPassword = "Ktz2024!";

    public AuthController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Логин — получить JWT токен
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (request.Login != ValidLogin || request.Password != ValidPassword)
        {
            return Unauthorized(new { message = "Неверный логин или пароль" });
        }

        var token = GenerateToken(request.Login);

        return Ok(new LoginResponse
        {
            Token = token,
            Login = request.Login,
            Role = "dispatcher",
            ExpiresAt = DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpireMinutes"]!))
        });
    }

    private string GenerateToken(string login)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, login),
            new Claim(ClaimTypes.Role, "dispatcher"),
            new Claim("sub", login)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(double.Parse(_config["Jwt:ExpireMinutes"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>Запрос на логин</summary>
public class LoginRequest
{
    /// <summary>Логин</summary>
    public string Login { get; set; } = string.Empty;
    /// <summary>Пароль</summary>
    public string Password { get; set; } = string.Empty;
}

/// <summary>Ответ после успешного логина</summary>
public class LoginResponse
{
    /// <summary>JWT токен</summary>
    public string Token { get; set; } = string.Empty;
    /// <summary>Логин пользователя</summary>
    public string Login { get; set; } = string.Empty;
    /// <summary>Роль</summary>
    public string Role { get; set; } = string.Empty;
    /// <summary>Время истечения токена</summary>
    public DateTime ExpiresAt { get; set; }
}