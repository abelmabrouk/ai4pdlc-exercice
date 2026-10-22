import { Component, computed, effect, input, output, signal } from '@angular/core';

/**
 * Dialogue de confirmation du design system, avec champ « motif » optionnel.
 *
 * <app-confirm-dialog
 *   [open]="dialogOpen()"
 *   title="Refuser la demande"
 *   message="…"
 *   confirmLabel="Refuser"
 *   tone="danger"
 *   reasonLabel="Motif du refus"
 *   [reasonMinLength]="10"
 *   (confirmed)="onConfirm($event)"
 *   (cancelled)="dialogOpen.set(false)" />
 */
@Component({
  selector: 'app-confirm-dialog',
  templateUrl: './confirm-dialog.html',
  styleUrl: './confirm-dialog.css',
  host: { '(document:keydown.escape)': 'open() && cancel()' },
})
export class ConfirmDialog {
  readonly open = input(false);
  readonly title = input.required<string>();
  readonly message = input<string>();
  readonly confirmLabel = input('Confirmer');
  readonly cancelLabel = input('Annuler');
  readonly tone = input<'default' | 'danger'>('default');
  /** Si renseigné, affiche un champ texte « motif ». */
  readonly reasonLabel = input<string>();
  /** Longueur minimale du motif (espaces de début et de fin ignorés). */
  readonly reasonMinLength = input(0);
  /** Désactive les boutons pendant un traitement. */
  readonly busy = input(false);

  /** Émis avec le motif saisi (chaîne vide si aucun champ motif). */
  readonly confirmed = output<string>();
  readonly cancelled = output<void>();

  protected readonly reason = signal('');
  protected readonly reasonLength = computed(() => this.reason().trim().length);
  protected readonly canConfirm = computed(
    () => !this.busy() && (!this.reasonLabel() || this.reasonLength() >= this.reasonMinLength()),
  );

  constructor() {
    effect(() => {
      if (this.open()) this.reason.set('');
    });
  }

  protected confirm(): void {
    if (this.canConfirm()) this.confirmed.emit(this.reason().trim());
  }

  protected cancel(): void {
    if (!this.busy()) this.cancelled.emit();
  }
}
