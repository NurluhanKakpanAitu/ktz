export type LocomotiveType = 'TE33A' | 'KZ8A';
export type HealthGrade = 'A' | 'B' | 'C' | 'D' | 'E';
export type AlertSeverity = 'Info' | 'Warning' | 'Critical';

export interface TelemetrySnapshot {
  locomotiveId: string;
  locomotiveType: LocomotiveType;
  timestamp: string;

  // Общие
  speed: number;
  brakePressure: number;
  tractionMotorCurrent: number;

  // ТЭ33А
  oilTemperature?: number;
  coolantTemperature?: number;
  oilPressure?: number;
  fuelLevel?: number;
  dieselRpm?: number;

  // KZ8A
  transformerTemperature?: number;
  tractionMotorTemperature?: number;
  catenaryVoltage?: number;
  tractiveEffort?: number;

  // Расширенные (TASK-015)
  wheelSlip: boolean;
  tripDistance: number;
  mainReservoirPressure: number;
  brakeCylinderPressure?: number;
  activeErrorCount: number;

  // ТЭ33А расширенные
  engineHours?: number;
  coolantPressure?: number;
  airFilterPressure?: number;
  fuelTank1Level?: number;
  fuelTank2Level?: number;
  instantFuelRate?: number;
  totalFuelConsumed?: number;
  engineMode?: string;
  tractiveEffortTE?: number;

  // KZ8A расширенные
  catenaryCurrent?: number;
  shaftPower?: number;
  powerFactor?: number;
  igbtTemperature?: number;
}

export interface HealthFactor {
  parameterName: string;
  score: number;
  currentValue: number;
  unit: string;
  /** "above" — плохо когда выше; "below" — плохо когда ниже */
  direction: string;
}

export interface HealthScore {
  locomotiveId: string;
  score: number;
  grade: HealthGrade;
  componentScores: Record<string, number>;
  activeAlerts: string[];
  calculatedAt: string;
  topWorstFactors: HealthFactor[];
}

export interface Locomotive {
  id: string;
  name: string;
  type: string;
  serialNumber: string;
  depotCity: string;
  latitude: number;
  longitude: number;
  currentRoute: string;
}

export interface LocomotiveState {
  locomotive: Locomotive;
  lastTelemetry: TelemetrySnapshot;
  lastHealth: HealthScore;
}

export interface Alert {
  id: string;
  locomotiveId: string;
  severity: AlertSeverity;
  parameter: string;
  message: string;
  value: number;
  triggeredAt: string;
  isActive: boolean;
}

export interface LocomotiveDto {
  id: string;
  name: string;
  type: string;
  depotCity: string;
  route: string;
  latitude: number;
  longitude: number;
  healthScore: number;
  healthGrade: string;
}

export interface LocomotiveDetailDto {
  id: string;
  name: string;
  type: string;
  serialNumber: string;
  depotCity: string;
  route: string;
  latitude: number;
  longitude: number;
  lastTelemetry: TelemetrySnapshot;
  lastHealth: HealthScore;
}