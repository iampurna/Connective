import { HttpClient } from '@angular/common/http';
import { inject, Inject, Injectable } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { ApiResponse } from '../modules/api-response';

@Injectable({
  providedIn: 'root',
})
export class AuthserviceService {
  private baseUrl: string = 'http://localhost:5000/api/account';
  httpClient = inject(HttpClient);
  register(data: FormData): Observable<ApiResponse<string>> {
    return this.httpClient
      .post<ApiResponse<string>>(`${this.baseUrl}/register`, data)
      .pipe(
        tap((response) => {
          localStorage.setItem('token', response.data);
        })
      );
  }
}
