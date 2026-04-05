import {
  Component, Input, OnChanges, OnDestroy, AfterViewInit,
  ElementRef, ViewChild, SimpleChanges
} from '@angular/core';
import { Chart, ChartConfiguration, registerables } from 'chart.js';

Chart.register(...registerables);

const MAX_POINTS = 60;

export type ParamChartType =
  | 'line'           // базовый line (tension 0.3, без fill)
  | 'line+fill'      // line с заливкой, цвет по статусу (температуры и т.п.)
  | 'line-inverted'  // давления: danger=below, красный fill
  | 'gauge'          // скорость/RPM: SVG полукруг + sparkline
  | 'area-dual'      // баки: два area, разные цвета
  | 'bar'            // расход/ошибки: вертикальные бары с status-цветом
  | 'zone-line'      // ток/напряжение: line + цветные зоны фона
  | 'step'           // режим двигателя: stepped line
  | 'area'           // мощность/топливо: area без порогов
  | 'counter';       // счётчики: только текст

@Component({
  selector: 'app-param-chart',
  templateUrl: './param-chart.component.html',
  styleUrls: ['./param-chart.component.css']
})
export class ParamChartComponent implements AfterViewInit, OnChanges, OnDestroy {

  @Input() label = '';
  @Input() unit = '';
  @Input() value: number = 0;
  @Input() value2?: number;          // для area-dual (второй бак)
  @Input() label2?: string;          // label для второй линии
  @Input() warningThreshold?: number | null;
  @Input() criticalThreshold?: number | null;
  @Input() thresholdDirection: 'above' | 'below' = 'above';
  @Input() healthContribution: number = 100;
  @Input() healthWeight: number = 0;
  @Input() chartType: ParamChartType = 'line';
  @Input() maxValue: number = 0;     // для gauge
  @Input() initialHistory?: number[];      // начальная история для seeding
  @Input() initialHistory2?: number[];     // начальная история для area-dual (второй ряд)
  @Input() initialLabels?: string[];       // метки времени для начальной истории

  @ViewChild('canvas') canvasRef?: ElementRef<HTMLCanvasElement>;

  private chart?: Chart;
  private labels: string[] = [];
  private dataPoints: number[] = [];
  private dataPoints2: number[] = [];
  private initialized = false;
  private seededInitial = false;

  // Gauge
  readonly gaugeR = 70;
  gaugeDash = '0 999';
  gaugeColor = '#22c55e';
  readonly gaugeCircumference = Math.PI * 70; // half circle

  get isCounter(): boolean { return this.chartType === 'counter'; }
  get isGauge(): boolean { return this.chartType === 'gauge'; }
  get hasChart(): boolean { return !this.isCounter && !this.isGauge; }
  get showHealthBadge(): boolean { return this.healthWeight > 0 && !this.isCounter; }

  get healthColor(): string {
    if (this.healthContribution >= 80) return '#22c55e';
    if (this.healthContribution >= 60) return '#f59e0b';
    return '#ef4444';
  }

  get healthBarWidth(): string {
    return Math.max(0, Math.min(100, this.healthContribution)) + '%';
  }

  get formattedValue(): string {
    if (this.value == null) return '—';
    if (Math.abs(this.value) >= 100) return this.value.toFixed(0);
    if (Math.abs(this.value) >= 10) return this.value.toFixed(1);
    return this.value.toFixed(2);
  }

  get gaugePercent(): number {
    if (!this.maxValue) return 0;
    return Math.min(1, Math.max(0, this.value / this.maxValue));
  }

  ngAfterViewInit(): void {
    // Засеиваем начальную историю ДО создания графика, чтобы датасеты были не пустые
    this.trySeedInitialHistory();
    if (this.hasChart || this.isGauge) {
      this.createChart();
    }
    this.initialized = true;
    this.pushPoint();
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Always update gauge (safe — only changes component fields, no DOM bindings cause issues)
    if (this.isGauge) this.updateGauge();

    // Если initialHistory прилетела уже после создания графика — применить её
    if (this.initialized && !this.seededInitial) {
      this.trySeedInitialHistory();
    }

    if (this.initialized) {
      this.pushPoint();
    }
  }

  /** Попытаться засеять начальную историю в массивы данных. */
  private trySeedInitialHistory(): void {
    if (this.seededInitial) return;
    if (!this.initialHistory || this.initialHistory.length === 0) return;
    if (this.isCounter) { this.seededInitial = true; return; }

    const take = Math.min(this.initialHistory.length, MAX_POINTS);
    const startIdx = this.initialHistory.length - take;

    // Мутируем in-place — Chart.js держит ссылки на this.labels/dataPoints
    this.labels.length = 0;
    this.dataPoints.length = 0;
    this.dataPoints2.length = 0;

    for (let i = startIdx; i < this.initialHistory.length; i++) {
      this.dataPoints.push(this.initialHistory[i]);
      if (this.initialLabels && this.initialLabels[i] != null) {
        this.labels.push(this.initialLabels[i]);
      } else {
        this.labels.push('');
      }
      if (this.initialHistory2 && this.initialHistory2[i] != null) {
        this.dataPoints2.push(this.initialHistory2[i]);
      }
    }

    this.seededInitial = true;

    if (this.chart) {
      this.chart.update('none');
    }
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  private updateGauge(): void {
    const pct = this.gaugePercent;
    const arcLen = pct * this.gaugeCircumference;
    this.gaugeDash = `${arcLen} ${this.gaugeCircumference * 2}`;

    // Color by thresholds
    if (this.criticalThreshold != null) {
      const inCrit = this.thresholdDirection === 'above'
        ? this.value >= this.criticalThreshold
        : this.value <= this.criticalThreshold;
      if (inCrit) { this.gaugeColor = '#ef4444'; return; }
    }
    if (this.warningThreshold != null) {
      const inWarn = this.thresholdDirection === 'above'
        ? this.value >= this.warningThreshold
        : this.value <= this.warningThreshold;
      if (inWarn) { this.gaugeColor = '#f59e0b'; return; }
    }
    this.gaugeColor = '#22c55e';
  }

  private pushPoint(): void {
    if (this.value == null || this.isCounter) return;

    const now = new Date();
    const timeLabel = now.toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit', second: '2-digit' });

    this.labels.push(timeLabel);
    this.dataPoints.push(this.value);
    if (this.value2 != null) this.dataPoints2.push(this.value2);

    if (this.labels.length > MAX_POINTS) {
      this.labels.shift();
      this.dataPoints.shift();
      if (this.dataPoints2.length > MAX_POINTS) this.dataPoints2.shift();
    }

    if (this.chart) {
      this.chart.data.labels = this.labels;
      this.chart.data.datasets[0].data = this.dataPoints;
      if (this.chart.data.datasets[1]) {
        this.chart.data.datasets[1].data = this.dataPoints2;
      }
      this.chart.update('none');
    }
  }

  private getValueColor(): string {
    if (this.criticalThreshold != null) {
      const inCrit = this.thresholdDirection === 'above'
        ? this.value >= this.criticalThreshold
        : this.value <= this.criticalThreshold;
      if (inCrit) return '#ef4444';
    }
    if (this.warningThreshold != null) {
      const inWarn = this.thresholdDirection === 'above'
        ? this.value >= this.warningThreshold
        : this.value <= this.warningThreshold;
      if (inWarn) return '#f59e0b';
    }
    return '#22c55e';
  }

  private createChart(): void {
    if (!this.canvasRef) return;
    const ctx = this.canvasRef.nativeElement;

    switch (this.chartType) {
      case 'line':
        this.createPlainLineChart(ctx);
        break;
      case 'line+fill':
      case 'line-inverted':
        this.createLineChart(ctx);
        break;
      case 'zone-line':
        this.createZoneLineChart(ctx);
        break;
      case 'bar':
        this.createBarChart(ctx);
        break;
      case 'area-dual':
        this.createAreaDualChart(ctx);
        break;
      case 'step':
        this.createStepChart(ctx);
        break;
      case 'area':
        this.createAreaChart(ctx);
        break;
      case 'gauge':
        // gauge uses SVG, but also has a sparkline canvas
        this.createSparkline(ctx);
        break;
    }
  }

  // ── LINE (базовый, без заливки) ──
  private createPlainLineChart(ctx: HTMLCanvasElement): void {
    const self = this;
    const thresholdPlugin = this.buildThresholdPlugin();

    const dynamicColorPlugin = {
      id: 'dynColorLine_' + this.uid(),
      beforeDraw: (chart: Chart) => {
        const color = self.getValueColor();
        const ds = chart.data.datasets[0];
        if (ds) ds.borderColor = color;
      }
    };

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: '#22c55e',
          backgroundColor: 'transparent',
          borderWidth: 1.5,
          pointRadius: 0,
          fill: false,
          tension: 0.3
        }]
      },
      options: this.baseOptions(),
      plugins: [thresholdPlugin, dynamicColorPlugin]
    });
  }

  // ── LINE+FILL (температуры) / LINE-INVERTED (давления) ──
  private createLineChart(ctx: HTMLCanvasElement): void {
    const self = this;
    const thresholdPlugin = this.buildThresholdPlugin();

    const dynamicColorPlugin = {
      id: 'dynColor_' + this.uid(),
      beforeDraw: (chart: Chart) => {
        const color = self.getValueColor();
        const ds = chart.data.datasets[0];
        if (ds) {
          ds.borderColor = color;
          ds.backgroundColor = color + '18';
        }
      }
    };

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: '#22c55e',
          backgroundColor: '#22c55e18',
          borderWidth: 1.5,
          pointRadius: 0,
          fill: true,
          tension: 0.3
        }]
      },
      options: this.baseOptions(),
      plugins: [thresholdPlugin, dynamicColorPlugin]
    });
  }

  // ── ZONE-LINE (current, voltage) ──
  private createZoneLineChart(ctx: HTMLCanvasElement): void {
    const self = this;

    const zonePlugin = {
      id: 'zones_' + this.uid(),
      beforeDraw: (chart: Chart) => {
        const yScale = chart.scales['y'];
        const xScale = chart.scales['x'];
        if (!yScale || !xScale) return;
        const chartCtx = chart.ctx;
        const left = xScale.left;
        const right = xScale.right;
        const top = yScale.top;
        const bottom = yScale.bottom;

        // Draw green/amber/red zones
        if (self.warningThreshold != null && self.criticalThreshold != null) {
          const warnY = yScale.getPixelForValue(self.warningThreshold);
          const critY = yScale.getPixelForValue(self.criticalThreshold);

          if (self.thresholdDirection === 'above') {
            // green: bottom→warning, amber: warning→critical, red: critical→top
            chartCtx.fillStyle = 'rgba(34,197,94,0.06)';
            chartCtx.fillRect(left, warnY, right - left, bottom - warnY);
            chartCtx.fillStyle = 'rgba(245,158,11,0.08)';
            chartCtx.fillRect(left, critY, right - left, warnY - critY);
            chartCtx.fillStyle = 'rgba(239,68,68,0.08)';
            chartCtx.fillRect(left, top, right - left, critY - top);
          } else {
            // green: top→warning, amber: warning→critical, red: critical→bottom
            chartCtx.fillStyle = 'rgba(34,197,94,0.06)';
            chartCtx.fillRect(left, top, right - left, warnY - top);
            chartCtx.fillStyle = 'rgba(245,158,11,0.08)';
            chartCtx.fillRect(left, warnY, right - left, critY - warnY);
            chartCtx.fillStyle = 'rgba(239,68,68,0.08)';
            chartCtx.fillRect(left, critY, right - left, bottom - critY);
          }
        }
      }
    };

    const thresholdPlugin = this.buildThresholdPlugin();

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: '#3b82f6',
          backgroundColor: 'transparent',
          borderWidth: 1.5,
          pointRadius: 0,
          fill: false,
          tension: 0.3
        }]
      },
      options: this.baseOptions(),
      plugins: [zonePlugin, thresholdPlugin]
    });
  }

  // ── BAR (fuel consumption) ──
  private createBarChart(ctx: HTMLCanvasElement): void {
    const self = this;

    const dynamicBarColor = {
      id: 'dynBar_' + this.uid(),
      beforeDraw: (chart: Chart) => {
        const ds = chart.data.datasets[0];
        if (!ds) return;
        ds.backgroundColor = (ds.data as number[]).map((v: number) => {
          if (self.criticalThreshold != null && v >= self.criticalThreshold) return 'rgba(239,68,68,0.7)';
          if (self.warningThreshold != null && v >= self.warningThreshold) return 'rgba(245,158,11,0.7)';
          return 'rgba(34,197,94,0.6)';
        });
      }
    };

    this.chart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          backgroundColor: 'rgba(34,197,94,0.6)',
          borderRadius: 2,
          barPercentage: 0.8,
        }]
      },
      options: this.baseOptions(),
      plugins: [dynamicBarColor]
    });
  }

  // ── AREA-DUAL (fuel tanks) ──
  private createAreaDualChart(ctx: HTMLCanvasElement): void {
    const thresholdPlugin = this.buildThresholdPlugin();

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [
          {
            label: this.label,
            data: this.dataPoints,
            borderColor: '#3b82f6',
            backgroundColor: 'rgba(59,130,246,0.15)',
            borderWidth: 1.5,
            pointRadius: 0,
            fill: true,
            tension: 0.3
          },
          {
            label: this.label2 || 'Бак 2',
            data: this.dataPoints2,
            borderColor: '#8b5cf6',
            backgroundColor: 'rgba(139,92,246,0.15)',
            borderWidth: 1.5,
            pointRadius: 0,
            fill: true,
            tension: 0.3
          }
        ]
      },
      options: {
        ...this.baseOptions(),
        plugins: {
          legend: {
            display: true,
            position: 'bottom',
            labels: { color: '#64748b', font: { size: 10 }, boxWidth: 12 }
          }
        }
      },
      plugins: [thresholdPlugin]
    });
  }

  // ── STEP (engine mode) ──
  private createStepChart(ctx: HTMLCanvasElement): void {
    const bandPlugin = {
      id: 'bands_' + this.uid(),
      beforeDraw: (chart: Chart) => {
        const yScale = chart.scales['y'];
        const xScale = chart.scales['x'];
        if (!yScale || !xScale) return;
        const c = chart.ctx;
        const left = xScale.left;
        const w = xScale.right - left;

        // Idle zone (0–0.5), Optimal zone (0.5–1.5), Overload zone (1.5–2.5)
        const bands = [
          { min: -0.5, max: 0.5, color: 'rgba(100,116,139,0.08)' },   // Idle - grey
          { min: 0.5,  max: 1.5, color: 'rgba(34,197,94,0.08)' },     // Optimal - green
          { min: 1.5,  max: 2.5, color: 'rgba(239,68,68,0.08)' },     // Overload - red
        ];

        bands.forEach(b => {
          const top = yScale.getPixelForValue(b.max);
          const bottom = yScale.getPixelForValue(b.min);
          c.fillStyle = b.color;
          c.fillRect(left, top, w, bottom - top);
        });
      }
    };

    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: '#f1f5f9',
          backgroundColor: 'transparent',
          borderWidth: 2,
          pointRadius: 0,
          fill: false,
          stepped: 'middle',
        }]
      },
      options: {
        ...this.baseOptions(),
        scales: {
          ...this.baseOptions().scales,
          y: {
            min: -0.5,
            max: 2.5,
            ticks: {
              color: '#64748b',
              font: { size: 9 },
              stepSize: 1,
              callback: (val: any) => {
                if (val === 0) return 'Idle';
                if (val === 1) return 'Optimal';
                if (val === 2) return 'Overload';
                return '';
              }
            },
            grid: { color: '#1e2d4230' }
          }
        }
      },
      plugins: [bandPlugin]
    });
  }

  // ── AREA (power, informational) ──
  private createAreaChart(ctx: HTMLCanvasElement): void {
    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: '#8b5cf6',
          backgroundColor: 'rgba(139,92,246,0.15)',
          borderWidth: 1.5,
          pointRadius: 0,
          fill: true,
          tension: 0.3
        }]
      },
      options: this.baseOptions(),
      plugins: []
    });
  }

  // ── SPARKLINE (for gauge) ──
  private createSparkline(ctx: HTMLCanvasElement): void {
    this.chart = new Chart(ctx, {
      type: 'line',
      data: {
        labels: this.labels,
        datasets: [{
          data: this.dataPoints,
          borderColor: '#3b82f680',
          backgroundColor: 'transparent',
          borderWidth: 1,
          pointRadius: 0,
          fill: false,
          tension: 0.4
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        animation: false,
        plugins: { legend: { display: false } },
        scales: {
          x: { display: false },
          y: { display: false }
        }
      }
    });
  }

  // ── Helpers ──

  private buildThresholdPlugin(): any {
    const self = this;
    return {
      id: 'thresh_' + this.uid(),
      afterDraw: (chart: Chart) => {
        const ctx = chart.ctx;
        const yScale = chart.scales['y'];
        const xScale = chart.scales['x'];
        if (!yScale || !xScale) return;

        const lines: { val: number; color: string; label: string }[] = [];
        if (self.warningThreshold != null) {
          lines.push({ val: self.warningThreshold, color: '#f59e0b', label: `Warning: ${self.warningThreshold}${self.unit}` });
        }
        if (self.criticalThreshold != null) {
          lines.push({ val: self.criticalThreshold, color: '#ef4444', label: `Critical: ${self.criticalThreshold}${self.unit}` });
        }

        lines.forEach(l => {
          const y = yScale.getPixelForValue(l.val);
          if (y == null) return;
          ctx.save();
          ctx.beginPath();
          ctx.setLineDash([6, 4]);
          ctx.strokeStyle = l.color;
          ctx.lineWidth = 1;
          ctx.moveTo(xScale.left, y);
          ctx.lineTo(xScale.right, y);
          ctx.stroke();
          ctx.setLineDash([]);
          ctx.font = '9px sans-serif';
          ctx.fillStyle = l.color;
          ctx.textAlign = 'right';
          ctx.fillText(l.label, xScale.right, y - 3);
          ctx.restore();
        });
      }
    };
  }

  private baseOptions(): any {
    return {
      responsive: true,
      maintainAspectRatio: false,
      animation: false,
      plugins: { legend: { display: false } },
      scales: {
        x: {
          display: true,
          ticks: { color: '#64748b', maxTicksLimit: 4, maxRotation: 0, font: { size: 9 } },
          grid: { color: '#1e2d4230' }
        },
        y: {
          ticks: { color: '#64748b', font: { size: 9 }, maxTicksLimit: 5 },
          grid: { color: '#1e2d4240' }
        }
      }
    };
  }

  private uid(): string {
    return Math.random().toString(36).slice(2, 8);
  }
}
