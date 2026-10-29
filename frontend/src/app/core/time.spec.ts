import { currentWeek, elapsedSeconds, formatDuration, toRange } from './time';

describe('toRange', () => {
  it('returns local midnight of the start and the day after the end', () => {
    const { from, to } = toRange('2026-03-28', '2026-03-29');
    expect(from).toEqual(new Date(2026, 2, 28));
    expect(to).toEqual(new Date(2026, 2, 30));
  });

  it('does not assume 24 h days across a DST change', () => {
    const { from, to } = toRange('2026-03-28', '2026-03-29');
    expect(to.getFullYear()).toBe(2026);
    expect(to.getMonth()).toBe(2);
    expect(to.getDate()).toBe(30);
    expect(to.getHours()).toBe(0);
    expect(from.getHours()).toBe(0);
  });

  it('crosses month and year boundaries', () => {
    const { to } = toRange('2026-12-30', '2026-12-31');
    expect(to).toEqual(new Date(2027, 0, 1));
  });
});

describe('currentWeek', () => {
  it('on a Monday starts that day', () => {
    expect(currentWeek(new Date(2026, 0, 5))).toEqual({ from: '2026-01-05', to: '2026-01-11' });
  });

  it('on a Sunday ends that day', () => {
    expect(currentWeek(new Date(2026, 0, 11, 23, 30))).toEqual({
      from: '2026-01-05',
      to: '2026-01-11',
    });
  });

  it('can span two months', () => {
    expect(currentWeek(new Date(2026, 1, 1))).toEqual({ from: '2026-01-26', to: '2026-02-01' });
  });
});

describe('formatDuration', () => {
  it.each([
    [0, '0:00:00'],
    [59, '0:00:59'],
    [3600, '1:00:00'],
    [90061, '25:01:01'],
  ])('formats %i s as %s', (seconds, expected) => {
    expect(formatDuration(seconds)).toBe(expected);
  });
});

describe('elapsedSeconds', () => {
  const startedAt = '2026-01-05T09:00:00Z';
  const start = Date.parse(startedAt);

  it('floors to whole seconds', () => {
    expect(elapsedSeconds(startedAt, start + 61_999)).toBe(61);
  });

  it('is never negative', () => {
    expect(elapsedSeconds(startedAt, start - 5000)).toBe(0);
  });
});
