import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import { Alert } from '../../models/telemetry.models';

const MAX_ALERTS = 20;

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

  constructor(
    private telemetry: TelemetryService,
    private api: ApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    // Загрузить активные алерты из API
    this.api.getAlerts(true).subscribe(alerts => {
      this.alerts = alerts.slice(0, MAX_ALERTS);
      this.updateCounts();
    });

    // Подписаться на live алерты через SignalR
    this.alertSub = this.telemetry.alert$.subscribe(alert => {
      this.alerts.unshift(alert);
      if (this.alerts.length > MAX_ALERTS) {
        this.alerts.pop();
      }
      this.updateCounts();
    });
  }

  ngOnDestroy(): void {
    this.alertSub?.unsubscribe();
  }

  goToLocomotive(alert: Alert): void {
    this.router.navigate(['/locomotive', alert.locomotiveId]);
  }

  timeAgo(dateStr: string): string {
    const diff = Date.now() - new Date(dateStr).getTime();
    const sec = Math.floor(diff / 1000);
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