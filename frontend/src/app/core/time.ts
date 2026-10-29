function parseDate(value: string): [number, number, number] {
  const [y, m, d] = value.split('-').map(Number);
  return [y, m, d];
}

function formatDate(date: Date): string {
  const mm = String(date.getMonth() + 1).padStart(2, '0');
  const dd = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${mm}-${dd}`;
}

/** `toDate` is inclusive in the UI; the returned `to` is exclusive (next local midnight). */
export function toRange(fromDate: string, toDate: string): { from: Date; to: Date } {
  const [fy, fm, fd] = parseDate(fromDate);
  const [ty, tm, td] = parseDate(toDate);
  return { from: new Date(fy, fm - 1, fd), to: new Date(ty, tm - 1, td + 1) };
}

/** Monday to Sunday of the week containing `today`, as local `YYYY-MM-DD`. */
export function currentWeek(today = new Date()): { from: string; to: string } {
  const sinceMonday = (today.getDay() + 6) % 7;
  const monday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - sinceMonday);
  const sunday = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + 6);
  return { from: formatDate(monday), to: formatDate(sunday) };
}

/** `H:MM:SS`; hours may exceed 24. */
export function formatDuration(seconds: number): string {
  const total = Math.max(0, Math.floor(seconds));
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

export function elapsedSeconds(startedAt: string, now: number): number {
  return Math.max(0, Math.floor((now - Date.parse(startedAt)) / 1000));
}

/** Instant as the UTC ISO string the API expects. */
export function toUtcIso(date: Date): string {
  return date.toISOString();
}
