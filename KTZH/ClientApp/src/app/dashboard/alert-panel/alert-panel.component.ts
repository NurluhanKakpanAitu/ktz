import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription, interval } from 'rxjs';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import { Alert } from '../../models/telemetry.models';

const MAX_ALERTS = 20;
const REFRESH_INTERVAL = 15_000; // обновлять список алертов каждые 15 сек

@Component({
  selector: 'app-alert-panel',
  templateUrl: './alert-panel.component.html',
  styleUrls: ['./alert-panel.component.css']
})
export class AlertPanelComponent implements OnInit, OnDestroy {

  alerts: Alert[] = [];
  criticalCount = 0;
  warningCount = 0;

  private alertSub?: Subscription;
  private refreshSub?: Subscription;

  constructor(
    private telemetry: TelemetryService,
    private api: ApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadAlerts();

    // Подписаться на live алерты через SignalR
    this.alertSub = this.telemetry.alert$.subscribe(alert => {
      // Убрать старый алерт с тем же locomotiveId+parameter (заменяем на свежий)
      this.alerts = this.alerts.filter(a =>
        !(a.locomotiveId === alert.locomotiveId && a.parameter === alert.parameter)
      );
      this.alerts.unshift(alert);
      if (this.alerts.length > MAX_ALERTS) {
        this.alerts.pop();
      }
      this.updateCounts();
    });

    // Периодически обновлять список из API (убираем деактивированные)
    this.refreshSub = interval(REFRESH_INTERVAL).subscribe(() => this.loadAlerts());
  }

  ngOnDestroy(): void {
    this.alertSub?.unsubscribe();
    this.refreshSub?.unsubscribe();
  }

  private loadAlerts(): void {
    this.api.getAlerts(true).subscribe(alerts => {
      this.alerts = alerts.slice(0, MAX_ALERTS);
      this.updateCounts();
    });
  }

  goToLocomotive(alert: Alert): void {
    this.router.navigate(['/locomotive', alert.locomotiveId]);
  }

  timeAgo(dateStr: string): string {
    // Бэкенд отдаёт UTC — добавляем 'Z' если нет таймзоны
    const utcStr = dateStr.endsWith('Z') || dateStr.includes('+') ? dateStr : dateStr + 'Z';
    const diff = Date.now() - new Date(utcStr).getTime();
    const sec = Math.max(0, Math.floor(diff / 1000));
    if (sec < 60) return `${sec}с назад`;
    const min = Math.floor(sec / 60);
    if (min < 60) return `${min}мин назад`;
    const hr = Math.floor(min / 60);
    return `${hr}ч назад`;
  }

  private updateCounts(): void {
    this.criticalCount = this.alerts.filter(a => a.severity === 'Critical').length;
    this.warningCount = this.alerts.filter(a => a.severity === 'Warning').length;
  }
}