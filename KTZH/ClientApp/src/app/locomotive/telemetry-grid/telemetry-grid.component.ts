import { Component, Input, OnChanges } from '@angular/core';
import { TelemetrySnapshot } from '../../models/telemetry.models';

interface MetricCard {
  label: string;
  value: number;
  unit: string;
  status: 'normal' | 'warning' | 'critical';
  icon: string;
}

// Пороги из CLAUDE.md
const THRESHOLDS: Record<string, { warn: number; crit: number; ascending: boolean }> = {
  // ТЭ33А
  speed:              { warn: 120, crit: 120, ascending: true },
  oilTemperature:     { warn: 85,  crit: 95,  ascending: true },
  coolantTemperature: { warn: 90,  crit: 105, ascending: true },
  oilPressure:        { warn: 0.49, crit: 0.30, ascending: false },
  brakePressure:      { warn: 0.50, crit: 0.35, ascending: false },
  fuelLevel:          { warn: 20,  crit: 10,  ascending: false },
  dieselRpm:          { warn: 1050, crit: 1100, ascending: true },
  tractionMotorCurrentTE: { warn: 900, crit: 1000, ascending: true },
  // KZ8A
  catenaryVoltage:           { warn: 22, crit: 18, ascending: false },
  transformerTemperature:    { warn: 80, crit: 95, ascending: true },
  tractionMotorTemperature:  { warn: 80, crit: 100, ascending: true },
  tractionMotorCurrentKZ:    { warn: 1200, crit: 1400, ascending: true },
};

@Component({
  selector: 'app-telemetry-grid',
  templateUrl: './telemetry-grid.component.html',
  styleUrls: ['./telemetry-grid.component.css']
})
export class TelemetryGridComponent implements OnChanges {

  @Input() snapshot!: TelemetrySnapshot;
  @Input() locomotiveType: string = 'TE33A';

  cards: MetricCard[] = [];

  ngOnChanges(): void {
    if (!this.snapshot) return;
    this.cards = this.locomotiveType === 'TE33A'
      ? this.buildTE33A()
      : this.buildKZ8A();
  }

  private buildTE33A(): MetricCard[] {
    const s = this.snapshot;
    return [
      this.card('Скорость',           s.speed,                    'км/ч', 'speed',              '⚡'),
      this.card('Т° масла',           s.oilTemperature ?? 0,      '°C',   'oilTemperature',     '🌡'),
      this.card('Т° ОЖ',             s.coolantTemperature ?? 0,  '°C',   'coolantTemperature', '💧'),
      this.card('Давл. масла',        s.oilPressure ?? 0,         'МПа',  'oilPressure',        '🔧'),
      this.card('Давл. тормозной',    s.brakePressure,            'МПа',  'brakePressure',      '🛑'),
      this.card('Топливо',            s.fuelLevel ?? 0,           '%',    'fuelLevel',          '⛽'),
      this.card('Обороты дизеля',     s.dieselRpm ?? 0,           'об/мин','dieselRpm',         '🔄'),
      this.card('Ток ТЭД',           s.tractionMotorCurrent,     'А',    'tractionMotorCurrentTE', '⚙'),
    ];
  }

  private buildKZ8A(): MetricCard[] {
    const s = this.snapshot;
    return [
      this.card('Скорость',           s.speed,                        'км/ч', 'speed',                    '⚡'),
      this.card('Напряжение КС',      s.catenaryVoltage ?? 0,         'кВ',   'catenaryVoltage',          '🔌'),
      this.card('Т° трансформатора',   s.transformerTemperature ?? 0,  '°C',   'transformerTemperature',   '🌡'),
      this.card('Т° ТЭД',            s.tractionMotorTemperature ?? 0,'°C',   'tractionMotorTemperature', '🔥'),
      this.card('Давл. тормозной',    s.brakePressure,                'МПа',  'brakePressure',            '🛑'),
      this.card('Ток ТЭД',           s.tractionMotorCurrent,         'А',    'tractionMotorCurrentKZ',   '⚙'),
    ];
  }

  private card(label: string, value: number, unit: string, thresholdKey: string, icon: string): MetricCard {
    return { label, value, unit, icon, status: this.getStatus(value, thresholdKey) };
  }

  private getStatus(value: number, key: string): 'normal' | 'warning' | 'critical' {
    const t = THRESHOLDS[key];
    if (!t) return 'normal';

    if (t.ascending) {
      if (value >= t.crit) return 'critical';
      if (value >= t.warn) return 'warning';
    } else {
      if (value <= t.crit) return 'critical';
      if (value <= t.warn) return 'warning';
    }
    return 'normal';
  }
}