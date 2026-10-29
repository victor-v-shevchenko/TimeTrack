-- Fictional demo data. Idempotent: re-running adds nothing.
-- Run after migrations; see seed/README.md.
BEGIN;

INSERT INTO employees (name, created_at)
VALUES ('Alice Example',     now() - interval '30 days'),
       ('Bob Sample',        now() - interval '30 days'),
       ('Carol Testova',     now() - interval '30 days'),
       ('Dmytro Demo',       now() - interval '30 days'),
       ('Erin Placeholder',  now() - interval '30 days'),
       ('Frank Fictional',   now() - interval '30 days')
ON CONFLICT (name) DO NOTHING;

-- Frank Fictional is deliberately left without entries (shows a 0 total).
-- Entries go only to seeded employees that have none yet. One statement, so both
-- inserts see the same snapshot of "has no entries".
WITH targets AS (
    SELECT e.id, e.name, t.idx
    FROM employees e
    JOIN (VALUES ('Alice Example', 0), ('Bob Sample', 1), ('Carol Testova', 2),
                 ('Dmytro Demo', 3), ('Erin Placeholder', 4)) AS t(name, idx)
      ON t.name = e.name
    WHERE NOT EXISTS (SELECT 1 FROM time_entries te WHERE te.employee_id = e.id)
),
days AS (
    -- the last 10 weekdays, today included; n = 1 is the oldest
    SELECT day, row_number() OVER (ORDER BY day) AS n
    FROM (
        SELECT date_trunc('day', now()) - make_interval(days => g) AS day
        FROM generate_series(0, 20) AS g
        WHERE extract(isodow FROM date_trunc('day', now()) - make_interval(days => g)) < 6
        ORDER BY g
        LIMIT 10
    ) last_weekdays
),
descriptions AS (
    SELECT * FROM (VALUES
        (0, 'Sprint planning'),
        (1, 'Code review: report endpoint'),
        (2, 'Fix timezone bug in report'),
        (3, 'Daily standup and email'),
        (4, 'Write API tests for timers'),
        (5, 'Refactor employee service'),
        (6, 'Customer call: onboarding questions'),
        (7, 'Update documentation')
    ) AS x(k, description)
),
slots AS (
    -- (slot, start offset from midnight, duration)
    SELECT * FROM (VALUES
        (0, interval '9 hours',            interval '2 hours'),
        (1, interval '11 hours 30 minutes', interval '90 minutes'),
        (2, interval '14 hours',           interval '3 hours')
    ) AS s(slot, start_offset, duration)
),
planned AS (
    SELECT t.id AS employee_id,
           d.day + s.start_offset AS started_at,
           d.day + s.start_offset + s.duration AS stopped_at,
           ds.description
    FROM targets t
    CROSS JOIN days d
    CROSS JOIN slots s
    JOIN descriptions ds ON ds.k = (t.idx + d.n + s.slot) % 8
    -- 2 entries every day, a third on alternating days
    WHERE (s.slot < 2 OR (t.idx + d.n) % 2 = 0)
),
midnight AS (
    -- Bob works late once and crosses midnight: 22:30 to 00:45 the next day
    SELECT t.id AS employee_id,
           d.day + interval '22 hours 30 minutes' AS started_at,
           d.day + interval '24 hours 45 minutes' AS stopped_at,
           'Production hotfix and deploy' AS description
    FROM targets t
    JOIN days d ON d.n = 5
    WHERE t.name = 'Bob Sample'
),
history AS (
    INSERT INTO time_entries (employee_id, description, started_at, stopped_at)
    SELECT employee_id, description, started_at, stopped_at
    FROM planned
    -- skip entries that would still be in the future (e.g. later today)
    WHERE stopped_at <= now() - interval '30 minutes'
    UNION ALL
    SELECT employee_id, description, started_at, stopped_at FROM midnight
    RETURNING 1
)
INSERT INTO time_entries (employee_id, description, started_at, stopped_at)
SELECT id, 'Investigate slow report query', now() - interval '25 minutes', NULL
FROM targets
WHERE name = 'Alice Example';

COMMIT;
