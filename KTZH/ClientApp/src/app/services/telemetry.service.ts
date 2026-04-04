import { Injectable, OnDestroy } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import * as signalR from '@microsoft/signalr';
import {
  TelemetrySnapshot,
  LocomotiveState,
  Alert
} from '../models/telemetry.models';

// Бэкенд URL: через Angular proxy (dev) или напрямую (prod)
const BACKEND_URL = 'http://localhost:5210';

@Injectable({ providedIn: 'root' })
export class TelemetryService implements OnDestroy {

  private connection: signalR.HubConnection | null = null;

  private telemetrySubject = new Subject<TelemetrySnapshot>();
  private fleetSubject = new Subject<LocomotiveState[]>();
  private alertSubject = new Subject<Alert>();
  private connectedSubject = new Subject<boolean>();

  /** Телеметрия конкретного локомотива (детальный вид) */
  telemetry$: Observable<TelemetrySnapshot> = this.telemetrySubject.asObservable();

  /** Состояние всего парка (диспетчерский вид, каждые 5 сек) */
  fleet$: Observable<LocomotiveState[]> = this.fleetSubject.asObservable();

  /** Алерты (broadcast) */
  alert$: Observable<Alert> = this.alertSubject.asObservable();

  /** Статус подключения */
  connected$: Observable<boolean> = this.connectedSubject.asObservable();

  private currentLocomotiveId: string | null = null;

  /**
   * Подключиться к SignalR hub.
   * Без locomotiveId — попадаем в группу fleet.
   * С locomotiveId — в группу loco-{id}.
   */
  async connect(locomotiveId?: string): Promise<void> {
    // Если уже подключены, переключить группу
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      if (locomotiveId && locomotiveId !== this.currentLocomotiveId) {
        await this.joinLocomotiveGroup(locomotiveId);
      } else if (!locomotiveId && this.currentLocomotiveId) {
        await this.joinFleetGroup();
      }
      return;
    }

    const url = locomotiveId
      ? `${BACKEND_URL}/hubs/telemetry?locomotiveId=${locomotiveId}`
      : `${BACKEND_URL}/hubs/telemetry`;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(url, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets
      })
      .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.registerHandlers();
    this.registerReconnectHandlers();

    this.currentLocomotiveId = locomotiveId ?? null;

    try {
      await this.connection.start();
      this.connectedSubject.next(true);
      console.log('[SignalR] Подключено', locomotiveId ? `loco-${locomotiveId}` : 'fleet');
    } catch (err) {
      console.error('[SignalR] Ошибка подключения:', err);
      this.connectedSubject.next(false);
    }
  }

  /** Переключиться на детальный вид конкретного локомотива */
  async joinLocomotiveGroup(locomotiveId: string): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) return;

    await this.connection.invoke('JoinLocomotiveGroup', locomotiveId);
    this.currentLocomotiveId = locomotiveId;
    console.log('[SignalR] Переключено на loco-' + locomotiveId);
  }

  /** Вернуться в диспетчерский вид */
  async joinFleetGroup(): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) return;

    await this.connection.invoke('JoinFleetGroup', this.currentLocomotiveId);
    this.currentLocomotiveId = null;
    console.log('[SignalR] Переключено на fleet');
  }

  /** Отключиться */
  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      this.currentLocomotiveId = null;
      this.connectedSubject.next(false);
    }
  }

  ngOnDestroy(): void {
    this.disconnect();
    this.telemetrySubject.complete();
    this.fleetSubject.complete();
    this.alertSubject.complete();
    this.connectedSubject.complete();
  }

  private registerHandlers(): void {
    if (!this.connection) return;

    this.connection.on('ReceiveTelemetry', (snapshot: TelemetrySnapshot) => {
      this.telemetrySubject.next(snapshot);
    });

    this.connection.on('ReceiveFleet', (fleet: LocomotiveState[]) => {
      this.fleetSubject.next(fleet);
    });

    this.connection.on('ReceiveAlert', (alert: Alert) => {
      this.alertSubject.next(alert);
    });
  }

  private registerReconnectHandlers(): void {
    if (!this.connection) return;

    this.connection.onreconnecting(() => {
      console.log('[SignalR] Переподключение...');
      this.connectedSubject.next(false);
    });

    this.connection.onreconnected(() => {
      console.log('[SignalR] Переподключено');
      this.connectedSubject.next(true);
    });

    this.connection.onclose(() => {
      console.log('[SignalR] Соединение закрыто');
      this.connectedSubject.next(false);
    });
  }
}