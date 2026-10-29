import { Routes } from '@angular/router';
import { EmployeesPage } from './employees/employees.page';
import { ReportPage } from './report/report.page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'employees' },
  { path: 'employees', component: EmployeesPage },
  { path: 'report', component: ReportPage },
];
