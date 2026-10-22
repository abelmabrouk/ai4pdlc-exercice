import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'mes-demandes' },
  {
    path: 'mes-demandes',
    title: 'Mes demandes',
    loadComponent: () => import('./features/my-requests/my-requests-page').then((m) => m.MyRequestsPage),
  },
  {
    path: 'manager/demandes',
    title: 'Demandes en attente',
    loadComponent: () =>
      import('./features/pending-requests/pending-requests-page').then((m) => m.PendingRequestsPage),
  },
  { path: '**', redirectTo: 'mes-demandes' },
];
