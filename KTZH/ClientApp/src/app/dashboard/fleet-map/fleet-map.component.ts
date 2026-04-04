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

function createTrainIcon(color: string): L.DivIcon {
  return L.divIcon({
    className: 'train-marker',
    iconSize: [40, 40],
    iconAnchor: [20, 20],
    popupAnchor: [0, -22],
    html: `
      <div style="
        width: 40px; height: 40px;
        border-radius: 50%;
        border: 3px solid ${color};
        background: rgba(26, 37, 53, 0.0);
        display: flex;
        align-items: center;
        justify-content: center;
        box-shadow: 0 0 8px ${color}80;
      ">
        <img src="assets/train-icon.png" style="width: 22px; height: 22px;" />
      </div>
    `
  });
}

interface MarkerData {
  id: string;
  name: string;
  type: string;
  depotCity: string;
  route: string;
  grade: string;
  score: number;
}

@Component({
  selector: 'app-fleet-map',
  templateUrl: './fleet-map.component.html',
  styleUrls: ['./fleet-map.component.css']
})
export class FleetMapComponent implements OnInit, AfterViewInit, OnDestroy {

  searchQuery = '';
  filterType = '';
  filterGrade = '';

  private map!: L.Map;
  private markers = new Map<string, L.Marker>();
  private markerData = new Map<string, MarkerData>();
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

  applyFilters(): void {
    this.markers.forEach((marker, id) => {
      const data = this.markerData.get(id);
      if (!data) return;

      const visible = this.matchesFilter(data);
      if (visible) {
        if (!this.map.hasLayer(marker)) marker.addTo(this.map);
      } else {
        if (this.map.hasLayer(marker)) this.map.removeLayer(marker);
      }
    });
  }

  private matchesFilter(data: MarkerData): boolean {
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase();
      const match = data.name.toLowerCase().includes(q) ||
        data.route.toLowerCase().includes(q) ||
        data.depotCity.toLowerCase().includes(q) ||
        data.id.toLowerCase().includes(q);
      if (!match) return false;
    }
    if (this.filterType && data.type !== this.filterType) return false;
    if (this.filterGrade && data.grade !== this.filterGrade) return false;
    return true;
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
      setTimeout(() => this.map.invalidateSize(), 100);
    });
  }

  private subscribeToFleet(): void {
    this.fleetSub = this.telemetry.fleet$.subscribe(fleet => {
      fleet.forEach(state => this.upsertMarkerFromState(state));
    });
  }

  private upsertMarker(loco: LocomotiveDto): void {
    const color = GRADE_COLORS[loco.healthGrade] || '#6b7280';
    const data: MarkerData = {
      id: loco.id, name: loco.name, type: loco.type,
      depotCity: loco.depotCity, route: loco.route,
      grade: loco.healthGrade, score: loco.healthScore
    };
    this.markerData.set(loco.id, data);

    const existing = this.markers.get(loco.id);
    if (existing) {
      existing.setLatLng([loco.latitude, loco.longitude]);
      existing.setIcon(createTrainIcon(color));
      existing.setPopupContent(this.popupHtml(loco.name, loco.route, loco.healthScore, loco.healthGrade));
    } else {
      const marker = L.marker([loco.latitude, loco.longitude], {
        icon: createTrainIcon(color)
      });

      marker.bindPopup(this.popupHtml(loco.name, loco.route, loco.healthScore, loco.healthGrade));
      marker.on('click', () => this.router.navigate(['/locomotive', loco.id]));

      this.markers.set(loco.id, marker);
      if (this.matchesFilter(data)) marker.addTo(this.map);
    }
  }

  private upsertMarkerFromState(state: LocomotiveState): void {
    const loco = state.locomotive;
    const health = state.lastHealth;
    const color = GRADE_COLORS[health.grade] || '#6b7280';

    const data: MarkerData = {
      id: loco.id, name: loco.name, type: loco.type,
      depotCity: loco.depotCity || '', route: loco.currentRoute,
      grade: health.grade, score: health.score
    };
    this.markerData.set(loco.id, data);

    const existing = this.markers.get(loco.id);
    if (existing) {
      existing.setLatLng([loco.latitude, loco.longitude]);
      existing.setIcon(createTrainIcon(color));
      existing.setPopupContent(this.popupHtml(loco.name, loco.currentRoute, health.score, health.grade));

      const visible = this.matchesFilter(data);
      if (visible && !this.map.hasLayer(existing)) existing.addTo(this.map);
      if (!visible && this.map.hasLayer(existing)) this.map.removeLayer(existing);
    } else {
      const marker = L.marker([loco.latitude, loco.longitude], {
        icon: createTrainIcon(color)
      });

      marker.bindPopup(this.popupHtml(loco.name, loco.currentRoute, health.score, health.grade));
      marker.on('click', () => this.router.navigate(['/locomotive', loco.id]));

      this.markers.set(loco.id, marker);
      if (this.matchesFilter(data)) marker.addTo(this.map);
    }
  }

  private popupHtml(name: string, route: string, score: number, grade: string): string {
    const color = GRADE_COLORS[grade] || '#6b7280';
    return `
      <div style="font-family: sans-serif; min-width: 160px;">
        <strong>${name}</strong><br>
        <span style="color: #666;">${route}</span><br>
        <span style="color: ${color}; font-weight: bold; font-size: 1.1em;">
          ${score} / ${grade}
        </span>
      </div>
    `;
  }
}
