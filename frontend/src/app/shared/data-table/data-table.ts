import { NgTemplateOutlet } from '@angular/common';
import { Component, Directive, TemplateRef, computed, contentChildren, inject, input } from '@angular/core';

export interface DataTableColumn<T> {
  /** Identifiant de la colonne ; sert aussi de clé pour un template de cellule (appCell). */
  key: string;
  header: string;
  /** Valeur texte de la cellule (ignorée si un template appCell existe pour cette clé). */
  value?: (row: T) => string | number | null | undefined;
  align?: 'start' | 'end' | 'center';
  width?: string;
}

/**
 * Template de cellule personnalisé :
 * <ng-template appCell="status" let-row> <app-status-badge [status]="row.status" /> </ng-template>
 */
@Directive({ selector: 'ng-template[appCell]' })
export class DataTableCell {
  readonly key = input.required<string>({ alias: 'appCell' });
  readonly template = inject<TemplateRef<{ $implicit: unknown }>>(TemplateRef);
}

/**
 * Tableau de données du design system.
 */
@Component({
  selector: 'app-data-table',
  imports: [NgTemplateOutlet],
  templateUrl: './data-table.html',
  styleUrl: './data-table.css',
})
export class DataTable<T> {
  readonly columns = input.required<DataTableColumn<T>[]>();
  readonly rows = input.required<readonly T[]>();
  /** Identifiant stable de ligne (suivi @for). */
  readonly trackBy = input.required<(row: T) => string | number>();
  /** data-testid posé sur chaque <tr> (ex. row => 'leave-row-' + row.id). */
  readonly rowTestId = input<(row: T) => string>();
  /** Légende accessible du tableau. */
  readonly caption = input<string>();

  private readonly cells = contentChildren(DataTableCell);
  protected readonly templates = computed(
    () => new Map(this.cells().map((cell) => [cell.key(), cell.template])),
  );

  protected cellValue(column: DataTableColumn<T>, row: T): string {
    const value = column.value
      ? column.value(row)
      : (row as Record<string, unknown>)[column.key];
    return value === null || value === undefined ? '' : String(value);
  }
}
