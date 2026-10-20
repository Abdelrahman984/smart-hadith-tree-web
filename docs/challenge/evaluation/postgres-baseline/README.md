# SQL Server baseline for the PostgreSQL move

Phase 0 of `docs/postgres_migration.md`, taken on 2026-10-21 against `SmartHadithTree_ShamelaV7` (the v7 backup,
all 11 SQL Server migrations applied), API built from tag `pre-postgres` (`f1c6493`) in Release on `:5147`.

| File | What | How to compare after the move |
|---|---|---|
| `search-sqlserver.json` / `.log` | `scripts/eval_search.py` (21 cases, no `--judge`) | `python scripts/eval_search.py --compare docs/challenge/evaluation/postgres-baseline/search-sqlserver.json` |
| `verify-sqlserver.json` / `.log` | `scripts/eval_verify.py` (29 cases, 1 run) | `python scripts/eval_verify.py --compare docs/challenge/evaluation/postgres-baseline/verify-sqlserver.json` |
| `book-counts-sqlserver.csv` | Per book (`NormalizedBookName`): hadiths, matn chars, isnad chars, transmissions, sum of `StepOrder` | Same query on PostgreSQL (`char_length` for the char sums), then diff the CSVs |
| `snapshot-cases.json` | 36 hadiths keyed by book/chapter/number (31 = first hadith with ≥ 4 transmissions per book, 5 = tahwil with 2–4 step-1 rows) and 20 narrator queries (10 names, with and without tashkeel) | Input of `scripts/snapshot_api.py` |
| `snapshot-sqlserver.json` | `/tree`, `/takhreej/related`, `/ilal`, `/takhreej?ids=` (hadith + 5 related) per case, `/narrators/search` per query | `python scripts/snapshot_api.py --compare docs/challenge/evaluation/postgres-baseline/snapshot-sqlserver.json <new>.json` |

## Numbers

**Tables** (identical to the v7 row of `backups/README.md`): Hadiths 274,597 · Transmissions 1,266,022 ·
Narrators 44,282 · ScholarEvaluations 131,994 · NarratorRelations 153,519 · MukhtalitHearings 168 ·
MukhtalitGroupRules 76 · HadithClusters 0 · mudallisin (`IsMudallis`) 165 · 31 books.

**Search**: hit@1 15/18, hit@3 16/18, hit@10 16/18, MRR 0.866, top-result precision 0.93 (3 cases).
Latency median 1,253 ms, max 1,667 ms.

**Verify**: 29/29 passed, 8/8 negatives abstained, 0 false attributions; the model reviewed 6/29 cases.
Latency median 809 ms, max 15,047 ms (model calls).

**API snapshot latency** (ms, median / max): tree 22 / 48 · related 1,532 / 8,353 · ilal 1,441 / 9,883 ·
comparative 39 / 1,072 · narrator search 162 / 240. The max values in the first run are cold cache; a second run
had related 1,403 / 3,203 and ilal 1,471 / 2,980.

## Already non-deterministic on SQL Server

Two snapshots of the **same** database differ in one place: the Shudhudh finding of مسند أحمد #2 lists the narrators
who omit an addition («ولم يذكرها …») in a different order (`MatnAtMadarRule`, `Describe(context, omitter)`; the order
comes from database row order without an `ORDER BY`). Everything else (tree, related, comparative, narrator search for
all cases) matched across the two runs. Expect this diff after the move too; fix it with an explicit order
(phase 4, "ordering ties") rather than ignoring it.
