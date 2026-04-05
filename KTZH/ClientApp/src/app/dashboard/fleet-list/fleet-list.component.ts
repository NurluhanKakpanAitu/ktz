import { Component, OnInit, OnDestroy } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import { LocomotiveDto } from '../../models/telemetry.models';

const GRADE_COLORS: Record<string, string> = {
  A: 'var(--grade-a)', B: 'var(--grade-b)', C: 'var(--grade-c)', D: 'var(--grade-d)', E: 'var(--grade-e)'
};

const GRADE_ORDER: Record<string, number> = {
  E: 0, D: 1, C: 2, B: 3, A: 4
};

@Component({
  selector: 'app-fleet-list',
  templateUrl: './fleet-list.component.html',
  styleUrls: ['./fleet-list.component.css']
})
export class FleetListComponent implements OnInit, OnDestroy {

  locomotives: LocomotiveDto[] = [];
  searchQuery = '';
  filterType = '';
  filterGrade = '';
  sortBy: 'name' | 'health' | 'grade' = 'name';

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

  get filtered(): LocomotiveDto[] {
    let result = [...this.locomotives];

    // Поиск
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      result = result.filter(l =>
        l.name.toLowerCase().includes(q) ||
        l.route.toLowerCase().includes(q) ||
        l.depotCity.toLowerCase().includes(q) ||
        l.id.toLowerCase().includes(q)
      );
    }

    // Фильтр по типу
    if (this.filterType) {
      result = result.filter(l => l.type === this.filterType);
    }

    // Фильтр по грейду
    if (this.filterGrade) {
      result = result.filter(l => l.healthGrade === this.filterGrade);
    }

    // Сортировка
    if (this.sortBy === 'health') {
      result.sort((a, b) => a.healthScore - b.healthScore);
    } else if (this.sortBy === 'grade') {
      result.sort((a, b) => (GRADE_ORDER[a.healthGrade] ?? 5) - (GRADE_ORDER[b.healthGrade] ?? 5));
    } else {
      result.sort((a, b) => a.name.localeCompare(b.name));
    }

    return result;
  }

  goTo(id: string): void {
    this.router.navigate(['/locomotive', id]);
  }

  gradeColor(grade: string): string {
    return GRADE_COLORS[grade] || 'var(--color-unknown)';
  }
}
