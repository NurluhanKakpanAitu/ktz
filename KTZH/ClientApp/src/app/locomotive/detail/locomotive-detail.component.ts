import { Component, OnInit, OnDestroy } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import {
  LocomotiveDetailDto,
  TelemetrySnapshot,
  HealthScore,
  HealthGrade,
  Alert
} from '../../models/telemetry.models';

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

  // Расшифровка компонентов, сортированная от худшего
  componentList: { name: string; score: number }[] = [];

  private telemetrySub?: Subscription;
  private alertSub?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private telemetry: TelemetryService,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id') || '';

    // Загрузить начальные данные
    this.api.getLocomotive(this.id).subscribe(d => {
      this.detail = d;
      this.snapshot = d.lastTelemetry;
      this.health = d.lastHealth;
      this.updateComponentList();
    });

    // Подключиться к группе конкретного локомотива
    this.telemetry.connect(this.id);

    this.telemetrySub = this.telemetry.telemetry$.subscribe(s => {
      if (s.locomotiveId === this.id) {
        this.snapshot = s;
        // Пересчитываем health из API раз в 5 секунд, но обновляем snapshot каждую секунду
      }
    });

    // Периодически обновляем health
    this.refreshHealth();

    this.alertSub = this.telemetry.alert$.subscribe(alert => {
      if (alert.locomotiveId === this.id) {
        this.alerts.unshift(alert);
        if (this.alerts.length > 20) this.alerts.pop();
      }
    });
  }

  ngOnDestroy(): void {
    this.telemetrySub?.unsubscribe();
    this.alertSub?.unsubscribe();
    this.telemetry.joinFleetGroup();
  }

  goBack(): void {
    this.router.navigate(['/']);
  }

  gradeColor(grade: string): string {
    const colors: Record<string, string> = {
      A: '#22c55e', B: '#84cc16', C: '#f59e0b', D: '#f97316', E: '#ef4444'
    };
    return colors[grade] || '#6b7280';
  }

  barWidth(score: number): string {
    return Math.max(0, Math.min(100, score)) + '%';
  }

  barColor(score: number): string {
    if (score >= 75) return '#22c55e';
    if (score >= 50) return '#f59e0b';
    return '#ef4444';
  }

  typeName(): string {
    return this.detail?.type === 'TE33A' ? 'ТЭ33А (тепловоз)' : 'KZ8A (электровоз)';
  }

  private refreshHealth(): void {
    const interval = setInterval(() => {
      if (!this.id) return;
      this.api.getHealth(this.id).subscribe(h => {
        this.health = h;
        this.updateComponentList();
      });
    }, 5000);

    // Очистка при destroy
    const origDestroy = this.ngOnDestroy.bind(this);
    this.ngOnDestroy = () => {
      clearInterval(interval);
      origDestroy();
    };
  }

  private updateComponentList(): void {
    if (!this.health) return;
    this.componentList = Object.entries(this.health.componentScores)
      .map(([name, score]) => ({ name, score }))
      .sort((a, b) => a.score - b.score);
  }
}