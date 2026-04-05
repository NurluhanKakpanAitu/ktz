import { Component, EventEmitter, Input, Output } from '@angular/core';

export type ReplayStatus = 'idle' | 'playing' | 'paused';

@Component({
  selector: 'app-replay-controls',
  templateUrl: './replay-controls.component.html',
  styleUrls: ['./replay-controls.component.css']
})
export class ReplayControlsComponent {
  /** Текущее состояние проигрывателя */
  @Input() status: ReplayStatus = 'idle';
  /** Выбранное окно в минутах */
  @Input() minutes: 5 | 10 | 15 = 10;
  /** Скорость воспроизведения */
  @Input() speed: 1 | 2 | 5 = 1;
  /** Текущая позиция (индекс точки) */
  @Input() position = 0;
  /** Всего точек в замере */
  @Input() total = 0;
  /** Идёт ли загрузка реплея */
  @Input() loading = false;

  @Output() selectMinutes = new EventEmitter<5 | 10 | 15>();
  @Output() play = new EventEmitter<void>();
  @Output() pause = new EventEmitter<void>();
  @Output() stop = new EventEmitter<void>();
  @Output() selectSpeed = new EventEmitter<1 | 2 | 5>();
  @Output() seek = new EventEmitter<number>();

  readonly windows: Array<5 | 10 | 15> = [5, 10, 15];
  readonly speeds: Array<1 | 2 | 5> = [1, 2, 5];

  onScrub(event: Event): void {
    const v = Number((event.target as HTMLInputElement).value);
    this.seek.emit(v);
  }
}
