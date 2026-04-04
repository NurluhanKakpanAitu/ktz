import { Component, Input } from '@angular/core';
import { TelemetrySnapshot } from '../../models/telemetry.models';
import { TrendChartConfig } from './trend-chart.component';

const TE33A_CHARTS: TrendChartConfig[] = [
  { title: 'Скорость',        field: 'speed',              unit: 'км/ч', warningLine: 120,  criticalLine: 120,  color: '#3b82f6' },
  { title: 'Температура масла',field: 'oilTemperature',     unit: '°C',   warningLine: 85,   criticalLine: 95,   color: '#f59e0b' },
  { title: 'Давление масла',   field: 'oilPressure',        unit: 'МПа',  warningLine: 0.49,  criticalLine: 0.30, color: '#8b5cf6' },
  { title: 'Уровень топлива',  field: 'fuelLevel',          unit: '%',    warningLine: 20,   criticalLine: 10,   color: '#22c55e' },
];

const KZ8A_CHARTS: TrendChartConfig[] = [
  { title: 'Скорость',            field: 'speed',                    unit: 'км/ч', warningLine: 120,  criticalLine: 120,  color: '#3b82f6' },
  { title: 'Т° трансформатора',   field: 'transformerTemperature',   unit: '°C',   warningLine: 80,   criticalLine: 95,   color: '#f59e0b' },
  { title: 'Давл. тормозной',     field: 'brakePressure',            unit: 'МПа',  warningLine: 0.50, criticalLine: 0.35, color: '#8b5cf6' },
  { title: 'Напряжение КС',       field: 'catenaryVoltage',          unit: 'кВ',   warningLine: 22,   criticalLine: 18,   color: '#22c55e' },
];

@Component({
  selector: 'app-trend-charts',
  template: `
    <div class="charts-grid">
      <app-trend-chart
        *ngFor="let cfg of charts"
        [snapshot]="snapshot"
        [config]="cfg"
      ></app-trend-chart>
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
export class TrendChartsComponent {
  @Input() snapshot!: TelemetrySnapshot;
  @Input() locomotiveType: string = 'TE33A';

  get charts(): TrendChartConfig[] {
    return this.locomotiveType === 'TE33A' ? TE33A_CHARTS : KZ8A_CHARTS;
  }
}