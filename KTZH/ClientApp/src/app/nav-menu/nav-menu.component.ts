import { Component, OnInit, OnDestroy, HostListener } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { TelemetryService } from '../services/telemetry.service';
import { Alert } from '../models/telemetry.models';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-nav-menu',
  templateUrl: './nav-menu.component.html',
  styleUrls: ['./nav-menu.component.css']
})
export class NavMenuComponent implements OnInit, OnDestroy {

  alerts: Alert[] = [];
  unreadCount = 0;
  showDropdown = false;

  private alertSub?: Subscription;

  constructor(
    public auth: AuthService,
    private telemetry: TelemetryService
  ) {}

  ngOnInit(): void {
    this.alertSub = this.telemetry.alert$.subscribe(alert => {
      this.alerts.unshift(alert);
      if (this.alerts.length > 30) this.alerts.pop();
      this.unreadCount++;
    });
  }

  ngOnDestroy(): void {
    this.alertSub?.unsubscribe();
  }

  toggleDropdown(): void {
    this.showDropdown = !this.showDropdown;
    if (this.showDropdown) {
      this.unreadCount = 0;
    }
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.alert-bell-wrap')) {
      this.showDropdown = false;
    }
  }

  logout(): void {
    this.auth.logout();
  }
}
