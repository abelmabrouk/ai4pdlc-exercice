import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { UserSession } from './current-user.service';

export const userIdInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/')) {
    return next(req);
  }
  const userId = inject(UserSession).userId();
  return next(req.clone({ setHeaders: { 'X-User-Id': String(userId) } }));
};
