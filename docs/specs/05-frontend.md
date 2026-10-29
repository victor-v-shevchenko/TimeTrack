# Step 5: Angular app — employees page, report page, tests

## Goal
A usable UI over the API: add employees, start and stop timers with a live elapsed time, and
view a report for a period, with an empty state.

## Depends on
Step 4 (the API must run on :5000 for manual checks; the tests don't need it).

## Setup

    cd /workspace
    npx @angular/cli@latest new frontend --style=css --routing --ssr=false --skip-git --skip-install
    cd frontend && npm install

Keep the CLI defaults: standalone, zoneless if it's the default, Vitest. Add scripts to `package.json`:
`"start": "ng serve --host 0.0.0.0"`. Set `serve.options.proxyConfig: "proxy.conf.json"` in
`angular.json` so the proxy applies without flags.

`proxy.conf.json`:

    { "/api": { "target": "http://localhost:5000", "secure": false } }

No UI component library. Plain CSS with a small `styles.css` (CSS variables, a readable table, form spacing).

## Files

    frontend/src/app/app.config.ts           # provideHttpClient(withFetch()), provideRouter
    frontend/src/app/app.routes.ts           # '' → 'employees', 'employees', 'report'
    frontend/src/app/app.ts / app.html       # header nav: Employees | Report
    frontend/src/app/core/models.ts
    frontend/src/app/core/api.service.ts
    frontend/src/app/core/time.ts            # pure functions: date ranges, formatting
    frontend/src/app/core/problem.ts         # extract message from HttpErrorResponse
    frontend/src/app/employees/employees.page.ts/.html/.css
    frontend/src/app/employees/employee-row.ts/.html     # one row: task input, start/stop, elapsed
    frontend/src/app/report/report.page.ts/.html/.css
    *.spec.ts next to each

## core/models.ts
TypeScript interfaces that match the step 3 contracts exactly. Timestamps are `string`
(ISO); `stoppedAt: string | null`; `activeEntry: ActiveEntry | null`.

## core/api.service.ts
`@Injectable({ providedIn: 'root' })` using `inject(HttpClient)`:
- `getEmployees(): Observable<Employee[]>`
- `createEmployee(name: string): Observable<Employee>`
- `startTimer(id: number, description: string): Observable<TimeEntry>`
- `stopTimer(id: number): Observable<TimeEntry>`
- `getReport(from: Date, to: Date, employeeId?: number): Observable<Report>`: sends
  `from.toISOString()` / `to.toISOString()` through `HttpParams`, which encodes them correctly.

## core/time.ts (pure, unit-tested)
- `toRange(fromDate: string, toDate: string): { from: Date; to: Date }`: input is `YYYY-MM-DD`
  from `<input type="date">`. `from` = local midnight of `fromDate`; `to` = local midnight of the
  day after `toDate` (exclusive). Build with `new Date(y, m - 1, d)` and `new Date(y, m - 1, d + 1)`,
  **not** `new Date('YYYY-MM-DD')` (that parses as UTC).
- `currentWeek(today = new Date()): { from: string; to: string }`: Monday to Sunday as `YYYY-MM-DD` local.
- `formatDuration(seconds: number): string` → `H:MM:SS` (hours can exceed 24).
- `elapsedSeconds(startedAt: string, now: number): number` → `max(0, floor((now - Date.parse(startedAt)) / 1000))`.

## core/problem.ts
`problemMessage(err: unknown): string`: for a `HttpErrorResponse` with a ProblemDetails body,
returns `errors` values joined, otherwise `detail ?? title`. For status 0 returns
"Server unreachable". Falls back to "Unexpected error".

## Employees page
- Signals: `employees`, `loading`, `error`, `newName`.
- On init: load employees.
- Add form: name input (required, maxlength 100, trimmed). Submit is disabled while invalid or
  saving. A server error (409/422) shows under the form. On success, append and clear the input.
- One `EmployeeRow` per employee (`@for … track e.id`):
  - Not running: description input (required, maxlength 500) and a **Start** button.
  - Running: shows the description, a live `H:MM:SS` elapsed time and a **Stop** button.
  - Buttons are disabled while a request is in flight. Errors show inline in the row.
  - After start or stop, emit an event; the page reloads the list (simple, and always consistent).
- Live clock: one `now` signal on the page, updated every second by `setInterval`, cleared in
  `DestroyRef.onDestroy`. Rows get `now` as an input; no interval per row.
- Empty list: "No employees yet. Add the first one above."

## Report page
- Form: `from` and `to` date inputs (default `currentWeek()`), an employee select ("All" plus
  employees) and a **Show** button. Also load on init.
- Client-side validation: `to` before `from` → inline message, no request.
- Entries table: Employee, Task, Started, Stopped ("running" if null), Duration. Format times
  in local time with Angular `DatePipe` (`'short'`).
- Totals table: Employee, Total (`formatDuration`).
- When `entries.length === 0`, show **"No records for this period"** instead of the entries table.
  Totals still show (with zeros).
- Errors show above the tables.

## Tests (Vitest via `ng test`)
- `api.service.spec.ts` (with `provideHttpClient()`, `provideHttpClientTesting()`):
  correct URLs, methods and bodies; `getReport` sends ISO params; `httpMock.verify()` afterwards.
- `time.spec.ts`: `toRange('2026-03-28','2026-03-29')` gives `to` = local 2026-03-30 00:00
  (including across a DST change: the difference is not assumed to be 48 h); `currentWeek` on a
  Sunday and on a Monday; `formatDuration(0)`, `(59)`, `(3600)`, `(90061)`; `elapsedSeconds`
  never negative.
- `problem.spec.ts`: validation errors, a 409 detail, status 0.
- `report.page.spec.ts`: with a stubbed `ApiService` returning `entries: []` → "No records for
  this period" is rendered and totals show `0:00:00`.
- `employee-row.spec.ts`: not running → Start is visible; click calls `startTimer`; with
  `activeEntry` set → Stop visible and elapsed shown; click calls `stopTimer`.

## Acceptance criteria
- [x] `npm test` is green; `npm run build` has no warnings about budgets or strict templates.
- [ ] Manual (API running in the container): add an employee, start, see the counter tick,
      stop, open the report with today in range, see the entry and total. A range in the
      future shows the empty state.
- [x] Opening and closing the Employees page repeatedly doesn't leave intervals running (DevTools check).
- [x] No `new Date('YYYY-MM-DD')` anywhere (`git grep`).

## Commit
`Step 5: Angular frontend`
