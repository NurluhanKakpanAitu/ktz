import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import { LocomotiveDto, LocomotiveState } from '../../models/telemetry.models';

const GRADE_COLORS: Record<string, string> = {
  A: '#22c55e', B: '#84cc16', C: '#f59e0b', D: '#f97316', E: '#ef4444'
};

@Component({
  selector: 'app-fleet-list',
  templateUrl: './fleet-list.component.html',
  styleUrls: ['./fleet-list.component.css']
})
export class FleetListComponent implements OnInit, OnDestroy {

  locomotives: LocomotiveDto[] = [];
  private fleetSub?: Subscription;

  constructor(
    private telemetry: TelemetryService,
    private api: ApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.api.getLocomotives().subscribe(locos => this.locomotives = locos);

    this.fleetSub = this.telemetry.fleet$.subscribe(fleet => {
      fleet.forEach(state => {
        const idx = this.locomotives.findIndex(l => l.id === state.locomotive.id);
        if (idx >= 0) {
          this.locomotives[idx] = {
            ...this.locomotives[idx],
            latitude: state.locomotive.latitude,
            longitude: state.locomotive.longitude,
            healthScore: state.lastHealth.score,
            healthGrade: state.lastHealth.grade
          };
        }
      });
    });
  }

  ngOnDestroy(): void {
    this.fleetSub?.unsubscribe();
  }

  goTo(id: string): void {
    this.router.navigate(['/locomotive', id]);
  }

  gradeColor(grade: string): string {
    return GRADE_COLORS[grade] || '#6b7280';
  }
}