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
    this.telemetry.joinFleetGroup();
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
      A: '#22c55e', B: '#84cc16', C: '#f59e0b', D: '#f97316', E: '#ef4444'
    };
    return colors[grade] || '#6b7280';
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
