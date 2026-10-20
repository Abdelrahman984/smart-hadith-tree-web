# Plan: replace SQL Server with PostgreSQL

Status: **phase 0 done** (2026-10-21): tag `pre-postgres` on `f1c6493`, baselines in
`docs/challenge/evaluation/postgres-baseline/` (see its README). No code changed yet.

## Why it is a contained change

Clean Architecture keeps the database behind `IHadithTreeDbContext` / `IHadithChainRepository`, so the
SQL Server footprint is small. A full survey of the repository found these touchpoints and nothing else:

| Area | What is SQL Server–specific | Files |
|---|---|---|
| Provider wiring | `UseSqlServer(...)`, `EnableRetryOnFailure`, `SqlConnectionStringBuilder` | `src/SmartHadithTree.Api/Program.cs`, `src/SmartHadithTree.Etl/Program.cs`, both `appsettings.json` |
| Collations | `Arabic_100_CI_AI` (database default + 27 columns), `Arabic_100_BIN2` (`NormalizedMatn`, `NormalizedBookName`, `FullIsnadText`) | `HadithTreeDbContext.cs`, `Data/Configurations/*.cs` |
| Filtered index | `.HasFilter("[SourceKey] IS NOT NULL")` (bracket quoting) | `NarratorConfiguration.cs` |
| Raw SQL | Two recursive CTEs in T-SQL (`UNIQUEIDENTIFIER`, `NVARCHAR`, `BIT`, `NEWID()`, unquoted PascalCase names, no `RECURSIVE`) | `Data/Repositories/HadithChainRepository.cs` |
| Migrations | 11 migrations full of `nvarchar`/collation; `AddIlalEngine` has hand-written T-SQL (`COL_LENGTH`, `sys.indexes`, `OBJECT_ID`) | `Data/Migrations/*` |
| Bulk load | `EFCore.BulkExtensions` with `SqlBulkCopyOptions` (CheckConstraints, TableLock, KeepIdentity) | `src/SmartHadithTree.Etl/Services/BulkDataIngestionService.cs` |
| Ops | `mssql/server:2022` image, `sqlcmd` healthcheck, `BACKUP DATABASE` pre-deploy, `.bak` backups v1–v7 | `docker-compose*.yml`, `deploy/deploy-remote.sh`, `deploy/*.md`, `backups/` |
| Side tools | `sqlcmd` exports, `IlalCompare` uses `UseSqlServer` | `scripts/shamela4-extractor/rijal_pilot/export_chains.ps1`, `phase6/export_db_chains.ps1`, `phase6/IlalCompare/` |
| Docs | Mentions of SQL Server, `.bak` restore | `CLAUDE.md`, `README.md`, `docs/architecture.md`, `backups/README.md`, `deploy/*` |

Not affected: Domain, almost all of Application (LINQ only), the Python resolver, the frontend, and the
unit tests (they use the EF InMemory provider, so they also give **no coverage** of the raw SQL or of
query translation; see phase 5).

## Decisions

1. **PostgreSQL 18** (`postgres:18` image) with `Npgsql.EntityFrameworkCore.PostgreSQL` 9.x (matches EF Core 9).
   PG 18 is the first version that supports `LIKE` on nondeterministic collations, which keeps the fallback in
   decision 3 open.
2. **Keep PascalCase table/column names** (Npgsql quotes them: `"Transmissions"."StudentId"`). Moving to
   snake_case (`EFCore.NamingConventions`) would also touch the raw SQL, the export scripts and every ad-hoc query
   for no functional gain.
3. **Accent-insensitive Arabic: normalized shadow columns, not collation.** The codebase already does this for
   hadiths (`NormalizedMatn`, `NormalizedBookName` filled by `ArabicNormalizer`); search code only runs `Contains`
   on those. The one LINQ query that relies on `Arabic_100_CI_AI` for substring matching is
   `NarratorService.SearchNarratorsAsync` (`FullName.Contains`, `KnownAs.Contains`). Add `NormalizedFullName` and
   `NormalizedKnownAs`, fill them in the ETL with the same `ArabicNormalizer`, and query those.
   The remaining `CI_AI` columns are only displayed or compared for equality on values the ETL already wrote, so
   they become plain `text` in the default deterministic collation.
   *Fallback if a CI_AI column turns out to need DB-side matching:* an ICU collation
   `CREATE COLLATION arabic_ci_ai (provider = icu, locale = 'ar-u-ks-level1', deterministic = false)`.
   Avoid it by default: nondeterministic collations cannot back b-tree pattern indexes and break `UNION` type
   matching in recursive CTEs.
4. **`BIN2` columns become `text COLLATE "C"`** (byte-wise, same semantics as `BIN2`), and get
   **`pg_trgm` GIN indexes** (`gin_trgm_ops`) on `NormalizedMatn`, `FullIsnadText`, `NormalizedBookName`,
   `NormalizedFullName`, `NormalizedKnownAs`. Today every `%word%` search is a scan of 274k hadith rows; with
   trigram indexes it becomes an index lookup. This is the main performance win of the move.
5. **Fresh migration history.** Archive the 11 SQL Server migrations (move to `Data/Migrations.SqlServer/`,
   excluded from compile, or delete after asking) and generate one Npgsql `InitialCreate`, plus a hand-written
   migration for `CREATE EXTENSION pg_trgm` and the GIN indexes. The `AddIlalEngine` schema-drift guards only
   existed for half-patched SQL Server databases and are not needed.
6. **No dual-provider period in code.** Supporting both providers needs two migration assemblies and doubles the
   test matrix. Parity is checked instead by running the last SQL Server build (git tag before the switch) next to
   the new build and diffing their API output (phase 6).
7. **Data: re-run the ETL into PostgreSQL**, the canonical path (it also proves the loader works on the new
   provider). Fallback if the ETL cannot reproduce v7 exactly: a one-off copier (pgloader, or a small console tool
   reading SQL Server and writing with Npgsql binary `COPY`), deleted afterwards.

## Phases

### Phase 0: baseline on SQL Server (before any code change)
- Tag the current commit (`pre-postgres`).
- `python scripts/eval_search.py --save docs/challenge/evaluation/baseline_sqlserver.json` and the same with
  `scripts/eval_verify.py`.
- Record per-table row counts and per-book hadith/transmission counts from v7 (the table in `backups/README.md`
  is the reference: Hadiths 274,597, Transmissions 1,266,022, Narrators 44,282, mudallisin/mukhtalit 165/168).
- Snapshot API responses for ~30 fixed hadith ids: `/tree`, comparative tree (`/takhreej`), `/ilal`,
  narrator search for 20 names with and without tashkeel. Store as JSON for diffing.

### Phase 1: provider and model
- Infrastructure, Etl (and `IlalCompare`): replace `Microsoft.EntityFrameworkCore.SqlServer` with
  `Npgsql.EntityFrameworkCore.PostgreSQL`; drop `Microsoft.Data.SqlClient` use.
- `Program.cs` (Api, Etl): `UseNpgsql(cs, o => { o.CommandTimeout(...); o.EnableRetryOnFailure(3, 5s, null); })`;
  ETL logging switches to `NpgsqlConnectionStringBuilder` (`Host` / `Database`).
- Connection strings: `Host=localhost;Port=5432;Database=smart_hadith_tree;Username=sht;Password=...`
  (password in user-secrets / env, not in `appsettings.json`). `MultipleActiveResultSets` has no Npgsql
  equivalent: grep for nested reads on one context (no `AsAsyncEnumerable` in `src` today, so low risk) and let the
  integration tests in phase 5 catch the rest.
- `HadithTreeDbContext`: remove `UseCollation("Arabic_100_CI_AI")`.
- Configurations: remove every `UseCollation("Arabic_100_CI_AI")`; `Arabic_100_BIN2` → `UseCollation("C")`;
  `HasFilter("\"SourceKey\" IS NOT NULL")`.
- Domain: add `Narrator.NormalizedFullName` / `NormalizedKnownAs`; `NarratorService` queries them.
- Generate `InitialCreate` for Npgsql; add the `pg_trgm` migration (decision 4).
- Check no `DateTime` columns need UTC handling (none in Domain today; re-check after the model change).

### Phase 2: raw SQL (`HadithChainRepository`)
- `WITH` → `WITH RECURSIVE`; quote identifiers (`"Transmissions"`, `"StudentId"`, …); aliases quoted to match DTO
  property names exactly (`AS "NarratorName"`), since `SqlQueryRaw` maps by column name and PG folds unquoted names
  to lowercase.
- Types: `UNIQUEIDENTIFIER` → `uuid`, `NVARCHAR(n|MAX)` → `text`, `BIT` → `boolean`, `NEWID()` →
  `gen_random_uuid()`, `CAST(0 AS BIT)` → `false`.
- PG requires each recursive-CTE column to have the **same type** in both branches: cast `TransmissionTerm` to
  `text` on both sides (the column is `varchar(50)`), and the zero-GUID anchor to `uuid`.
- `IN ({0}, {1}, …)` → `= ANY({0})` with one `Guid[]` parameter (simpler, one plan for any list length).
- Recursion limit: SQL Server stops at 100 levels by default, PG has no limit. The join on
  `StepOrder = parent.StepOrder + 1` makes depth strictly increase, so it terminates; keep it that way.

### Phase 3: ETL
- Drop `SqlBulkCopyOptions` from `BulkConfig` (SQL Server only; keys are GUIDs so `KeepIdentity` is moot).
  `EFCore.BulkExtensions` 9 supports PostgreSQL via binary `COPY`.
- `BulkInsertOrUpdateAsync(Narrators)` uses `ON CONFLICT` on PG, which needs a unique index on the match key:
  confirm the key it upserts on and that the index exists.
- Fill the new normalized narrator columns where `FullName`/`KnownAs` are set (parsers + `ShamelaIlalService`
  if it renames).
- Load into a scratch database first (`ConnectionStrings__DefaultConnection` env override already supported), then
  run `seed-ilal-shamela`. Compare counts with phase 0. Run `ANALYZE` after the load.

### Phase 4: behaviour differences to check in Application
- **Ordering ties.** `OrderBy(h => h.MatnArabic.Length)` + `Skip/Take` and `NarratorService`'s `Take(50)` without
  `OrderBy` return rows in engine-dependent order. Phase 0 found one case that already varies between two runs on
  SQL Server: the omitter list in `MatnAtMadarRule`'s Shudhudh evidence text (مسند أحمد #2). Add a tiebreaker (`.ThenBy(h => h.Id)`) and an `OrderBy` for
  narrators, otherwise search pages will differ between engines and between runs.
- **`string.Contains` translation.** Check with `ToQueryString()` what Npgsql emits for `Contains(param)`.
  If it is `strpos(...) > 0`, the trigram index is not used; switch those predicates to
  `EF.Functions.Like(col, "%" + escaped + "%")` (escape `%`, `_`, `\`). `HadithSearchService` already uses
  `EF.Functions.Like` for ordered phrases.
- **Case sensitivity** of Latin-text equality (`Source`, `SourceKey`, `GradeEn`): PG is case-sensitive, SQL Server
  CI was not. Values are ETL-written constants, so this should be fine; verify with the parity diff.
- `.Length` → `length()` counts characters on both engines; no change.

### Phase 5: tests
- Keep the InMemory unit tests.
- Add an integration test project area using **Testcontainers.PostgreSql** that migrates a real database, seeds a
  small fixture (2–3 hadiths with tahwil, shared narrators) and covers: `GetIsnadTreeAsync`,
  `GetComparativeIsnadTreeAsync`, hadith search with Arabic diacritics, narrator search with/without tashkeel,
  bulk insert + upsert. This closes the current gap where raw SQL has no tests at all.

### Phase 6: parity check
- Run old build (tag `pre-postgres`, SQL Server, port 5148) and new build (PG, port 5147).
- `python scripts/snapshot_api.py --save snapshot-pg.json`, then `--compare` it with
  `postgres-baseline/snapshot-sqlserver.json`. The script keys cases by book/chapter/number and relabels every GUID
  (the ETL assigns random ids, and the comparative anchors use `NEWID()`), so tree/takhreej/ilal must match exactly.
- `python scripts/eval_search.py --compare docs/challenge/evaluation/baseline_sqlserver.json` and the `/verify`
  equivalent: no regressions; expect latency to drop on multi-word searches.

### Phase 7: ops and docs
- `docker-compose.yml` / `docker-compose.prod.yml`: `postgres:18` service, `POSTGRES_PASSWORD` instead of
  `MSSQL_SA_PASSWORD`, volume `/var/lib/postgresql`, healthcheck `pg_isready -U sht -d smart_hadith_tree`,
  port 5432. PG needs far less memory than SQL Server's 2 GB minimum, which helps the prod host.
- `deploy/deploy-remote.sh`: pre-deploy backup with `pg_dump -Fc` (`predeploy_*.dump`), restore with
  `pg_restore --clean`. Update `deploy/deployment.md`, `deploy/devops-handoff.md`.
- Backups: take `SmartHadithTree_Shamela_v8_pg_<date>.dump` (custom format) with SHA-256 in `backups/README.md`;
  keep the `.bak` files as the historical record. Update the Drive link.
- Scripts: port `export_chains.ps1` / `export_db_chains.ps1` from `sqlcmd` to `psql`; switch `IlalCompare` to
  Npgsql; remove `scripts/setup.ps1` / `fix.ps1` / `setup_etl.ps1` SqlServer lines.
- Docs: `CLAUDE.md` (commands, default DB), `README.md` (restore steps), `docs/architecture.md`
  (collation/normalization section), `docs/changelog.md`.

## Risks

| Risk | Mitigation |
|---|---|
| Narrator search loses tashkeel-insensitivity | Normalized columns (decision 3) + integration test with diacritics |
| `Contains` not using trigram index | Inspect SQL in phase 4; `EF.Functions.Like` |
| Recursive CTE type/collation errors only at runtime | Phase 2 casts + integration tests |
| ETL does not reproduce v7 byte-for-byte (resolver output changed since) | Compare counts per book; fall back to one-off copy from v7 (decision 7) |
| Search ranking/paging differs due to ties | Tiebreakers (phase 4) + eval compare |
| Prod cutover loses data | Data is read-only apart from ETL/admin imports: cutover = load PG, switch connection string, keep SQL Server volume until the parity check passes in prod |

## Rough size

Phases 1–3: about a day of code. Phases 4–6: a day or two, mostly checking. Phase 7: half a day.
Rollback at any point before phase 7 is `git checkout pre-postgres`.
