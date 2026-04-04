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

  arcPath: string = '';
  color: string = '#22c55e';

  // SVG semi-circle params
  private readonly cx = 100;
  private readonly cy = 100;
  private readonly r = 80;
  private readonly startAngle = Math.PI;       // 180° (left)
  private readonly endAngle = 2 * Math.PI;     // 360° (right)

  ngOnChanges(): void {
    this.color = GRADE_COLORS[this.grade] || '#6b7280';
    this.arcPath = this.buildArc(this.score);
  }

  private buildArc(score: number): string {
    const clamped = Math.max(0, Math.min(100, score));
    const fraction = clamped / 100;
    const angle = this.startAngle + fraction * (this.endAngle - this.startAngle);

    const x1 = this.cx + this.r * Math.cos(this.startAngle);
    const y1 = this.cy + this.r * Math.sin(this.startAngle);
    const x2 = this.cx + this.r * Math.cos(angle);
    const y2 = this.cy + this.r * Math.sin(angle);

    const largeArc = fraction > 0.5 ? 1 : 0;

    return `M ${x1} ${y1} A ${this.r} ${this.r} 0 ${largeArc} 1 ${x2} ${y2}`;
  }
}