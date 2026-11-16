import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.getAccessToken();

  const cloned = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(cloned).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401 && !req.url.includes('auth/login') && !req.url.includes('auth/refresh')) {
        const refresh = auth.getRefreshToken();
        if (refresh) {
          return auth.refreshToken().pipe(
            switchMap((res) => {
              const retried = req.clone({ setHeaders: { Authorization: `Bearer ${res.accessToken}` } });
              return next(retried);
            }),
            catchError((refreshErr) => {
              auth.logout();
              return throwError(() => refreshErr);
            })
          );
        }
        auth.logout();
      }
      return throwError(() => err);
    })
  );
};

