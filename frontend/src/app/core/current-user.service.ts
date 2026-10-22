import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Employee } from './api.models';

const STORAGE_KEY = 'conges.userId';
const DEFAULT_USER_ID = 2;

/**
 * Identifiant de l'utilisateur « connecté » (simulation pour l'atelier), mémorisé dans le localStorage.
 * Sans dépendance : l'interceptor HTTP l'injecte pour poser l'en-tête X-User-Id.
 */
@Injectable({ providedIn: 'root' })
export class UserSession {
  readonly userId = signal<number>(readStoredUserId());

  switchTo(id: number): void {
    this.userId.set(id);
    try {
      localStorage.setItem(STORAGE_KEY, String(id));
    } catch {
      // stockage indisponible : on garde la valeur en mémoire
    }
  }
}

/**
 * Utilisateur courant et liste des employés (sélecteur « Connecté en tant que » de l'en-tête).
 */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly http = inject(HttpClient);
  private readonly session = inject(UserSession);

  readonly employees = toSignal(this.http.get<Employee[]>('/api/employees'), { initialValue: [] });
  readonly userId = this.session.userId.asReadonly();
  readonly user = computed(() => this.employees().find((e) => e.id === this.userId()) ?? null);

  switchTo(id: number): void {
    this.session.switchTo(id);
  }
}

function readStoredUserId(): number {
  try {
    const stored = Number(localStorage.getItem(STORAGE_KEY));
    return Number.isInteger(stored) && stored > 0 ? stored : DEFAULT_USER_ID;
  } catch {
    return DEFAULT_USER_ID;
  }
}
