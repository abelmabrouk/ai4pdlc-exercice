import { Injectable, signal } from '@angular/core';

export type ToastKind = 'success' | 'error' | 'info';

export interface ToastMessage {
  id: number;
  kind: ToastKind;
  message: string;
}

const DISPLAY_DURATION_MS = 5000;

/**
 * Notifications éphémères.
 */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<ToastMessage[]>([]);

  success(message: string): void {
    this.show('success', message);
  }

  error(message: string): void {
    this.show('error', message);
  }

  info(message: string): void {
    this.show('info', message);
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(kind: ToastKind, message: string): void {
    const toast: ToastMessage = { id: this.nextId++, kind, message };
    this.toasts.update((list) => [...list, toast]);
    setTimeout(() => this.dismiss(toast.id), DISPLAY_DURATION_MS);
  }
}
