using System.Collections.Concurrent;
using System.Diagnostics;
using KTZH.Data;
using KTZH.Hubs;
using KTZH.Models;
using Microsoft.AspNetCore.SignalR;

namespace KTZH.Services;

/// <summary>
/// Фоновый сервис симуляции телеметрии 10 локомотивов КТЖ.
/// Push каждую секунду через SignalR, сохранение в SQLite.
/// </summary>
public class TelemetrySimulatorService : BackgroundService
{
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetrySimulatorService> _logger;
    private readonly Random _rng = new();

    /// <summary>Текущее состояние всех локомотивов (доступно контроллерам)</summary>
    public ConcurrentDictionary<string, LocomotiveState> Fleet { get; } = new();

    /// <summary>Активные алерты (in-memory кэш последних)</summary>
    public ConcurrentBag<Alert> RecentAlerts { get; } = new();

    private int _tickCount;
    private int _nextWarningTick;
    private int _nextCriticalTick;
    private readonly ConcurrentDictionary<string, (string Param, int ExpiresAtTick)> _activeOverrides = new();

    public TelemetrySimulatorService(
        IHubContext<TelemetryHub> hub,
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetrySimulatorService> logger)
    {
        _hub = hub;
        _scopeFactory = scopeFactory;
        _logger = logger;

        InitializeFleet();

        _nextWarningTick = 90 + _rng.Next(60);   // первый warning через ~90–150 сек
        _nextCriticalTick = 400 + _rng.Next(160); // первый critical через ~400–560 сек
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelemetrySimulator запущен: {Count} локомотивов", Fleet.Count);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await Tick(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка в симуляторе телеметрии");
            }
        }
    }

    /// <summary>Один тик симуляции (вызывается каждую секунду)</summary>
    private async Task Tick(CancellationToken ct)
    {
        _tickCount++;

        // Проверяем, нужно ли создать аномалию
        CheckAnomalySchedule();

        // Обновляем телеметрию всех 10 локомотивов
        var snapshots = new List<TelemetrySnapshot>(10);
        foreach (var state in Fleet.Values)
        {
            var snapshot = SimulateTelemetry(state);
            var health = HealthScoreEngine.Calculate(snapshot);

            state.LastTelemetry = snapshot;
            state.LastHealth = health;
            snapshots.Add(snapshot);

            // Генерируем Alert объекты при пересечении порогов
            GenerateAlerts(snapshot, health);

            // Отправляем в группу конкретного локомотива
            await _hub.Clients.Group($"loco-{state.Locomotive.Id}")
                .SendAsync("ReceiveTelemetry", snapshot, ct);
        }

        // Сохраняем в SQLite
        await SaveToDatabase(snapshots, ct);

        // Каждые 5 секунд — fleet update
        if (_tickCount % 5 == 0)
        {
            var fleetUpdate = Fleet.Values.ToList();
            await _hub.Clients.Group("fleet")
                .SendAsync("ReceiveFleet", fleetUpdate, ct);
        }
    }

    /// <summary>Highload тест: 10 локомотивов × 10 тиков за 500мс</summary>
    public async Task<(int EventsGenerated, long DurationMs)> SimulateBurst(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var count = 0;

        for (var i = 0; i < 10; i++)
        {
            foreach (var state in Fleet.Values)
            {
                var snapshot = SimulateTelemetry(state);
                var health = HealthScoreEngine.Calculate(snapshot);
                state.LastTelemetry = snapshot;
                state.LastHealth = health;

                await _hub.Clients.Group($"loco-{state.Locomotive.Id}")
                    .SendAsync("ReceiveTelemetry", snapshot, ct);
                count++;
            }
            await Task.Delay(50, ct); // 10 × 50мс = 500мс
        }

        sw.Stop();
        _logger.LogInformation("Burst завершён: {Count} событий за {Ms}мс", count, sw.ElapsedMilliseconds);
        return (count, sw.ElapsedMilliseconds);
    }

    // ── Инициализация 10 локомотивов ──

    private void InitializeFleet()
    {
        var locos = new[]
        {
            // ТЭ33А — тепловозы
            CreateLoco("TE33A-001", "ТЭ33А-001", LocomotiveType.TE33A, "Алматы",      "Алматы → Шымкент",      43.238, 76.889),
            CreateLoco("TE33A-002", "ТЭ33А-002", LocomotiveType.TE33A, "Шымкент",      "Шымкент → Кызылорда",   42.300, 69.590),
            CreateLoco("TE33A-003", "ТЭ33А-003", LocomotiveType.TE33A, "Актобе",       "Актобе → Кандыагаш",    50.279, 57.207),
            CreateLoco("TE33A-004", "ТЭ33А-004", LocomotiveType.TE33A, "Атырау",       "Атырау → Актобе",       47.106, 51.923),
            CreateLoco("TE33A-005", "ТЭ33А-005", LocomotiveType.TE33A, "УКК",          "УКК → Семей",           49.948, 82.628),
            CreateLoco("TE33A-006", "ТЭ33А-006", LocomotiveType.TE33A, "Кызылорда",    "Кызылорда → Туркестан", 44.853, 65.509),

            // KZ8A — электровозы
            CreateLoco("KZ8A-001",  "KZ8A-001",  LocomotiveType.KZ8A,  "Астана",       "Астана → Қарағанды",    51.180, 71.445),
            CreateLoco("KZ8A-002",  "KZ8A-002",  LocomotiveType.KZ8A,  "Қарағанды",    "Қарағанды → Алматы",    49.806, 73.088),
            CreateLoco("KZ8A-003",  "KZ8A-003",  LocomotiveType.KZ8A,  "Астана",       "Астана → Петропавловск",51.500, 70.100),
            CreateLoco("KZ8A-004",  "KZ8A-004",  LocomotiveType.KZ8A,  "Алматы",       "Алматы → Астана",       45.500, 74.200),
        };

        foreach (var state in locos)
        {
            // Задаём начальные нормальные значения телеметрии
            InitializeTelemetry(state);
            Fleet[state.Locomotive.Id] = state;
        }
    }

    private static LocomotiveState CreateLoco(string id, string name, LocomotiveType type,
        string depot, string route, double lat, double lon)
    {
        return new LocomotiveState
        {
            Locomotive = new Locomotive
            {
                Id = id,
                Name = name,
                Type = type,
                SerialNumber = $"SN-{id}",
                DepotCity = depot,
                Latitude = lat,
                Longitude = lon,
                CurrentRoute = route
            }
        };
    }

    private void InitializeTelemetry(LocomotiveState state)
    {
        var loco = state.Locomotive;
        var s = new TelemetrySnapshot
        {
            LocomotiveId = loco.Id,
            LocomotiveType = loco.Type,
            Speed = 60 + _rng.NextDouble() * 30, // 60–90 км/ч
            BrakePressure = 0.60 + _rng.NextDouble() * 0.20, // 0.60–0.80 МПа
        };

        if (loco.Type == LocomotiveType.TE33A)
        {
            s.OilTemperature = 55 + _rng.NextDouble() * 20;        // 55–75 °C
            s.CoolantTemperature = 75 + _rng.NextDouble() * 10;    // 75–85 °C
            s.OilPressure = 0.55 + _rng.NextDouble() * 0.35;       // 0.55–0.90 МПа
            s.FuelLevel = 60 + _rng.NextDouble() * 35;              // 60–95 %
            s.DieselRpm = 500 + _rng.NextDouble() * 400;            // 500–900 об/мин
            s.TractionMotorCurrent = 200 + _rng.NextDouble() * 500; // 200–700 А
        }
        else
        {
            s.TransformerTemperature = 45 + _rng.NextDouble() * 25;     // 45–70 °C
            s.TractionMotorTemperature = 30 + _rng.NextDouble() * 40;   // 30–70 °C
            s.CatenaryVoltage = 23 + _rng.NextDouble() * 4;             // 23–27 кВ
            s.TractiveEffort = 200 + _rng.NextDouble() * 400;           // 200–600 кН
            s.TractionMotorCurrent = 300 + _rng.NextDouble() * 600;     // 300–900 А
        }

        state.LastTelemetry = s;
        state.LastHealth = HealthScoreEngine.Calculate(s);
    }

    // ── Симуляция телеметрии (физически правдоподобные изменения) ──

    private TelemetrySnapshot SimulateTelemetry(LocomotiveState state)
    {
        var prev = state.LastTelemetry;
        var loco = state.Locomotive;
        var hasOverride = _activeOverrides.TryGetValue(loco.Id, out var ovr);
        var overrideParam = hasOverride ? ovr.Param : null;

        // Скорость: плавный разгон/торможение к целевой крейсерской скорости
        var targetSpeed = 80 + Gaussian(0, 5);
        var speed = Lerp(prev.Speed, targetSpeed, 0.02) + Gaussian(0, 2);
        speed = Math.Clamp(speed, 0, 125);

        // Давление тормозной: плавное, чуть колеблется
        var brake = Lerp(prev.BrakePressure, 0.70, 0.005) + Gaussian(0, 0.005);
        brake = Math.Clamp(brake, 0.30, 0.95);

        var s = new TelemetrySnapshot
        {
            LocomotiveId = loco.Id,
            LocomotiveType = loco.Type,
            Timestamp = DateTime.UtcNow,
            Speed = speed,
            BrakePressure = overrideParam == "BrakePressure" ? SimulateOverrideValue(prev.BrakePressure, 0.30, false) : brake,
        };

        if (loco.Type == LocomotiveType.TE33A)
            SimulateTE33A(s, prev, speed, overrideParam);
        else
            SimulateKZ8A(s, prev, speed, overrideParam);

        // Медленное движение координат вдоль маршрута
        state.Locomotive.Latitude += Gaussian(0, 0.001);
        state.Locomotive.Longitude += Gaussian(0, 0.001);

        return s;
    }

    private void SimulateTE33A(TelemetrySnapshot s, TelemetrySnapshot prev, double speed, string? ovr)
    {
        // Температура масла: растёт с нагрузкой, медленно меняется
        var oilTarget = speed / 120.0 * 70 + 15;
        var oilTemp = prev.OilTemperature!.Value + (oilTarget - prev.OilTemperature.Value) * 0.001 + Gaussian(0, 0.3);
        s.OilTemperature = ovr == "OilTemperature"
            ? SimulateOverrideValue(prev.OilTemperature.Value, 98, true)
            : Math.Clamp(oilTemp, 30, 110);

        // Температура ОЖ: зависит от температуры масла
        var coolTarget = s.OilTemperature.Value * 0.9 + 10;
        var cool = prev.CoolantTemperature!.Value + (coolTarget - prev.CoolantTemperature.Value) * 0.002 + Gaussian(0, 0.2);
        s.CoolantTemperature = ovr == "CoolantTemperature"
            ? SimulateOverrideValue(prev.CoolantTemperature.Value, 108, true)
            : Math.Clamp(cool, 50, 115);

        // Давление масла: инверсно к температуре
        var oilPress = 0.75 - (s.OilTemperature.Value - 65) * 0.003 + Gaussian(0, 0.01);
        s.OilPressure = ovr == "OilPressure"
            ? SimulateOverrideValue(prev.OilPressure!.Value, 0.25, false)
            : Math.Clamp(oilPress, 0.15, 1.10);

        // Топливо: медленно убывает
        var fuel = prev.FuelLevel!.Value - (speed > 10 ? 0.0011 : 0.0002);
        if (fuel < 5) fuel = 95; // "дозаправка" при критически низком уровне
        s.FuelLevel = ovr == "FuelLevel"
            ? SimulateOverrideValue(prev.FuelLevel.Value, 7, false)
            : Math.Clamp(fuel, 0, 100);

        // Обороты дизеля: зависят от скорости
        var rpmTarget = 320 + speed / 120.0 * 730;
        var rpm = Lerp(prev.DieselRpm!.Value, rpmTarget, 0.05) + Gaussian(0, 5);
        s.DieselRpm = ovr == "DieselRpm"
            ? SimulateOverrideValue(prev.DieselRpm.Value, 1120, true)
            : Math.Clamp(rpm, 300, 1150);

        // Ток ТЭД: зависит от скорости и нагрузки
        var currentTarget = speed / 120.0 * 800;
        var current = Lerp(prev.TractionMotorCurrent, currentTarget, 0.03) + Gaussian(0, 10);
        s.TractionMotorCurrent = ovr == "TractionMotorCurrent"
            ? SimulateOverrideValue(prev.TractionMotorCurrent, 1050, true)
            : Math.Clamp(current, 0, 1100);
    }

    private void SimulateKZ8A(TelemetrySnapshot s, TelemetrySnapshot prev, double speed, string? ovr)
    {
        // Напряжение КС: слегка колеблется вокруг 25 кВ
        var voltage = Lerp(prev.CatenaryVoltage!.Value, 25, 0.01) + Gaussian(0, 0.2);
        s.CatenaryVoltage = ovr == "CatenaryVoltage"
            ? SimulateOverrideValue(prev.CatenaryVoltage.Value, 16, false)
            : Math.Clamp(voltage, 15, 30);

        // Температура трансформатора: зависит от нагрузки
        var trTarget = speed / 120.0 * 60 + 20;
        var trTemp = prev.TransformerTemperature!.Value + (trTarget - prev.TransformerTemperature.Value) * 0.002 + Gaussian(0, 0.3);
        s.TransformerTemperature = ovr == "TransformerTemperature"
            ? SimulateOverrideValue(prev.TransformerTemperature.Value, 98, true)
            : Math.Clamp(trTemp, 25, 110);

        // Температура ТЭД
        var motorTarget = speed / 120.0 * 70 + 10;
        var motorTemp = prev.TractionMotorTemperature!.Value + (motorTarget - prev.TractionMotorTemperature.Value) * 0.002 + Gaussian(0, 0.3);
        s.TractionMotorTemperature = ovr == "TractionMotorTemperature"
            ? SimulateOverrideValue(prev.TractionMotorTemperature.Value, 105, true)
            : Math.Clamp(motorTemp, 0, 115);

        // Тяговое усилие
        var effortTarget = speed / 120.0 * 700;
        var effort = Lerp(prev.TractiveEffort!.Value, effortTarget, 0.03) + Gaussian(0, 10);
        s.TractiveEffort = Math.Clamp(effort, 0, 850);

        // Ток ТЭД
        var currentTarget = speed / 120.0 * 1000;
        var current = Lerp(prev.TractionMotorCurrent, currentTarget, 0.03) + Gaussian(0, 15);
        s.TractionMotorCurrent = ovr == "TractionMotorCurrent"
            ? SimulateOverrideValue(prev.TractionMotorCurrent, 1500, true)
            : Math.Clamp(current, 0, 1500);
    }

    // ── Аномалии (Warning / Critical) ──

    private void CheckAnomalySchedule()
    {
        // Очистить истёкшие override-ы
        foreach (var kv in _activeOverrides)
        {
            if (_tickCount >= kv.Value.ExpiresAtTick)
                _activeOverrides.TryRemove(kv.Key, out _);
        }

        // Warning: ~каждые 120 секунд
        if (_tickCount >= _nextWarningTick)
        {
            var locoId = Fleet.Keys.ElementAt(_rng.Next(Fleet.Count));
            var param = PickRandomParam(Fleet[locoId].Locomotive.Type);
            _activeOverrides[locoId] = (param, _tickCount + 15); // 15 секунд warning
            _logger.LogWarning("Симуляция Warning: {LocoId} параметр {Param}", locoId, param);
            _nextWarningTick = _tickCount + 100 + _rng.Next(40);
        }

        // Critical: ~каждые 480 секунд
        if (_tickCount >= _nextCriticalTick)
        {
            var locoId = Fleet.Keys.ElementAt(_rng.Next(Fleet.Count));
            var param = PickRandomParam(Fleet[locoId].Locomotive.Type);
            _activeOverrides[locoId] = (param, _tickCount + 30); // 30 секунд critical
            _logger.LogWarning("Симуляция Critical: {LocoId} параметр {Param}", locoId, param);
            _nextCriticalTick = _tickCount + 420 + _rng.Next(120);
        }
    }

    private string PickRandomParam(LocomotiveType type)
    {
        var te33aParams = new[] { "OilTemperature", "CoolantTemperature", "OilPressure", "FuelLevel", "DieselRpm", "TractionMotorCurrent", "BrakePressure" };
        var kz8aParams = new[] { "CatenaryVoltage", "TransformerTemperature", "TractionMotorTemperature", "TractionMotorCurrent", "BrakePressure" };

        var pool = type == LocomotiveType.TE33A ? te33aParams : kz8aParams;
        return pool[_rng.Next(pool.Length)];
    }

    /// <summary>Плавно двигать значение к аномальному целевому</summary>
    private double SimulateOverrideValue(double current, double anomalyTarget, bool ascending)
    {
        var step = ascending
            ? Math.Abs(anomalyTarget - current) * 0.08 + Gaussian(0, 0.5)
            : -Math.Abs(current - anomalyTarget) * 0.08 + Gaussian(0, 0.5);
        return current + step;
    }

    // ── Алерты ──

    private void GenerateAlerts(TelemetrySnapshot snapshot, HealthScore health)
    {
        if (health.ActiveAlerts.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var msg in health.ActiveAlerts)
        {
            var severity = msg.StartsWith("КРИТИЧНО") ? AlertSeverity.Critical : AlertSeverity.Warning;
            var alert = new Alert
            {
                LocomotiveId = snapshot.LocomotiveId,
                Severity = severity,
                Parameter = ExtractParamName(msg),
                Message = msg,
                Value = 0,
                TriggeredAt = DateTime.UtcNow
            };

            db.Alerts.Add(alert);
            RecentAlerts.Add(alert);

            // Broadcast алерт
            _hub.Clients.All.SendAsync("ReceiveAlert", alert);
        }

        db.SaveChanges();

        // Ограничиваем in-memory кэш до 200 записей
        while (RecentAlerts.Count > 200)
            RecentAlerts.TryTake(out _);
    }

    private static string ExtractParamName(string message)
    {
        // "КРИТИЧНО: Температура масла = 96.5 °C (критический порог)"
        var start = message.IndexOf(':') + 2;
        var end = message.IndexOf('=');
        if (start > 1 && end > start)
            return message[start..end].Trim();
        return message;
    }

    // ── Сохранение в SQLite ──

    private async Task SaveToDatabase(List<TelemetrySnapshot> snapshots, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var snapshot in snapshots)
            {
                var health = Fleet[snapshot.LocomotiveId].LastHealth;
                db.TelemetryHistory.Add(TelemetryHistory.FromSnapshot(snapshot, health));
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сохранения телеметрии в БД");
        }
    }

    // ── Утилиты ──

    private static double Lerp(double current, double target, double rate)
        => current + (target - current) * rate;

    private double Gaussian(double mean, double stdDev)
    {
        var u1 = 1.0 - _rng.NextDouble();
        var u2 = 1.0 - _rng.NextDouble();
        var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return mean + stdDev * normal;
    }
}