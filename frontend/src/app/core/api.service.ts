import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Employee, Report, TimeEntry } from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  getEmployees(): Observable<Employee[]> {
    return this.http.get<Employee[]>('/api/employees');
  }

  createEmployee(name: string): Observable<Employee> {
    return this.http.post<Employee>('/api/employees', { name });
  }

  startTimer(id: number, description: string): Observable<TimeEntry> {
    return this.http.post<TimeEntry>(`/api/employees/${id}/timer/start`, { description });
  }

  stopTimer(id: number): Observable<TimeEntry> {
    return this.http.post<TimeEntry>(`/api/employees/${id}/timer/stop`, null);
  }

  getReport(from: Date, to: Date, employeeId?: number): Observable<Report> {
    let params = new HttpParams().set('from', from.toISOString()).set('to', to.toISOString());
    if (employeeId !== undefined) {
      params = params.set('employeeId', employeeId);
    }
    return this.http.get<Report>('/api/reports', { params });
  }
}
