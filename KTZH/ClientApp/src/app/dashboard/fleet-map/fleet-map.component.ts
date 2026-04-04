import { Component, OnInit, OnDestroy, AfterViewInit } from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import * as L from 'leaflet';
import { TelemetryService } from '../../services/telemetry.service';
import { ApiService } from '../../services/api.service';
import { LocomotiveState, LocomotiveDto } from '../../models/telemetry.models';

const GRADE_COLORS: Record<string, string> = {
  A: '#22c55e',
  B: '#84cc16',
  C: '#f59e0b',
  D: '#f97316',
  E: '#ef4444'
};

@Component({
  selector: 'app-fleet-map',
  templateUrl: './fleet-map.component.html',
  styleUrls: ['./fleet-map.component.css']
})
export class FleetMapComponent implements OnInit, AfterViewInit, OnDestroy {

  private map!: L.Map;
  private markers = new Map<string, L.CircleMarker>();
  private fleetSub?: Subscription;

  constructor(
    private telemetry: TelemetryService,
    private api: ApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.telemetry.connect();
  }

  ngAfterViewInit(): void {
    this.initMap();
    this.loadInitialData();
    this.subscribeToFleet();
  }

  ngOnDestroy(): void {
    this.fleetSub?.unsubscribe();
  }

  private initMap(): void {
    this.map = L.map('fleet-map', {
      center: [48.0, 68.0],
      zoom: 5,
      zoomControl: true
    });

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; OpenStreetMap contributors'
    }).addTo(this.map);
  }

  private loadInitialData(): void {
    this.api.getLocomotives().subscribe(locos => {
      locos.forEach(loco => this.upsertMarker(loco));
      // Fix map rendering after DOM init
      setTimeout(() => this.map.invalidateSize(), 100);
    });
  }

  private subscribeToFleet(): void {
    this.fleetSub = this.telemetry.fleet$.subscribe(fleet => {
      fleet.forEach(state => {
        this.upsertMarkerFromState(state);
      });
    });
  }

  private upsertMarker(loco: LocomotiveDto): void {
    const color = GRADE_COLORS[loco.healthGrade] || '#6b7280';
    const existing = this.markers.get(loco.id);

    if (existing) {
      existing.setLatLng([loco.latitude, loco.longitude]);
      existing.setStyle({ fillColor: color, color: color });
      existing.setPopupContent(this.popupHtml(loco.name, loco.route, loco.healthScore, loco.healthGrade));
    } else {
      const marker = L.circleMarker([loco.latitude, loco.longitude], {
        radius: 10,
        fillColor: color,
        color: color,
        weight: 2,
        opacity: 1,
        fillOpacity: 0.8
      }).addTo(this.map);

      marker.bindPopup(this.popupHtml(loco.name, loco.route, loco.healthScore, loco.healthGrade));
      marker.on('click', () => {
        this.router.navigate(['/locomotive', loco.id]);
      });

      this.markers.set(loco.id, marker);
    }
  }

  private upsertMarkerFromState(state: LocomotiveState): void {
    const loco = state.locomotive;
    const health = state.lastHealth;
    const color = GRADE_COLORS[health.grade] || '#6b7280';
    const existing = this.markers.get(loco.id);

    if (existing) {
      existing.setLatLng([loco.latitude, loco.longitude]);
      existing.setStyle({ fillColor: color, color: color });
      existing.setPopupContent(this.popupHtml(loco.name, loco.currentRoute, health.score, health.grade));
    } else {
      const marker = L.circleMarker([loco.latitude, loco.longitude], {
        radius: 10,
        fillColor: color,
        color: color,
        weight: 2,
        opacity: 1,
        fillOpacity: 0.8
      }).addTo(this.map);

      marker.bindPopup(this.popupHtml(loco.name, loco.currentRoute, health.score, health.grade));
      marker.on('click', () => {
        this.router.navigate(['/locomotive', loco.id]);
      });

      this.markers.set(loco.id, marker);
    }
  }

  private popupHtml(name: string, route: string, score: number, grade: string): string {
    const color = GRADE_COLORS[grade] || '#6b7280';
    return `
      <div style="font-family: sans-serif; min-width: 160px;">
        <strong>${name}</strong><br>
        <span style="color: #94a3b8;">${route}</span><br>
        <span style="color: ${color}; font-weight: bold; font-size: 1.1em;">
          ${score} / ${grade}
        </span>
      </div>
    `;
  }
}