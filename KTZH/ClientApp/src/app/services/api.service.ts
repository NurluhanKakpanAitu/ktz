import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  LocomotiveDto,
  LocomotiveDetailDto,
  HealthScore,
  Alert
} from '../models/telemetry.models';

@Injectable({ providedIn: 'root' })
export class ApiService {

  private readonly base = '/api';

  constructor(private http: HttpClient) {}

  /** Список всех 10 локомотивов */
  getLocomotives(): Observable<LocomotiveDto[]> {
    return this.http.get<LocomotiveDto[]>(`${this.base}/locomotives`);
  }

  /** Детали локомотива с телеметрией и health */
  getLocomotive(id: string): Observable<LocomotiveDetailDto> {
    return this.http.get<LocomotiveDetailDto>(`${this.base}/locomotives/${id}`);
  }

  /** История телеметрии */
  getHistory(id: string, hours: number = 1): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/locomotives/${id}/history?hours=${hours}`);
  }

  /** Replay: данные за последние N минут (5/10/15), отсортировано по Timestamp ASC */
  getReplay(id: string, minutes: number = 10): Observable<any[]> {
    return this.http.get<any[]>(`${this.base}/locomotives/${id}/replay?minutes=${minutes}`);
  }

  /** Экспорт телеметрии в CSV за последние N минут (5–60) */
  exportCsv(id: string, minutes: number = 15): Observable<Blob> {
    return this.http.get(`${this.base}/locomotives/${id}/export?minutes=${minutes}`, {
      responseType: 'blob'
    });
  }

  /** Health Score с расшифровкой */
  getHealth(id: string): Observable<HealthScore> {
    return this.http.get<HealthScore>(`${this.base}/locomotives/${id}/health`);
  }

  /** Активные алерты */
  getAlerts(active: boolean = true): Observable<Alert[]> {
    return this.http.get<Alert[]>(`${this.base}/alerts?active=${active}`);
  }

  /** Healthcheck */
  getServiceHealth(): Observable<{ status: string; locomotivesCount: number; timestamp: string }> {
    return this.http.get<any>(`${this.base}/health`);
  }

  /** Highload burst тест (только Development) */
  triggerBurst(): Observable<{ eventsGenerated: number; durationMs: number }> {
    return this.http.post<{ eventsGenerated: number; durationMs: number }>(`${this.base}/debug/burst`, {});
  }
}