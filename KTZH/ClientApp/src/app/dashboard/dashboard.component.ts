import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { TelemetryService } from '../services/telemetry.service';
import { ApiService } from '../services/api.service';
import { LocomotiveState } from '../models/telemetry.models';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit, OnDestroy {

  viewMode: 'map' | 'list' = 'map';
  fleetHealthAvg = 0;
  alertCount = 0;
  connected = false;

  private fleetSub?: Subscription;
  private alertSub?: Subscription;
  private connSub?: Subscription;

  constructor(
    private telemetry: TelemetryService,
    private api: ApiService
  ) {}

  ngOnInit(): void {
    this.telemetry.connect();

    this.connSub = this.telemetry.connected$.subscribe(c => this.connected = c);

    this.fleetSub = this.telemetry.fleet$.subscribe(fleet => {
      if (fleet.length > 0) {
        this.fleetHealthAvg = Math.round(
          fleet.reduce((sum, s) => sum + s.lastHealth.score, 0) / fleet.length
        );
      }
    });

    this.alertSub = this.telemetry.alert$.subscribe(() => {
      this.alertCount++;
    });

    // Начальный подсчёт из API
    this.api.getLocomotives().subscribe(locos => {
      if (locos.length > 0) {
        this.fleetHealthAvg = Math.round(
          locos.reduce((sum, l) => sum + l.healthScore, 0) / locos.length
        );
      }
    });

    this.api.getAlerts(true).subscribe(alerts => {
      this.alertCount = alerts.length;
    });
  }

  ngOnDestroy(): void {
    this.fleetSub?.unsubscribe();
    this.alertSub?.unsubscribe();
    this.connSub?.unsubscribe();
  }
}