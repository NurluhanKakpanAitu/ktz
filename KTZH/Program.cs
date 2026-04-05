using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using KTZH.Configuration;
using KTZH.Data;
using KTZH.Hubs;
using KTZH.Services;

// Serilog: структурированные логи в консоль
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    // Controllers + Swagger (enums как строки в JSON)
    builder.Services.AddControllersWithViews()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "КТЖ Locomotive Dashboard API",
            Version = "v1",
            Description = "API мониторинга локомотивов АО «Казахстан Темір Жолы»"
        });

        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            options.IncludeXmlComments(xmlPath);

        // Swagger: кнопка Authorize с JWT Bearer
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Введите JWT токен"
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // JWT Authentication
    var jwtKey = builder.Configuration["Jwt:Key"]!;
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };

            // Поддержка JWT через query string для SignalR
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    // SignalR (enums как строки в JSON)
    builder.Services.AddSignalR(options =>
    {
        options.MaximumReceiveMessageSize = 102400;
    }).AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

    // SQLite через EF Core
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

    // CORS для Angular dev server
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Angular", policy =>
        {
            policy.WithOrigins("http://localhost:4200", "https://localhost:44492")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });

    // Конфигурация порогов Warning/Critical из appsettings.json
    builder.Services.Configure<ThresholdConfig>(builder.Configuration.GetSection("ThresholdConfig"));

    // Движок расчёта Health Score (Singleton — потребляет IOptions<ThresholdConfig>)
    builder.Services.AddSingleton<HealthScoreEngine>();

    // Телеметрия симулятор (Singleton — доступен контроллерам)
    builder.Services.AddSingleton<TelemetrySimulatorService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<TelemetrySimulatorService>());

    // Фоновый сервис очистки истории (каждый час удаляет записи старше 24ч)
    builder.Services.AddHostedService<HistoryCleanupService>();

    var app = builder.Build();

    // Создать БД при старте
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    // Swagger UI (в Development)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "КТЖ API v1");
            options.RoutePrefix = "swagger";
        });
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
    app.UseStaticFiles();
    app.UseRouting();
    app.UseCors("Angular");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller}/{action=Index}/{id?}");

    app.MapHub<TelemetryHub>("/hubs/telemetry");

    app.MapFallbackToFile("index.html");

    Log.Information("КТЖ Locomotive Dashboard запущен");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение завершилось с ошибкой");
}
finally
{
    Log.CloseAndFlush();
}