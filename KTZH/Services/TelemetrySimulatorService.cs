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

    /// <summary>Прогресс каждого локомотива по маршруту (0.0–1.0)</summary>
    private readonly ConcurrentDictionary<string, double> _routeProgress = new();
    /// <summary>Направление движения: +1 = вперёд, -1 = обратно</summary>
    private readonly ConcurrentDictionary<string, int> _routeDirection = new();

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

        // Сохраняем в SQLite каждые 5 секунд (не каждый тик — снижаем нагрузку на БД)
        if (_tickCount % 5 == 0)
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

        // Общие расширенные
        s.MainReservoirPressure = 0.75 + _rng.NextDouble() * 0.15;  // 0.75–0.90 МПа
        s.TripDistance = 0;
        s.ActiveErrorCount = 0;

        if (loco.Type == LocomotiveType.TE33A)
        {
            s.OilTemperature = 55 + _rng.NextDouble() * 20;        // 55–75 °C
            s.CoolantTemperature = 75 + _rng.NextDouble() * 10;    // 75–85 °C
            s.OilPressure = 0.55 + _rng.NextDouble() * 0.35;       // 0.55–0.90 МПа
            s.FuelLevel = 60 + _rng.NextDouble() * 35;              // 60–95 %
            s.DieselRpm = 500 + _rng.NextDouble() * 400;            // 500–900 об/мин
            s.TractionMotorCurrent = 200 + _rng.NextDouble() * 500; // 200–700 А
            s.EngineHours = 1000 + _rng.NextDouble() * 5000;       // 1000–6000 ч
            s.CoolantPressure = 0.10 + _rng.NextDouble() * 0.08;   // 0.10–0.18 МПа
            s.AirFilterPressure = 95 + _rng.NextDouble() * 5;      // 95–100 кПа
            s.FuelTank1Level = (s.FuelLevel ?? 80) * 0.55;
            s.FuelTank2Level = (s.FuelLevel ?? 80) * 0.45;
            s.InstantFuelRate = 50 + _rng.NextDouble() * 150;      // 50–200 л/ч
            s.TotalFuelConsumed = 100 + _rng.NextDouble() * 500;   // 100–600 л
            s.EngineMode = "Optimal";
            s.TractiveEffortTE = 100 + _rng.NextDouble() * 200;    // 100–300 кН
        }
        else
        {
            s.TransformerTemperature = 45 + _rng.NextDouble() * 25;     // 45–70 °C
            s.TractionMotorTemperature = 30 + _rng.NextDouble() * 40;   // 30–70 °C
            s.CatenaryVoltage = 23 + _rng.NextDouble() * 4;             // 23–27 кВ
            s.TractiveEffort = 200 + _rng.NextDouble() * 400;           // 200–600 кН
            s.TractionMotorCurrent = 300 + _rng.NextDouble() * 600;     // 300–900 А
            s.CatenaryCurrent = 100 + _rng.NextDouble() * 200;         // 100–300 А
            s.ShaftPower = 2000 + _rng.NextDouble() * 4000;            // 2000–6000 кВт
            s.PowerFactor = 0.85 + _rng.NextDouble() * 0.10;           // 0.85–0.95
            s.IgbtTemperature = 35 + _rng.NextDouble() * 25;           // 35–60 °C
            s.BrakeCylinderPressure = 0.25 + _rng.NextDouble() * 0.15; // 0.25–0.40 МПа
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

        // Главные резервуары: стабильно около 0.80 МПа
        var mainRes = Lerp(prev.MainReservoirPressure, 0.80, 0.01) + Gaussian(0, 0.003);

        // Пробег: растёт с каждым тиком пропорционально скорости (км = скорость * время/3600)
        var tripDist = prev.TripDistance + speed / 3600.0;

        // Ошибки: обычно 0, при override может быть 1-2
        var errorCount = prev.ActiveErrorCount;

        var s = new TelemetrySnapshot
        {
            LocomotiveId = loco.Id,
            LocomotiveType = loco.Type,
            Timestamp = DateTime.UtcNow,
            Speed = speed,
            BrakePressure = overrideParam == "BrakePressure" ? SimulateOverrideValue(prev.BrakePressure, 0.30, false) : brake,
            MainReservoirPressure = Math.Clamp(mainRes, 0.60, 0.95),
            TripDistance = tripDist,
            ActiveErrorCount = hasOverride ? (_rng.NextDouble() < 0.3 ? 1 : 0) : 0,
            WheelSlip = speed > 40 && _rng.NextDouble() < 0.005, // редкое событие
        };

        if (loco.Type == LocomotiveType.TE33A)
            SimulateTE33A(s, prev, speed, overrideParam);
        else
            SimulateKZ8A(s, prev, speed, overrideParam);

        // Движение по реальному ЖД маршруту
        UpdatePosition(state, speed);

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

        // Расширенные параметры ТЭ33А
        s.EngineHours = (prev.EngineHours ?? 3000) + 1.0 / 3600.0; // +1 сек
        s.CoolantPressure = Math.Clamp(
            Lerp(prev.CoolantPressure ?? 0.14, 0.14, 0.01) + Gaussian(0, 0.002),
            0.05, 0.25);
        s.AirFilterPressure = Math.Clamp(
            Lerp(prev.AirFilterPressure ?? 98, 98, 0.005) + Gaussian(0, 0.2),
            85, 101);
        // Баки: бак1 ~55%, бак2 ~45% от общего уровня
        s.FuelTank1Level = (s.FuelLevel ?? 80) * 0.55 + Gaussian(0, 0.3);
        s.FuelTank2Level = (s.FuelLevel ?? 80) * 0.45 + Gaussian(0, 0.3);
        // Мгновенный расход: зависит от оборотов
        s.InstantFuelRate = Math.Clamp(
            (s.DieselRpm ?? 700) / 1050.0 * 200 + Gaussian(0, 5),
            10, 300);
        // Суммарный расход
        s.TotalFuelConsumed = (prev.TotalFuelConsumed ?? 200) + (s.InstantFuelRate ?? 100) / 3600.0;
        // Режим двигателя
        s.EngineMode = (s.DieselRpm ?? 700) < 400 ? "Idle"
            : (s.DieselRpm ?? 700) > 1000 ? "Overload" : "Optimal";
        // Тяговое усилие ТЭ33А
        s.TractiveEffortTE = Math.Clamp(
            speed / 120.0 * 350 + Gaussian(0, 5), 0, 500);
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

        // Расширенные параметры KZ8A
        s.CatenaryCurrent = Math.Clamp(
            s.TractionMotorCurrent * 0.3 + Gaussian(0, 5), 0, 500);
        s.ShaftPower = Math.Clamp(
            (s.CatenaryVoltage ?? 25) * (s.CatenaryCurrent ?? 150) * (0.85 + Gaussian(0, 0.02)),
            0, 8800);
        s.PowerFactor = Math.Clamp(
            0.90 + Gaussian(0, 0.015), 0.75, 0.99);
        s.IgbtTemperature = Math.Clamp(
            Lerp(prev.IgbtTemperature ?? 50, speed / 120.0 * 55 + 25, 0.003) + Gaussian(0, 0.3),
            20, 100);
        s.BrakeCylinderPressure = Math.Clamp(
            Lerp(prev.BrakeCylinderPressure ?? 0.30, 0.30, 0.01) + Gaussian(0, 0.005),
            0.10, 0.50);
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

    /// <summary>Трекинг активных алертов по (locomotiveId, parameter) — чтобы не дублировать</summary>
    private readonly ConcurrentDictionary<string, DateTime> _activeAlertKeys = new();
    private int _lastDeactivationTick;

    private void GenerateAlerts(TelemetrySnapshot snapshot, HealthScore health)
    {
        var locoId = snapshot.LocomotiveId;

        // Собираем текущие проблемные параметры
        var currentParams = new HashSet<string>();
        foreach (var msg in health.ActiveAlerts)
        {
            currentParams.Add(ExtractParamName(msg));
        }

        // Деактивируем алерты этого локомотива, которых нет в currentParams
        var keysToRemove = _activeAlertKeys.Keys
            .Where(k => k.StartsWith(locoId + ":") && !currentParams.Contains(k[(locoId.Length + 1)..]))
            .ToList();

        foreach (var key in keysToRemove)
            _activeAlertKeys.TryRemove(key, out _);

        // Каждые 30 секунд — деактивируем в БД алерты старше 2 минут
        if (_tickCount - _lastDeactivationTick >= 30)
        {
            _lastDeactivationTick = _tickCount;
            try
            {
                using var scopeClean = _scopeFactory.CreateScope();
                var dbClean = scopeClean.ServiceProvider.GetRequiredService<AppDbContext>();
                var cutoff = DateTime.UtcNow.AddMinutes(-2);
                var staleAlerts = dbClean.Alerts
                    .Where(a => a.IsActive && a.TriggeredAt < cutoff)
                    .ToList();
                foreach (var a in staleAlerts)
                    a.IsActive = false;
                if (staleAlerts.Count > 0)
                    dbClean.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка деактивации старых алертов");
            }
        }

        // Создаём новые алерты только для параметров без активного трекинга
        if (health.ActiveAlerts.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var newAlerts = false;

        foreach (var msg in health.ActiveAlerts)
        {
            var param = ExtractParamName(msg);
            var key = $"{locoId}:{param}";

            // Уже есть активный алерт для этого параметра — пропускаем
            if (_activeAlertKeys.ContainsKey(key)) continue;

            var severity = msg.StartsWith("КРИТИЧНО") ? AlertSeverity.Critical : AlertSeverity.Warning;
            var alert = new Alert
            {
                LocomotiveId = locoId,
                Severity = severity,
                Parameter = param,
                Message = msg,
                Value = 0,
                TriggeredAt = DateTime.UtcNow
            };

            _activeAlertKeys[key] = DateTime.UtcNow;
            db.Alerts.Add(alert);
            RecentAlerts.Add(alert);
            newAlerts = true;

            // Broadcast алерт
            _hub.Clients.All.SendAsync("ReceiveAlert", alert);
        }

        if (newAlerts)
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

    // ── Реальные ЖД маршруты Казахстана (путевые точки lat/lon) ──

    private static readonly Dictionary<string, (double Lat, double Lon)[]> RailwayRoutes = new()
    {
        // ТЭ33А-001: Алматы → Шымкент (через Тараз)
        ["TE33A-001"] = new[] {
            (43.238, 76.889), (43.350, 76.200), (43.400, 75.500),
            (43.300, 74.800), (43.100, 74.000), (42.980, 73.200),
            (42.900, 72.400), (42.800, 71.600), (42.530, 71.367),  // Тараз
            (42.450, 70.800), (42.350, 70.100), (42.315, 69.597)   // Шымкент
        },
        // ТЭ33А-002: Шымкент → Кызылорда
        ["TE33A-002"] = new[] {
            (42.315, 69.597), (42.400, 69.000), (42.500, 68.200),
            (42.800, 67.500), (43.200, 67.000), (43.500, 66.500),
            (43.800, 66.000), (44.200, 65.600), (44.853, 65.509)   // Кызылорда
        },
        // ТЭ33А-003: Актобе → Кандыагаш
        ["TE33A-003"] = new[] {
            (50.279, 57.207), (50.150, 57.500), (50.000, 57.900),
            (49.800, 58.200), (49.600, 58.600), (49.470, 58.850),
            (49.400, 59.100), (49.300, 59.400), (49.200, 59.600)   // Кандыагаш
        },
        // ТЭ33А-004: Атырау → Актобе
        ["TE33A-004"] = new[] {
            (47.106, 51.923), (47.300, 52.300), (47.600, 52.800),
            (47.900, 53.400), (48.200, 54.000), (48.600, 54.500),
            (49.000, 55.100), (49.400, 55.700), (49.800, 56.300),
            (50.100, 56.800), (50.279, 57.207)                      // Актобе
        },
        // ТЭ33А-005: УКК → Семей
        ["TE33A-005"] = new[] {
            (49.948, 82.628), (49.980, 82.200), (50.050, 81.600),
            (50.100, 81.000), (50.200, 80.500), (50.350, 80.200),
            (50.410, 80.260)                                         // Семей
        },
        // ТЭ33А-006: Кызылорда → Туркестан
        ["TE33A-006"] = new[] {
            (44.853, 65.509), (44.600, 65.800), (44.300, 66.200),
            (44.000, 66.700), (43.700, 67.200), (43.400, 67.800),
            (43.297, 68.251)                                         // Туркестан
        },
        // KZ8A-001: Астана → Қарағанды
        ["KZ8A-001"] = new[] {
            (51.180, 71.445), (51.000, 71.600), (50.800, 71.900),
            (50.600, 72.200), (50.400, 72.500), (50.200, 72.800),
            (50.000, 73.000), (49.806, 73.088)                      // Қарағанды
        },
        // KZ8A-002: Қарағанды → Алматы (через Балхаш)
        ["KZ8A-002"] = new[] {
            (49.806, 73.088), (49.400, 73.300), (49.000, 73.800),
            (48.500, 74.200), (47.800, 74.500), (46.848, 74.980),   // Балхаш
            (46.200, 75.200), (45.500, 75.500), (44.800, 75.900),
            (44.200, 76.300), (43.600, 76.600), (43.238, 76.889)    // Алматы
        },
        // KZ8A-003: Астана → Петропавловск
        ["KZ8A-003"] = new[] {
            (51.180, 71.445), (51.400, 71.200), (51.700, 70.800),
            (52.000, 70.400), (52.300, 70.000), (52.600, 69.600),
            (52.900, 69.300), (53.200, 69.142),
            (54.200, 69.100), (54.875, 69.163)                      // Петропавловск
        },
        // KZ8A-004: Алматы → Астана (через Балхаш, Қарағанды)
        ["KZ8A-004"] = new[] {
            (43.238, 76.889), (43.600, 76.600), (44.200, 76.300),
            (44.800, 75.900), (45.500, 75.500), (46.200, 75.200),
            (46.848, 74.980), (47.800, 74.500), (48.500, 74.200),   // Балхаш
            (49.000, 73.800), (49.806, 73.088),                      // Қарағанды
            (50.200, 72.800), (50.600, 72.200), (51.000, 71.600),
            (51.180, 71.445)                                          // Астана
        }
    };

    /// <summary>Интерполяция позиции по маршруту (0.0–1.0)</summary>
    private static (double Lat, double Lon) InterpolateRoute((double Lat, double Lon)[] waypoints, double progress)
    {
        if (waypoints.Length < 2) return waypoints[0];

        var p = Math.Clamp(progress, 0, 1);
        var totalSegments = waypoints.Length - 1;
        var exactSegment = p * totalSegments;
        var segIndex = (int)Math.Floor(exactSegment);
        if (segIndex >= totalSegments) segIndex = totalSegments - 1;
        var t = exactSegment - segIndex;

        var from = waypoints[segIndex];
        var to = waypoints[segIndex + 1];

        return (
            from.Lat + (to.Lat - from.Lat) * t,
            from.Lon + (to.Lon - from.Lon) * t
        );
    }

    /// <summary>Обновить позицию локомотива по ЖД маршруту</summary>
    private void UpdatePosition(LocomotiveState state, double speed)
    {
        var id = state.Locomotive.Id;
        if (!RailwayRoutes.TryGetValue(id, out var waypoints)) return;

        // Скорость движения по маршруту: ~80 км/ч → полный маршрут за ~10 минут симуляции
        var speedFactor = (speed / 120.0) * 0.0008;

        var dir = _routeDirection.GetOrAdd(id, 1);
        var progress = _routeProgress.GetOrAdd(id, _rng.NextDouble() * 0.3); // старт на случайной позиции

        progress += speedFactor * dir;

        // Разворот на концах маршрута
        if (progress >= 1.0) { progress = 1.0; dir = -1; }
        else if (progress <= 0.0) { progress = 0.0; dir = 1; }

        _routeProgress[id] = progress;
        _routeDirection[id] = dir;

        var (lat, lon) = InterpolateRoute(waypoints, progress);
        state.Locomotive.Latitude = lat;
        state.Locomotive.Longitude = lon;
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