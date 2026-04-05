import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import {
  LocomotiveDetailDto,
  TelemetrySnapshot,
  HealthScore,
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

  activeTab = 'movement';

  tabs: TabDef[] = [
    { key: 'movement', label: 'Движение', icon: '🏎' },
    { key: 'engine', label: 'Двигатель', labelTE: 'Двигатель', labelKZ: 'Электропитание', icon: '🔥', iconKZ: '⚡' },
    { key: 'fuel', label: 'Топливо', labelTE: 'Топливо', labelKZ: 'Температуры', icon: '⛽', iconKZ: '🌡' },
  ];

  private telemetrySub?: Subscription;
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
    this.alertSub?.unsubscribe();
    if (this.healthInterval) clearInterval(this.healthInterval);

    this.id = newId;
    this.detail = null;
    this.snapshot = null;
    this.health = null;
    this.alerts = [];
    this.activeTab = 'movement';

    this.api.getLocomotive(this.id).subscribe(d => {
      this.detail = d;
      this.snapshot = d.lastTelemetry;
      this.health = d.lastHealth;
    });

    this.telemetry.connect(this.id);

    this.telemetrySub = this.telemetry.telemetry$.subscribe(s => {
      if (s.locomotiveId === this.id) {
        this.snapshot = s;
      }
    });

    this.healthInterval = setInterval(() => {
      if (!this.id) return;
      this.api.getHealth(this.id).subscribe(h => {
        this.health = h;
      });
    }, 5000);

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

  goBack(): void {
    this.router.navigate(['/']);
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
}
