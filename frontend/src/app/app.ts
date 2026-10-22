import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { CurrentUserService } from './core/current-user.service';
import { Toast } from './shared/toast/toast';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Toast],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  protected readonly currentUser = inject(CurrentUserService);

  protected onUserChange(event: Event): void {
    this.currentUser.switchTo(Number((event.target as HTMLSelectElement).value));
  }
}
