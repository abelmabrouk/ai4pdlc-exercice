import { Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

/**
 * Zone d'affichage des toasts. Déjà placée dans le shell de l'application (app.ts) :
 * les écrans utilisent uniquement ToastService.
 */
@Component({
  selector: 'app-toast',
  template: `
    <div class="toasts" aria-live="polite">
      @for (toast of toastService.toasts(); track toast.id) {
        <div
          class="toast"
          [class]="'toast toast--' + toast.kind"
          role="status"
          data-testid="toast"
          [attr.data-kind]="toast.kind"
        >
          <span class="toast__message">{{ toast.message }}</span>
          <button type="button" class="toast__close" aria-label="Fermer" (click)="toastService.dismiss(toast.id)">×</button>
        </div>
      }
    </div>
  `,
  styleUrl: './toast.css',
})
export class Toast {
  protected readonly toastService = inject(ToastService);
}
