import { Component, OnDestroy, OnInit } from '@angular/core';
import { Subscription } from 'rxjs';
import { ConnectionState, TelemetryService } from '../../services/telemetry.service';

@Component({
  selector: 'app-connection-status',
  templateUrl: './connection-status.component.html',
  styleUrls: ['./connection-status.component.css']
})
export class ConnectionStatusComponent implements OnInit, OnDestroy {
  state: ConnectionState = 'disconnected';
  private sub?: Subscription;

  constructor(private telemetry: TelemetryService) {}

  ngOnInit(): void {
    this.sub = this.telemetry.connectionState$.subscribe(s => this.state = s);
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
  }

  get label(): string {
    switch (this.state) {
      case 'connected':    return 'Live';
      case 'reconnecting': return 'Переподключение...';
      case 'disconnected': return 'Нет связи';
    }
  }

  get icon(): string {
    switch (this.state) {
      case 'connected':    return '🟢';
      case 'reconnecting': return '🟡';
      case 'disconnected': return '🔴';
    }
  }
}
