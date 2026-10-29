# AI log

Bugs and wrong output produced by AI tools, how they were found, and how they were fixed.

## Template
### <short title>
- **Step / commit:** <step N, hash>
- **Tool / model:** Claude Code, Opus 5.5 | /code-review, Sonnet 5.5
- **What the AI produced:** <code or behavior>
- **How it was found:** failing test | /code-review | manual check
- **Fix:** <what changed, commit link>

### Seed script numbered weekdays before applying LIMIT
- **Step / commit:** step 7, `Step 7: Seed data`
- **Tool / model:** Claude Code, Sonnet 5.5
- **What the AI produced:** `days` CTE computed `row_number()` over all 15 candidate weekdays in the same
  query as `ORDER BY … LIMIT 10`, so `n = 5` (used for the midnight-crossing entry) did not point at
  one of the 10 selected days and the entry was silently not inserted.
- **How it was found:** manual check — querying for entries with `stopped_at::date <> started_at::date`
  after the first run returned only Alice's running entry.
- **Fix:** select the 10 days in a subquery and number them in the outer query (same commit).
