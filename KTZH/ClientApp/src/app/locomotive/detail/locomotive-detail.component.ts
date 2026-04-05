import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import {
  LocomotiveDetailDto,
  TelemetrySnapshot,
  HealthScore,
  HealthFactor,
  Alert
} from '../../models/telemetry.models';
import { ReplayStatus } from '../replay-controls/replay-controls.component';

interface TabDef {
  key: string;
  label: string;
  labelTE?: string;
  labelKZ?: string;
  icon: string;
  iconKZ?: string;
}

@Component({
  selector: 'app-locomotive-detail',
  templateUrl: './locomotive-detail.component.html',
  styleUrls: ['./locomotive-detail.component.css']
})
export class LocomotiveDetailComponent implements OnInit, OnDestroy {

  id = '';
  detail: LocomotiveDetailDto | null = null;
  snapshot: TelemetrySnapshot | null = null;
  health: HealthScore | null = null;
  alerts: Alert[] = [];

  /** История по каждому полю телеметрии (последние 5 минут) для seeding графиков. */
  historyByField: Record<string, number[]> = {};
  /** Метки времени для исторических точек. */
  historyLabels: string[] = [];

  activeTab = 'movement';

  tabs: TabDef[] = [
    { key: 'movement', label: 'Движение', icon: '🏎' },
    { key: 'engine', label: 'Двигатель', labelTE: 'Двигатель', labelKZ: 'Электропитание', icon: '🔥', iconKZ: '⚡' },
    { key: 'fuel', label: 'Топливо', labelTE: 'Топливо', labelKZ: 'Температуры', icon: '⛽', iconKZ: '🌡' },
  ];

  private telemetrySub?: Subscription;
  private healthSub?: Subscription;
  private alertSub?: Subscription;
  private routeSub?: Subscription;
  private healthInterval?: any;

  // ── Replay state ──
  replayStatus: ReplayStatus = 'idle';
  replayMinutes: 5 | 10 | 15 = 10;
  replaySpeed: 1 | 2 | 5 = 1;
  replayData: any[] = [];
  replayIndex = 0;
  replayLoading = false;
  private replayTimer?: any;
  /** Живой снимок, сохранённый на время replay — восстанавливается при Stop */
  private liveSnapshotBackup: TelemetrySnapshot | null = null;
  private liveHealthBackup: HealthScore | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private telemetry: TelemetryService,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    this.routeSub = this.route.paramMap.subscribe(params => {
      const newId = params.get('id') || '';
      if (newId !== this.id) {
        this.loadLocomotive(newId);
      }
    });
  }

  private loadLocomotive(newId: string): void {
    // Cleanup previous
    this.telemetrySub?.unsubscribe();
    this.healthSub?.unsubscribe();
    this.alertSub?.unsubscribe();
    if (this.healthInterval) clearInterval(this.healthInterval);

    this.id = newId;
    this.detail = null;
    this.snapshot = null;
    this.health = null;
    this.alerts = [];
    this.historyByField = {};
    this.historyLabels = [];
    this.activeTab = 'movement';

    this.api.getLocomotive(this.id).subscribe(d => {
      this.detail = d;
      this.snapshot = d.lastTelemetry;
      this.health = d.lastHealth;
      // После получения деталей загружаем историю для seeding графиков
      this.loadHistorySeed();
    });

    this.telemetry.connect(this.id);

    this.telemetrySub = this.telemetry.telemetry$.subscribe(s => {
      if (s.locomotiveId === this.id) {
        this.snapshot = s;
      }
    });

    // Health через SignalR каждую секунду (с top-3 факторами)
    this.healthSub = this.telemetry.health$.subscribe(h => {
      if (h.locomotiveId === this.id && this.replayStatus === 'idle') {
        this.health = h;
      }
    });

    this.alertSub = this.telemetry.alert$.subscribe(alert => {
      if (alert.locomotiveId === this.id) {
        this.alerts.unshift(alert);
        if (this.alerts.length > 50) this.alerts.pop();
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    this.telemetrySub?.unsubscribe();
    this.healthSub?.unsubscribe();
    this.alertSub?.unsubscribe();
    if (this.healthInterval) clearInterval(this.healthInterval);
    this.clearReplayTimer();
    this.telemetry.joinFleetGroup();
  }

  // ── Replay controls ──

  onReplayMinutes(m: 5 | 10 | 15): void {
    this.replayMinutes = m;
  }

  onReplaySpeed(s: 1 | 2 | 5): void {
    this.replaySpeed = s;
    if (this.replayStatus === 'playing') {
      this.clearReplayTimer();
      this.startReplayTimer();
    }
  }

  onReplayPlay(): void {
    if (this.replayStatus === 'paused') {
      this.replayStatus = 'playing';
      this.startReplayTimer();
      return;
    }

    // idle → загрузить данные и начать воспроизведение
    if (this.replayLoading || !this.id) return;
    this.replayLoading = true;
    this.api.getReplay(this.id, this.replayMinutes).subscribe({
      next: data => {
        this.replayLoading = false;
        if (!data || data.length === 0) {
          console.warn('[Replay] Нет данных за выбранный период');
          return;
        }
        this.replayData = data;
        this.replayIndex = 0;

        // Приостановить live: сохраняем текущий снимок и отписываемся
        this.liveSnapshotBackup = this.snapshot;
        this.liveHealthBackup = this.health;
        this.telemetrySub?.unsubscribe();
        this.telemetrySub = undefined;

        this.replayStatus = 'playing';
        this.applyReplayFrame();
        this.startReplayTimer();
      },
      error: err => {
        this.replayLoading = false;
        console.error('[Replay] Ошибка загрузки:', err);
      }
    });
  }

  onReplayPause(): void {
    if (this.replayStatus !== 'playing') return;
    this.replayStatus = 'paused';
    this.clearReplayTimer();
  }

  onReplayStop(): void {
    this.clearReplayTimer();
    this.replayStatus = 'idle';
    this.replayIndex = 0;
    this.replayData = [];

    // Восстанавливаем live-подписку
    if (this.liveSnapshotBackup) {
      this.snapshot = this.liveSnapshotBackup;
      this.health = this.liveHealthBackup;
      this.liveSnapshotBackup = null;
      this.liveHealthBackup = null;
    }
    if (!this.telemetrySub) {
      this.telemetrySub = this.telemetry.telemetry$.subscribe(s => {
        if (s.locomotiveId === this.id) {
          this.snapshot = s;
        }
      });
    }
  }

  onReplaySeek(index: number): void {
    if (!this.replayData.length) return;
    this.replayIndex = Math.max(0, Math.min(index, this.replayData.length - 1));
    this.applyReplayFrame();
  }

  private startReplayTimer(): void {
    const delay = 1000 / this.replaySpeed;
    this.replayTimer = setInterval(() => {
      if (this.replayIndex >= this.replayData.length - 1) {
        this.clearReplayTimer();
        this.replayStatus = 'paused';
        return;
      }
      this.replayIndex++;
      this.applyReplayFrame();
    }, delay);
  }

  private clearReplayTimer(): void {
    if (this.replayTimer) {
      clearInterval(this.replayTimer);
      this.replayTimer = undefined;
    }
  }

  private applyReplayFrame(): void {
    const point = this.replayData[this.replayIndex];
    if (!point) return;
    // TelemetryHistory совместим со TelemetrySnapshot по camelCase полям
    this.snapshot = point as TelemetrySnapshot;
    // Восстанавливаем health из точки (TelemetryHistory содержит healthScore/healthGrade)
    if (point.healthScore != null && point.healthGrade != null && this.health) {
      this.health = {
        ...this.health,
        score: point.healthScore,
        grade: point.healthGrade
      };
    }
  }

  /** Загрузить последние 5 минут истории и разложить её по полям для seeding графиков. */
  private loadHistorySeed(): void {
    if (!this.id) return;
    const requestId = this.id;
    this.api.getReplay(requestId, 5).subscribe({
      next: history => {
        console.log('[History seed] Получено точек:', history?.length, 'первая:', history?.[0]);
        // Если пользователь успел переключиться на другой локомотив — игнорируем
        if (requestId !== this.id) return;
        if (!history || history.length === 0) return;

        this.historyLabels = history.map(h =>
          new Date(h.timestamp).toLocaleTimeString('ru-RU', {
            hour: '2-digit', minute: '2-digit', second: '2-digit'
          })
        );

        const fields = [
          // Общие
          'speed', 'brakePressure', 'mainReservoirPressure', 'tractionMotorCurrent', 'tripDistance',
          // ТЭ33А
          'oilTemperature', 'coolantTemperature', 'oilPressure', 'coolantPressure', 'airFilterPressure',
          'fuelLevel', 'dieselRpm', 'engineHours', 'fuelTank1Level', 'fuelTank2Level',
          'instantFuelRate', 'totalFuelConsumed', 'tractiveEffortTE',
          // KZ8A
          'transformerTemperature', 'tractionMotorTemperature', 'igbtTemperature',
          'catenaryVoltage', 'catenaryCurrent', 'tractiveEffort', 'shaftPower',
          'powerFactor', 'brakeCylinderPressure'
        ];

        const map: Record<string, number[]> = {};
        for (const f of fields) {
          map[f] = history.map(h => (h[f] ?? 0) as number);
        }
        this.historyByField = map;
      },
      error: err => console.warn('[History seed] Ошибка загрузки:', err)
    });
  }

  goBack(): void {
    this.router.navigate(['/']);
  }

  /** Скачать CSV с телеметрией за последние 15 минут */
  exportCsv(): void {
    if (!this.id) return;
    this.api.exportCsv(this.id, 15).subscribe({
      next: blob => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        const stamp = new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-');
        a.href = url;
        a.download = `loco-${this.id}-${stamp}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
      },
      error: err => console.error('[Export CSV] Ошибка:', err)
    });
  }

  tabLabel(tab: TabDef): string {
    if (!this.detail) return tab.label;
    if (this.detail.type === 'TE33A') return tab.labelTE || tab.label;
    return tab.labelKZ || tab.label;
  }

  tabIcon(tab: TabDef): string {
    if (!this.detail) return tab.icon;
    if (this.detail.type === 'KZ8A' && tab.iconKZ) return tab.iconKZ;
    return tab.icon;
  }

  isTE33A(): boolean {
    return this.detail?.type === 'TE33A';
  }

  gradeColor(grade: string): string {
    const colors: Record<string, string> = {
      A: 'var(--grade-a)', B: 'var(--grade-b)', C: 'var(--grade-c)', D: 'var(--grade-d)', E: 'var(--grade-e)'
    };
    return colors[grade] || 'var(--color-unknown)';
  }

  typeName(): string {
    return this.detail?.type === 'TE33A' ? 'ТЭ33А (тепловоз)' : 'KZ8A (электровоз)';
  }

  engineModeClass(): string {
    if (!this.snapshot?.engineMode) return 'mode-optimal';
    switch (this.snapshot.engineMode) {
      case 'Idle': return 'mode-idle';
      case 'Overload': return 'mode-overload';
      default: return 'mode-optimal';
    }
  }

  engineModeLabel(): string {
    if (!this.snapshot?.engineMode) return 'Оптимальный';
    switch (this.snapshot.engineMode) {
      case 'Idle': return 'Холостой';
      case 'Overload': return 'Перегруз';
      default: return 'Оптимальный';
    }
  }

  /** Получить health contribution компонента по имени */
  hc(name: string): number {
    if (!this.health?.componentScores) return 100;
    return this.health.componentScores[name] ?? 100;
  }

  /** CSS-класс для чипа фактора в зависимости от его Score */
  factorChipClass(f: HealthFactor): string {
    if (f.score < 60) return 'factor-chip critical';
    if (f.score < 80) return 'factor-chip warning';
    return 'factor-chip ok';
  }

  /** Стрелка направления фактора: ↑ если плохо когда выше, ↓ если плохо когда ниже */
  factorArrow(f: HealthFactor): string {
    return f.direction === 'above' ? '↑' : '↓';
  }

  /** Короткое имя параметра для чипа */
  factorShortName(name: string): string {
    const map: Record<string, string> = {
      'Температура масла': 'Т°масла',
      'Температура ОЖ': 'Т°ОЖ',
      'Давление масла': 'Давл.масла',
      'Давление тормозной': 'Давл.торм',
      'Уровень топлива': 'Топливо',
      'Обороты дизеля': 'Обороты',
      'Ток ТЭД': 'Ток ТЭД',
      'Напряжение КС': 'Напр.КС',
      'Температура трансформатора': 'Т°тр-ра',
      'Температура ТЭД': 'Т°ТЭД',
      'Температура IGBT': 'Т°IGBT'
    };
    return map[name] ?? name;
  }

  /** Форматированное значение фактора для чипа */
  factorValue(f: HealthFactor): string {
    const v = f.currentValue;
    // Давление/cosφ — с 2 знаками; проценты и температуры — без дробной части
    if (f.unit === 'МПа' || f.unit === '') return v.toFixed(2);
    if (f.unit === '°C' || f.unit === '%' || f.unit === 'кВ' || f.unit === 'об/мин' || f.unit === 'А') {
      return Math.round(v).toString();
    }
    return v.toFixed(1);
  }
}
