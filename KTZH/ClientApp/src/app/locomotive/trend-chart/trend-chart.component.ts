import { Component, Input, OnChanges, OnDestroy, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';
import { TelemetrySnapshot } from '../../models/telemetry.models';

Chart.register(...registerables);

export interface TrendChartConfig {
  title: string;
  field: keyof TelemetrySnapshot;
  unit: string;
  warningLine?: number;
  criticalLine?: number;
  color: string;
}

const MAX_POINTS = 60;

@Component({
  selector: 'app-trend-chart',
  templateUrl: './trend-chart.component.html',
  styleUrls: ['./trend-chart.component.css']
})
export class TrendChartComponent implements AfterViewInit, OnChanges, OnDestroy {

  @Input() snapshot!: TelemetrySnapshot;
  @Input() config!: TrendChartConfig;

  @ViewChild('canvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;

  private chart?: Chart;
  private labels: string[] = [];
  private dataPoints: number[] = [];
  private initialized = false;

  ngAfterViewInit(): void {
    this.createChart();
    this.initialized = true;
    if (this.snapshot) this.pushPoint();
  }

  ngOnChanges(): void {
    if (this.initialized && this.snapshot) {
      this.pushPoint();
    }
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private pushPoint(): void {
    const val = this.snapshot[this.config.field];
    if (val == null) return;

    const now = new Date(this.snapshot.timestamp || Date.now());
    const timeLabel = now.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit', second: '2-digit' });

    this.labels.push(timeLabel);
    this.dataPoints.push(val as number);

    if (this.labels.length > MAX_POINTS) {
      this.labels.shift();
      this.dataPoints.shift();
    }

    if (this.chart) {
      this.chart.data.labels = this.labels;
      this.chart.data.datasets[0].data = this.dataPoints;
      this.chart.update('none'); // без анимации — плавнее при частых обновлениях
    }
  }

  private createChart(): void {
    const annotations: any[] = [];

    if (this.config.warningLine != null) {
      annotations.push({
        type: 'line',
        yMin: this.config.warningLine,
        yMax: this.config.warningLine,
        borderColor: '#f59e0b',
        borderDash: [6, 4],
        borderWidth: 1,
        label: { display: false }
      });
    }
    if (this.config.criticalLine != null) {
      annotations.push({
        type: 'line',
        yMin: this.config.criticalLine,
        yMax: this.config.criticalLine,
        borderColor: '#ef4444',
        borderDash: [6, 4],
        borderWidth: 1,
        label: { display: false }
      });
    }

    const cfg: ChartConfiguration = {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: this.config.color,
          backgroundColor: this.config.color + '20',
          borderWidth: 2,
          pointRadius: 0,
          fill: true,
          tension: 0.3
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        plugins: {
          legend: { display: false },
          title: {
            display: true,
            text: `${this.config.title} (${this.config.unit})`,
            color: '#94a3b8',
            font: { size: 12 }
          }
        },
        scales: {
          x: {
            display: true,
            ticks: {
              color: '#64748b',
              maxTicksLimit: 6,
              maxRotation: 0,
              font: { size: 10 }
            },
            grid: { color: '#1e2d4240' }
          },
          y: {
            ticks: {
              color: '#64748b',
              font: { size: 10 }
            },
            grid: { color: '#1e2d4260' }
          }
        }
      }
    };

    this.chart = new Chart(this.canvasRef.nativeElement, cfg);

    // Рисуем пороговые линии вручную через plugin (без chart.js-plugin-annotation)
    if (annotations.length > 0) {
      this.chart.config.plugins = [{
        id: 'thresholdLines',
        afterDraw: (chart: Chart) => {
          const ctx = chart.ctx;
          const yScale = chart.scales['y'];
          const xScale = chart.scales['x'];
          if (!yScale || !xScale) return;

          annotations.forEach((ann: any) => {
            const yPixel = yScale.getPixelForValue(ann.yMin);
            if (yPixel == null) return;

            ctx.save();
            ctx.beginPath();
            ctx.setLineDash(ann.borderDash || []);
            ctx.strokeStyle = ann.borderColor;
            ctx.lineWidth = ann.borderWidth;
            ctx.moveTo(xScale.left, yPixel);
            ctx.lineTo(xScale.right, yPixel);
            ctx.stroke();
            ctx.restore();
          });
        }
      }];
    }
  }
}