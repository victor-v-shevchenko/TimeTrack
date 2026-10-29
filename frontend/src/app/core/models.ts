export interface ActiveEntry {
  id: number;
  description: string;
  startedAt: string;
}

export interface Employee {
  id: number;
  name: string;
  createdAt: string;
  activeEntry: ActiveEntry | null;
}

export interface TimeEntry {
  id: number;
  employeeId: number;
  description: string;
  startedAt: string;
  stoppedAt: string | null;
}

export interface ReportEntry {
  id: number;
  employeeId: number;
  employeeName: string;
  description: string;
  startedAt: string;
  stoppedAt: string | null;
  durationSeconds: number;
}

export interface ReportTotal {
  employeeId: number;
  employeeName: string;
  totalSeconds: number;
}

export interface Report {
  from: string;
  to: string;
  entries: ReportEntry[];
  totals: ReportTotal[];
}
