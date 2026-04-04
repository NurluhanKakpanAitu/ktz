import { Component, Input, OnChanges } from '@angular/core';
import { HealthGrade } from '../../models/telemetry.models';

const GRADE_COLORS: Record<string, string> = {
  A: '#22c55e',
  B: '#84cc16',
  C: '#f59e0b',
  D: '#f97316',
  E: '#ef4444'
};

@Component({
  selector: 'app-health-gauge',
  templateUrl: './health-gauge.component.html',
  styleUrls: ['./health-gauge.component.css']
})
export class HealthGaugeComponent implements OnChanges {

  @Input() score: number = 0;
  @Input() grade: HealthGrade = 'A';

  color: string = '#22c55e';

  // SVG semi-circle: используем stroke-dasharray на одном <circle>
  // Полуокружность длиной π * r
  readonly r = 80;
  readonly cx = 100;
  readonly cy = 100;
  readonly halfCircle = Math.PI * 80;       // длина полуокружности ~251.33
  readonly fullCircle = 2 * Math.PI * 80;   // полная окружность ~502.65

  // Фоновая дуга: ровно полуокружность, остаток — gap
  bgDash = `${Math.PI * 80} ${2 * Math.PI * 80}`;

  valueDash: string = `0 ${2 * Math.PI * 80}`;

  ngOnChanges(): void {
    this.color = GRADE_COLORS[this.grade] || '#6b7280';
    const clamped = Math.max(0, Math.min(100, this.score));
    const filled = (clamped / 100) * this.halfCircle;
    this.valueDash = `${filled} ${this.fullCircle}`;
  }
}