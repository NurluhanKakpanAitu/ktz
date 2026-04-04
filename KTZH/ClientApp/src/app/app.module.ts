import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { RouterModule } from '@angular/router';

import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { HomeComponent } from './home/home.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { FleetMapComponent } from './dashboard/fleet-map/fleet-map.component';
import { FleetListComponent } from './dashboard/fleet-list/fleet-list.component';
import { AlertPanelComponent } from './dashboard/alert-panel/alert-panel.component';
import { HealthGaugeComponent } from './shared/health-gauge/health-gauge.component';
import { TelemetryGridComponent } from './locomotive/telemetry-grid/telemetry-grid.component';
import { TrendChartComponent } from './locomotive/trend-chart/trend-chart.component';
import { TrendChartsComponent } from './locomotive/trend-chart/trend-charts.component';
import { LocomotiveDetailComponent } from './locomotive/detail/locomotive-detail.component';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    HomeComponent,
    DashboardComponent,
    FleetMapComponent,
    FleetListComponent,
    AlertPanelComponent,
    HealthGaugeComponent,
    TelemetryGridComponent,
    TrendChartComponent,
    TrendChartsComponent,
    LocomotiveDetailComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    HttpClientModule,
    FormsModule,
    RouterModule.forRoot([
      { path: '', component: DashboardComponent, pathMatch: 'full' },
      { path: 'locomotive/:id', component: LocomotiveDetailComponent },
    ])
  ],
  providers: [],
  bootstrap: [AppComponent]
})
export class AppModule { }