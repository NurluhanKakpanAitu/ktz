import { Component, Input, OnChanges } from '@angular/core';
import { TelemetrySnapshot } from '../../models/telemetry.models';

interface ChartDef {
  label: string;
  field: keyof TelemetrySnapshot;
  unit: string;
  warningLine?: number;
  criticalLine?: number;
  color: string;
  value: number;
}

const TE33A_DEFS: Omit<ChartDef, 'value'>[] = [
  { label: 'Скорость',         field: 'speed',              unit: 'км/ч', warningLine: 120,  criticalLine: 120,  color: '#3b82f6' },
  { label: 'Температура масла', field: 'oilTemperature',     unit: '°C',   warningLine: 85,   criticalLine: 95,   color: '#f59e0b' },
  { label: 'Давление масла',   field: 'oilPressure',        unit: 'МПа',  warningLine: 0.49, criticalLine: 0.30, color: '#8b5cf6' },
  { label: 'Уровень топлива',  field: 'fuelLevel',          unit: '%',    warningLine: 20,   criticalLine: 10,   color: '#22c55e' },
];

const KZ8A_DEFS: Omit<ChartDef, 'value'>[] = [
  { label: 'Скорость',            field: 'speed',                  unit: 'км/ч', warningLine: 120,  criticalLine: 120,  color: '#3b82f6' },
  { label: 'Т° трансформатора',   field: 'transformerTemperature', unit: '°C',   warningLine: 80,   criticalLine: 95,   color: '#f59e0b' },
  { label: 'Давл. тормозной',     field: 'brakePressure',          unit: 'МПа',  warningLine: 0.50, criticalLine: 0.35, color: '#8b5cf6' },
  { label: 'Напряжение КС',       field: 'catenaryVoltage',        unit: 'кВ',   warningLine: 22,   criticalLine: 18,   color: '#22c55e' },
];

@Component({
  selector: 'app-trend-charts',
  template: `
    <div class="charts-grid">
      <app-shared-trend-chart
        *ngFor="let c of charts; trackBy: trackByLabel"
        [label]="c.label"
        [value]="c.value"
        [unit]="c.unit"
        [warningThreshold]="c.warningLine"
        [criticalThreshold]="c.criticalLine"
        [color]="c.color"
      ></app-shared-trend-chart>
    </div>
  `,
  styles: [`
    .charts-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
    }
    @media (max-width: 768px) {
      .charts-grid {
        grid-template-columns: 1fr;
      }
    }
  `]
})
export class TrendChartsComponent implements OnChanges {
  @Input() snapshot!: TelemetrySnapshot;
  @Input() locomotiveType: string = 'TE33A';

  charts: ChartDef[] = [];

  ngOnChanges(): void {
    if (!this.snapshot) return;
    const defs = this.locomotiveType === 'TE33A' ? TE33A_DEFS : KZ8A_DEFS;
    this.charts = defs.map(d => ({
      ...d,
      value: (this.snapshot[d.field] as number) ?? 0
    }));
  }

  trackByLabel(_: number, item: ChartDef): string {
    return item.label;
  }
}
