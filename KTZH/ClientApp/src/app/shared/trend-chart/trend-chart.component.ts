import { Component, Input, OnChanges, OnDestroy, AfterViewInit, ElementRef, ViewChild } from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

Chart.register(...registerables);

const MAX_POINTS = 60;

@Component({
  selector: 'app-shared-trend-chart',
  templateUrl: './trend-chart.component.html',
  styleUrls: ['./trend-chart.component.css']
})
export class SharedTrendChartComponent implements AfterViewInit, OnChanges, OnDestroy {

  @Input() label: string = '';
  @Input() value: number = 0;
  @Input() unit: string = '';
  @Input() warningThreshold?: number;
  @Input() criticalThreshold?: number;
  @Input() color: string = '#3b82f6';

  @ViewChild('canvas', { static: true }) canvasRef!: ElementRef<HTMLCanvasElement>;

  private chart?: Chart;
  private labels: string[] = [];
  private dataPoints: number[] = [];
  private initialized = false;

  ngAfterViewInit(): void {
    this.createChart();
    this.initialized = true;
    this.pushPoint();
  }

  ngOnChanges(): void {
    if (this.initialized) {
      this.pushPoint();
    }
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private pushPoint(): void {
    if (this.value == null) return;

    const now = new Date();
    const timeLabel = now.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit', second: '2-digit' });

    this.labels.push(timeLabel);
    this.dataPoints.push(this.value);

    if (this.labels.length > MAX_POINTS) {
      this.labels.shift();
      this.dataPoints.shift();
    }

    if (this.chart) {
      this.chart.data.labels = this.labels;
      this.chart.data.datasets[0].data = this.dataPoints;
      this.chart.update('none');
    }
  }

  private createChart(): void {
    const annotations: any[] = [];

    if (this.warningThreshold != null) {
      annotations.push({
        yVal: this.warningThreshold,
        borderColor: '#f59e0b',
        borderDash: [6, 4],
        borderWidth: 1
      });
    }
    if (this.criticalThreshold != null) {
      annotations.push({
        yVal: this.criticalThreshold,
        borderColor: '#ef4444',
        borderDash: [6, 4],
        borderWidth: 1
      });
    }

    const thresholdPlugin = {
      id: 'thresholdLines_' + Math.random().toString(36).slice(2),
      afterDraw: (chart: Chart) => {
        if (annotations.length === 0) return;
        const ctx = chart.ctx;
        const yScale = chart.scales['y'];
        const xScale = chart.scales['x'];
        if (!yScale || !xScale) return;

        annotations.forEach((ann: any) => {
          const yPixel = yScale.getPixelForValue(ann.yVal);
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
    };

    const cfg: ChartConfiguration = {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: this.color,
          backgroundColor: this.color + '20',
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
            text: `${this.label} (${this.unit})`,
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
      },
      plugins: [thresholdPlugin]
    };

    this.chart = new Chart(this.canvasRef.nativeElement, cfg);
  }
}
