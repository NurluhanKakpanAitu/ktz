import { Component, Input } from '@angular/core';

export type MetricStatus = 'normal' | 'warning' | 'critical';

@Component({
  selector: 'app-metric-card',
  templateUrl: './metric-card.component.html',
  styleUrls: ['./metric-card.component.css']
})
export class MetricCardComponent {
  @Input() title: string = '';
  @Input() value: number = 0;
  @Input() unit: string = '';
  @Input() status: MetricStatus = 'normal';
  @Input() icon: string = '';
  @Input() format: string = '1.1-1';
}
