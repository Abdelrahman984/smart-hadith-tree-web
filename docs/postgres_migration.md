# Plan: replace SQL Server with PostgreSQL

Status (2026-10-21), branch `feature/postgres`:
- **Phase 0 done**: tag `pre-postgres` on `f1c6493`, baselines in `docs/challenge/evaluation/postgres-baseline/`
  (see its README).
- **Phase 1 done**: the code builds and migrates against PostgreSQL 18; see "Phase 1: what was done" below.
- **Phase 2 done**: both raw queries of `HadithChainRepository` are PostgreSQL and return the same rows as the old
  T-SQL; see "Phase 2: what was done".
- **Phase 3 done**: the ETL loads the full v7 corpus into PostgreSQL in about 4 minutes, and every table and
  per-book count matches the SQL Server baseline; see "Phase 3: what was done".
- **Phase 4 done**: part A (tiebreakers on queries that already sort) and part B (an order for the five reads that
  had none, as proposed and approved); see "Phase 4".
- **Phase 5 done**: 11 Testcontainers integration tests on a real PostgreSQL 18 (isnad CTEs, search translation,
  bulk `COPY`, Ilal loading); see "Phase 5: what was done".
- **Phase 6 done**: the `pre-postgres` build on SQL Server and this branch on PostgreSQL, side by side: evals and
  Ilal pages identical, every API difference explained, searches 8–12× faster; see "Phase 6: results".
- **Phase 7 done in the repository**: compose files, deploy script, backups (v8 `.dump`), scripts and docs are
  PostgreSQL; see "Phase 7: what was done". **Production is not cut over**: that is a manual step on the host
  (`deploy/devops-handoff.md`, "Cutover to PostgreSQL"), and the v8 dump still has to be uploaded.

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
   `NormalizedKnownAs`, derived from `FullName`/`KnownAs` with the same `ArabicNormalizer`, and query those.
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
5. **Fresh migration history.** Archive the 11 SQL Server migrations (moved to `Data/Migrations.SqlServer/`,
   excluded from compile) and generate one Npgsql `InitialCreate`. `pg_trgm` and the GIN indexes are declared in
   the model (`HasPostgresExtension`, `HasMethod("gin")`/`HasOperators("gin_trgm_ops")`), so no hand-written
   migration is needed. The `AddIlalEngine` schema-drift guards only existed for half-patched SQL Server databases
   and are not needed.
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

#### Phase 1: what was done
- Packages: `Npgsql.EntityFrameworkCore.PostgreSQL` 9.0.4 (EF Core 9.0.21) in Infrastructure and Etl.
  Infrastructure also references `Microsoft.EntityFrameworkCore.Relational` 9.0.*: without a floating EF package
  it compiled against 9.0.21 (through `Tools`) while its consumers resolved 9.0.1–9.0.9, which fails with CS1705.
  The old SqlServer 9.0.* reference used to do this implicitly.
- `NormalizedFullName` / `NormalizedKnownAs` are computed properties on `Narrator` (getter normalizes, private
  no-op setter for EF). Every writer (seed, Shamela mappers, Gawami importer, bulk insert) therefore stores them
  without changes; the InMemory unit tests evaluate the same getter.
- Narrator search is now also insensitive to alef/ta marbuta variants, not only tashkeel, because both sides go
  through `ArabicNormalizer` (with `Arabic_100_CI_AI` the column side was only collated, the query side normalized).
  The phase 6 snapshot diff will show where this changes results.
- `HasIndex(h => h.NormalizedBookName)` twice reconfigured one index (the B-tree became the GIN one); the trigram
  index uses the named overload `HasIndex(expr, name)`.
- Connection strings carry no password; Npgsql reads `PGPASSWORD` or `%APPDATA%\postgresql\pgpass.conf`.
- The archived snapshot is named `HadithTreeDbContextModelSnapshot.SqlServer.cs`: with its original file name,
  `dotnet ef migrations add` wrote the new snapshot over it.
- `IlalCompare` switched to Npgsql (it compared SQL Server databases; the SQL Server version is at `pre-postgres`).
- New files under `Data/Migrations/` need `git add -f`: the `data/` rule of `.gitignore` also matches `Data/`
  (git on Windows ignores case). The old migrations were tracked before that rule existed.
- **Checked** on a throwaway `postgres:18` container (port 5433): `dotnet ef database update` creates 9 tables,
  `pg_trgm`, the 5 GIN indexes and the filtered `SourceKey` index; `dotnet run --project src/SmartHadithTree.Etl
  -- seed` loads 46 records through the current `BulkDataIngestionService` (bulk insert already works on PG);
  `string.Contains` translates to `LIKE '%…%'` (trigram-indexable, so the phase 4 `EF.Functions.Like` fallback is
  not needed); narrator search for «الزُّهْرِيِّ», «مَالِكُ بْنُ أَنَسٍ», «عُمَرَ بْنِ الْخَطَّابِ» finds the seed narrators.
- **Tests**: 284/286 pass. The 2 failures (`ShamelaLoaderTests.ParseAsync_OnTheRealData_GivesTheExpectedCounts`,
  `ShamelaIlalServiceTests.OnTheRealData_EveryLinkedEntryIsApplied`) expect pre-v7 counts (23,502 narrators,
  158 mudallisin vs 44,282 / 165) and fail the same way at tag `pre-postgres`; unrelated to the move.
- **Left for phase 3**: `EFCore.BulkExtensions` is still the metapackage, which pulls the SqlServer and Oracle
  providers (and the NU1902/NU1903 warnings for `System.Security.Cryptography.Pkcs` 9.0.4 through them) and keeps
  `SqlBulkCopyOptions` compiling. Switching to `EFCore.BulkExtensions.PostgreSql` removes all three.

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

#### Phase 2: what was done
- `GetIsnadTreeAsync`: `WITH RECURSIVE`, quoted identifiers and aliases, `::uuid` / `::text` casts,
  `TransmissionTerm::text` in the recursive branch (its column is `varchar(50)`, the anchor's `NULL::text`),
  `false` for `IsAnomaly`.
  *Corrected in phase 5 by breaking each on purpose:* the recursive branch's `::text` cast is not required
  (the `NULL::text` anchor sets the column type and `varchar` converts implicitly; kept for clarity), and quoting
  the **aliases** is not required either (Npgsql falls back to a case-insensitive column lookup). Quoting the
  **tables and columns** is required, since they were created as quoted PascalCase names.
- `GetComparativeIsnadTreeAsync` is not recursive (a `UNION ALL` of the compilers and every edge): quoted names,
  `gen_random_uuid()` for the anchor ids, and `= ANY({0})` with one `uuid[]` parameter instead of an `IN` list
  built from one placeholder per hadith.
- **Checked, raw rows** (scratch tool, not in the repo): the 36 snapshot hadiths, their related hadiths, chains and
  narrators were copied from SQL Server v7 into the test container **with their GUIDs**; the old T-SQL (from tag
  `pre-postgres`) ran on SQL Server and the new SQL on PostgreSQL for 36 trees and 12 comparative groups (the 3
  snapshot groups with related hadiths, 8 groups of 4 snapshot hadiths from different books, the 5 tahwil cases):
  **810 rows, every column and column name identical** (sorted, comparative anchor ids excluded).
- **Checked, end to end**: with all narrators, relations and evaluations copied too, `HadithSearchService`
  `GetIsnadTreeAsync` / `GetComparativeTreeAsync` on PostgreSQL against the phase 0 API snapshot
  (`snapshot_api.normalize`): 71 of 72 outputs identical (696 nodes). The one difference is not SQL: see phase 4,
  group rule order. (With only the relations among the copied narrators, 3 more Ilal reports differed, because
  `NarratorsWithRelations` counts all of a narrator's relations; a copy artifact, gone with the full copy.)
- `snapshot_api.py`: every compiler node of a tahwil tree has the id `00000000-…`, which was labelled after
  whichever compiler came first, so two tahwil trees (الدارقطني #1, البيهقي #323) differed only in labels. That id
  now has one fixed label.

### Phase 3: ETL
- Drop `SqlBulkCopyOptions` from `BulkConfig` (SQL Server only; keys are GUIDs so `KeepIdentity` is moot).
  `EFCore.BulkExtensions` 9 supports PostgreSQL via binary `COPY`.
- `BulkInsertOrUpdateAsync(Narrators)` uses `ON CONFLICT` on PG, which needs a unique index on the match key:
  confirm the key it upserts on and that the index exists.
- Replace the `EFCore.BulkExtensions` metapackage with `EFCore.BulkExtensions.PostgreSql` (see phase 1 notes).
- The normalized narrator columns need no ETL change (computed properties, phase 1); check they are filled after
  the full load.
- Load into a scratch database first (`ConnectionStrings__DefaultConnection` env override already supported), then
  run `seed-ilal-shamela`. Compare counts with phase 0. Run `ANALYZE` after the load.

#### Phase 3: what was done
- `EFCore.BulkExtensions` → `EFCore.BulkExtensions.PostgreSql` 9.0.2, and `SqlBulkCopyOptions` removed. The build
  now has no warnings (the NU1902/NU1903 `System.Security.Cryptography.Pkcs` warnings came through the SqlServer
  and Oracle providers of the metapackage), and nothing references `Microsoft.Data.SqlClient` any more.
- **Bug found by the first full load**: the `COPY` of the 1,266,022 transmissions failed in
  `NpgsqlBinaryImporter.Complete` with a read timeout, and the transaction rolled back. The binary importer takes
  its timeout from the connection string's `Command Timeout` (default 30 s), not from EF's `CommandTimeout(600)`,
  and finishing that `COPY` (three foreign keys and three indexes) takes longer. The ETL now also sets
  `CommandTimeout = 600` on its connection string (`src/SmartHadithTree.Etl/Program.cs`).
  Side effect seen in that run: `seed-ilal-shamela` on the empty database logged 0 rows and still
  "ETL completed successfully".
- **Checked** on the test container, fresh database: `dotnet run -c Release --project src/SmartHadithTree.Etl --
  data/shamela_rijal` (resolver output written 2026-10-07 02:07, five minutes before the v7 backup) in 233 s
  (parse 52 s; hadiths about 2 min, most likely because the GIN indexes are maintained during the `COPY`; transmissions 50 s),
  then `seed-ilal-shamela data/shamela_rijal` in 3 s (165 mudallisin, 141 mukhtalitun, 168 hearings, 76 group
  rules), then `ANALYZE`.
  - **Tables**: identical to v7 (Hadiths 274,597, Transmissions 1,266,022, Narrators 44,282, ScholarEvaluations
    131,994, NarratorRelations 153,519, MukhtalitHearings 168, MukhtalitGroupRules 76, HadithClusters 0,
    mudallisin 165).
  - **Per book**: `book-counts-sqlserver.csv` reproduced exactly for all 31 books (hadiths, matn and isnad
    characters, transmissions, sum of `StepOrder`), with `char_length` in place of `DATALENGTH/2`.
  - `NormalizedFullName` filled for all 44,282 narrators (25,291 differ from `FullName`).
  - `NormalizedMatn LIKE '%انما الاعمال بالنيات%'` runs as a bitmap scan on `IX_Hadiths_NormalizedMatn_Trgm`,
    20 ms over the 274,597 hadiths. Database size 1.49 GB.
- `UpsertNarratorsAsync` and `ReplaceNarratorRelationsAsync` have no callers. The upsert matches on `FullName`,
  which on PostgreSQL becomes `ON CONFLICT` and needs a unique index on that column (there is none); not run.
- Possible speed-up, not done: create the GIN indexes after the load instead of maintaining them during `COPY`.

### Phase 4: behaviour differences to check in Application
- **Ordering ties.** `OrderBy(h => h.MatnArabic.Length)` + `Skip/Take` and `NarratorService`'s `Take(50)` without
  `OrderBy` return rows in engine-dependent order. Phase 0 found one case that already varies between two runs on
  SQL Server: the omitter list in `MatnAtMadarRule`'s Shudhudh evidence text (مسند أحمد #2).
  Phase 2 found one that differs between the engines: `IkhtilatRule.GroupRuleHint` quotes `rules[0]`, and
  `IlalAnalysisService` loads `MukhtalitGroupRules` without `ORDER BY` (شريك النخعي has 4 rules; SQL Server gave
  «الذين سمعوا منه بواسط», PostgreSQL «من سمع منه قديما», takhreej of سنن ابن ماجه #1). The rules have GUID keys
  and no order column, so which rule is "the" hint needs a decision (e.g. by `Timing`, or an ordinal kept from
  `ilal.json`).
  Related, latent: in a tahwil tree all compiler nodes share the id `00000000-…`, so
  `GetIsnadTreeAsync`'s anomaly check (`nodes.FirstOrDefault(n => n.Id == node.ParentNodeId)`) compares a step-1
  narrator with whichever compiler comes first. It gave the same output on both engines for the 5 tahwil cases.
  Add a tiebreaker (`.ThenBy(h => h.Id)`) and an `OrderBy` for narrators, otherwise search pages will differ
  between engines and between runs.

#### Phase 4, part A: tiebreakers (done)
Only queries that already sort, so nothing changes except which of the equal rows comes first. Ties go to keys
that survive a reload (book, number, volume, registry `SourceKey`, byte-wise collation on both engines), then to
the GUID, which the ETL regenerates on every load.
- `HadithSearchService`: the three database fetches ordered by `MatnArabic.Length` (`Skip/Take` page, the
  candidate cap, the exact-phrase cap) add `NormalizedBookName`, `HadithNumber`, `Id`; the final in-memory ranking
  adds `BookName` (ordinal) and `Id` after `HadithNumber`.
- `HadithChainRepository`: the tree query orders by `StepOrder`, `SourceKey COLLATE "C"`, `NarratorId`, `Id`; the
  comparative query by `StepOrder`, book, number, `HadithId`, `SourceKey`, `NarratorId`, `Id`. The merge takes each
  narrator's first row (its `TransmissionTerm`), and the anomaly check the first node with the shared zero id, so
  both depended on row order.
- `BooksService.GetHadithsAsync`: `HadithNumber`, then `Volume`, then `Id`. Book, chapter and number repeat
  **4,925** times in v7 (المعجم الكبير restarts its numbers per volume: nine #1, two of them in one chapter).
- `scripts/snapshot_api.py` relied on book/chapter/number being unique: `snapshot-cases.json` now also carries
  `matnSha1` (filled from the matn in `snapshot-sqlserver.json`, so the phase 0 baseline keeps the hadith it
  actually resolved), and `resolve_id` only accepts the hadith with that exact text.
- **Checked** on the full corpus in the test container: the 36 snapshot trees, resolved by matn hash, have 376
  nodes, the same as the SQL Server snapshot; a tree requested twice is identical; the search SQL ends in
  `ORDER BY length("MatnArabic"), "NormalizedBookName", "HadithNumber", "Id"`. The comparative trees have 324 nodes
  vs 320, because their related hadiths come from the unordered `Take(20)` below.

#### Phase 4, part B: reads with no order at all (done, the "Proposed order" column was chosen)
Here a cap or a `[0]` picks rows, so an order is a behaviour choice:

| Read | What depends on it | Proposed order | Alternative |
|---|---|---|---|
| `IkhtilatRule.GroupRuleHint` quotes `rules[0]` of `MukhtalitGroupRules` | The hint after «ولم يتبين أسمع منه قبل الاختلاط أم بعده»: 29 mukhtalitun have rules, 19 have several, **15 mix before/after**, so the quoted verdict can flip | Add `Ordinal` (position in `ilal.json`, i.e. the book's own order) and quote the **first «before» and the first «after» rule** (one if only one kind exists): «…نُص في (الذين سمعوا منه بواسط) أن سماعهم قبل الاختلاط، وفي (المتأخرون عنه بالكوفة) أن سماعهم بعده» | Keep one rule: the first by `Ordinal`. Or no schema change: order by `Timing`, then `GroupAr` (byte-wise), and quote the first |
| `HadithSearchService.FindRelatedHadithsAsync`: `Take(20)` with no order | Takhreej related list, `/ilal/{id}` (its turuq and their order, which also fixes the varying Shudhudh omitter list of phase 0), the merged trees; in a sample of 2,000 short hadiths, 194 have more than 20 matches | `MatnArabic.Length`, book, number, `Id`: the shortest texts that contain the matn excerpt, the same order the search uses | Book, number |
| `NarratorService.SearchNarratorsAsync`: `Take(50)` with no order | Which 50 narrators the search box lists for short queries | Names that start with the query first, then by `NormalizedFullName` length, then `SourceKey` | `NormalizedFullName` length, `SourceKey` |
| `IlalAnalysisService.LoadContextAsync`: transmissions with no order | `BuildPaths` walks links in load order and stops at `MaxPathsPerHadith`, so which tahwil branches survive the cap, and their order | `StepOrder`, the sheikh's `SourceKey`, `Id` | A chain ordinal on `Transmissions` written by the ETL from the resolver's chain order (migration + reload) |
| `NarratorService` tooltip: `ScholarEvaluations…VerdictRating.FirstOrDefault()` | The grade shown when the narrator has no `GawamiRank`/`Verdict`: 3,459 narrators have several evaluations | `ScholarName` (byte-wise), then `Id` | Most severe `VerdictRating` first |

What was done:
- `MukhtalitGroupRule.Ordinal` (migration `AddGroupRuleOrdinal`; `ShamelaIlalService` writes the rule's position in
  the narrator's `group_rules` list). **Existing databases need `seed-ilal-shamela` once** (3–5 s): until then every
  rule has `Ordinal` 0 and the order falls back to `Id`. `IlalAnalysisService` loads the rules by `Ordinal`.
  `IkhtilatRule.GroupRuleHint` quotes the first «before» and the first «after» rule. Rules without a timing
  («other» in `ilal.json`, `HearingTiming.Unknown`: حفص بن غياث, سويد بن سعيد, «من كتب عنه من كتابه») are no longer
  quoted; before, any rule that was not «after» was quoted as «أن سماعهم قبل الاختلاط».
- `FindRelatedHadithsAsync`: `MatnArabic.Length`, `NormalizedBookName`, `HadithNumber`, `Id` before `Take(20)`.
- `SearchNarratorsAsync`: prefix match (full name or `KnownAs`) first, then `NormalizedFullName.Length`,
  `SourceKey`, `Id`. (`SourceKey` sorts in the column's default collation here, not byte-wise: it only breaks ties
  of equal-length names, and `EF.Functions.Collate` would not run on the InMemory tests.)
- `LoadContextAsync`: transmissions by `StepOrder`, the sheikh's `SourceKey`, `Id`.
- Tooltip: evaluations by `ScholarName`, then `Id`.
- **Tests** (+6): both-ways group rules quote the first before and first after rule in order; after-only; no-timing
  rules not quoted; narrator search order; tooltip order; related hadiths keep the 20 shortest, shortest first.
  290/292 pass (the 2 known stale-count tests).
- **Checked** on the full corpus (test container, migrated, Ilal reloaded: شريك's 4 rules have `Ordinal` 0–3 in the
  books' order): related + Ilal analysis for the 36 snapshot cases twice, 36 reports with 44 findings, identical
  between the runs. The takhreej of سنن ابن ماجه #1 now says «…وقد نُص في جماعة من تلاميذه (الذين سمعوا منه بواسط)
  أن سماعهم قبل الاختلاط: «…»، وفي (المتأخرون عنه بالكوفة) أن سماعهم بعده: «…»». مسند أحمد #2's related hadiths are
  now the 20 shortest matches, so its Shudhudh finding is worded differently from the phase 0 snapshot. Expect
  such differences in phase 6 wherever the old `Take(20)` picked other hadiths.
- **`string.Contains` translation** (checked in phase 1: Npgsql 9 emits `LIKE '%…%'`, so nothing to do).
  Check with `ToQueryString()` what Npgsql emits for `Contains(param)`.
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

#### Phase 5: what was done
- `Testcontainers.PostgreSql` 4.6.0 in `SmartHadithTree.Tests`; `Integration/PostgresFixture.cs` starts one
  `postgres:18` container per run (xUnit collection fixture) and applies the real migrations. Tests share the
  database and isolate themselves: fresh GUIDs, and a random Arabic marker word (`Marker.New()`, letters the
  normalizer leaves alone) for anything searched by text. All are tagged `[Trait("Category", "Integration")]`.
- 11 tests in 4 files:
  - `IsnadQueryTests`: a tahwil isnad converging on one Companion (5 nodes, both branches, parent links, terms,
    `GradeEn` from `IbnHajarRank`, the temporal-break anomaly on one branch only, `SourceKey` order); two compilers
    sharing the zero id, in `SourceKey` order; the comparative tree merging a shared sheikh and Companion with both
    sources, through the `uuid[]` parameter with 3 ids (one without a chain) and with 1.
  - `SearchTranslationTests`: matn search with and without tashkeel in the query; `Contains` → `LIKE`, and `%`
    / `_` in the searched text match literally (Npgsql escapes them); narrator search with and without tashkeel
    in the new order; alef variants.
  - `BulkIngestionTests`: `BulkDataIngestionService.IngestAsync` copies all five tables, and the computed
    normalized names reach the table through binary `COPY` (read back with raw SQL).
  - `IlalLoadingTests`: `IlalAnalysisService` on PostgreSQL (orders through the `Sheikh` navigation) quotes the
    group rules by `Ordinal`, not by physical order.
- **Checked**: 11/11 pass, twice in a row; full suite 301/303 (the 2 known stale-count tests), 61 s including the
  container start. Each test was also checked against a deliberate break: without `ORDER BY "Ordinal"` the
  `IlalLoadingTests` test fails 3 of 3 runs (it first passed by luck, because one `SaveChanges` inserts in key
  order, so the rules are now saved one at a time, last first); the other two breaks showed the corrections
  noted in phase 2.
- **Without Docker**: run `dotnet test --filter "Category!=Integration"` (checked: 292 tests, the 11 excluded).
  What the integration tests do when Docker is missing was not observed: Testcontainers found Docker Desktop
  even with `DOCKER_HOST` pointing elsewhere, and Docker was not stopped for the check. CI (`deploy.yml`,
  `ubuntu-24.04`) has Docker and will run them.
- Not covered: `UpsertNarratorsAsync` (no callers; it would need a unique index on `FullName` on PostgreSQL).

### Phase 6: parity check
- Run old build (tag `pre-postgres`, SQL Server, port 5148) and new build (PG, port 5147).
- `python scripts/snapshot_api.py --save snapshot-pg.json`, then `--compare` it with
  `postgres-baseline/snapshot-sqlserver.json`. The script keys cases by book/chapter/number and relabels every GUID
  (the ETL assigns random ids, and the comparative anchors use `NEWID()`), so tree/takhreej/ilal must match exactly.
- `python scripts/eval_search.py --compare docs/challenge/evaluation/baseline_sqlserver.json` and the `/verify`
  equivalent: no regressions; expect latency to drop on multi-word searches.

#### Phase 6: results (2026-10-21)
Setup: tag `pre-postgres` in a worktree, Release, on `:5148` against SQL Server `SmartHadithTree_ShamelaV7`; this
branch (`5793664`), Release, on `:5147` against the test container (phase 3 full load, migrated to
`AddGroupRuleOrdinal`, Ilal reloaded). Same user secrets, so the same AI provider. Launch configs `api-sqlserver-pre`
and `api-pg` in `.claude/launch.json` (local, untracked; the PostgreSQL password is read from a file at start).
Files in `docs/challenge/evaluation/postgres-baseline/` (`*-phase6.*`).

| Check | SQL Server (`pre-postgres`) | PostgreSQL (this branch) |
|---|---|---|
| Phase 0 snapshot vs today, same build | identical (the old build is stable) | — |
| `eval_search` (21 cases) | hit@1 15/18, hit@3 16/18, MRR 0.866, precision 0.93 | **identical**; no rank changed (`--compare`) |
| `eval_verify` (29 cases) | 29/29, 8/8 negatives abstained | **identical**; no status changed (`--compare`) |
| `eval_ilal_pages` (9 pages) | 119 findings, 46 matn, 9 Qadihah = the v7 record | **identical**, every grade unchanged |
| Snapshot: trees (36) | — | **identical** |
| Snapshot: related lists (36, as sets) | — | **identical** (order differs: phase 4B) |
| Snapshot: merged trees / Ilal reports | — | 5 / 4 differ, all explained below |
| Snapshot: narrator search (20 queries) | — | 16 differ, all explained below |

Latency (median ms, both warm, same machine):

| Endpoint | SQL Server | PostgreSQL |
|---|---|---|
| `eval_search` (hadith search) | 844 | **72** |
| `eval_verify` | 789 | **74** |
| `/takhreej/related` | 1,196 | **148** |
| `/ilal/{id}` | 1,160 | **96** |
| `/narrators/search` | 129 | **14** |
| `/tree`, `/takhreej?ids=` | 23, 27 | 28, 38 |

The substring searches are where SQL Server scanned and PostgreSQL uses the trigram indexes. Trees and merged trees
are the same small CTE queries on both, a few milliseconds slower on PostgreSQL in Docker.

Every difference, by cause:
- **Group-rule hint (phase 4B, intended)**: Ilal and merged-tree reports of سنن ابن ماجه #1 and جامع الترمذي #1
  quote the first «before» and «after» rule instead of one arbitrary rule.
- **Tiebreaker (phase 4A, intended)**: the merged tree of السنن الكبرى للبيهقي #1, one node's `transmissionTerm`
  (أخبرنا → عن): the merge takes the narrator's first row, which is now fixed instead of the database's.
- **Related order (phase 4B, intended)**: the snapshot builds merged trees from the first 5 related hadiths, so
  البيهقي #373 compares a different 5 (same related set); its Ilal report has the same finding types and severities.
- **Narrator search (phase 1, a bug fixed)**: on SQL Server, **مالك بن أنس, أبو هريرة, قتادة, عائشة, شعبة بن الحجاج,
  حماد بن سلمة return nothing**, with or without tashkeel. The query goes through `ArabicNormalizer` (أ→ا, ة→ه), the
  column does not, and `Arabic_100_CI_AI` ignores the diacritics but not those letters: any name with أ/إ/آ/ة could
  not be found. PostgreSQL searches the normalized columns and returns 2–48 narrators, the expected one first in
  most cases (أبو هريرة, قتادة, عبد الله بن عمر, شعبة بن الحجاج, حماد بن سلمة). الزهري and عبد الله بن عمر hit the cap of
  50 on both; which 50 now follows the new order. سفيان الثوري and نافع مولى ابن عمر return nothing on both (no
  registry name contains them as one string; not a migration issue).
- **Order-dependent Ilal verdict (pre-existing, exposed by phase 4B)**: مسند أحمد #2 has the same 4 turuq on both,
  but `MatnAtMadarRule` takes its reference matn from the turuq's order. SQL Server's arbitrary order compared
  Ahmad with ابن ماجه #1395 («زاد… ولم يذكرها», **Tanbih**, 0.45); the length order compares him with أبي يعلى #12
  («اختلف الرواة… شاذة», **Qadihah**, 0.5). The order is now deterministic, but the verdict still depends on it.
  A rule-engine question, not a migration one. **Fixed** on `fix/ilal-matn-order` (merged into `feature/postgres`): the rule no longer depends on the
  order, and مسند أحمد #2 is the Tanbih in every order (`docs/changelog.md`).

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

#### Phase 7: what was done
- **Compose**: `db` is `postgres:18` (user `sht`, database `smart_hadith_tree`, volume `pgdata` at
  `/var/lib/postgresql` as the 18 image expects, `pg_isready` healthcheck, `api` waits for it). Local publishes
  `${DB_PORT:-5433}` (5432 is often a local install). `POSTGRES_PASSWORD` replaces `MSSQL_SA_PASSWORD` and
  `MSSQL_MEMORY_LIMIT_MB` (`.env.production.example`). The old `sqldata` volume is no longer declared and is left
  on the host for a rollback.
- **`deploy/deploy-remote.sh`**: pre-deploy backup with `pg_dump -Fc` (`predeploy_*.dump`); new step 0 refuses a
  database image change (resets the checkout, exits 1, touches nothing), because `up -d` would otherwise start an
  empty PostgreSQL and `/api/books` would still answer 200; new check that `/api/books` is not empty. Checked:
  `bash -n`, `shellcheck -S warning` clean, the guard run with stubbed `docker`/`git` in three cases.
  **Correction after the merge** (deploy run 39524924844, 2026-10-21): on the host the guard failed open. The host's
  `.env` has no `POSTGRES_PASSWORD`, so `docker compose` could not read the new compose file; the guard asked compose
  for the running `db`, got nothing and let the deploy through. It failed at the pre-deploy backup on the same error,
  and the rollback's `compose up` failed too, so no container changed (checked: the site still served the SQL Server
  build). None of the three stub cases had an unreadable compose file. Fixed on `fix/deploy-guard-fail-closed`: the
  running `db` image is read from Docker labels, and an unreadable compose file or one without a database image is
  refused; stub-tested in five cases (including the host's state that day), the label query checked on a real compose
  container. The DevOps checklist is `deploy/postgres-cutover-tasks.md`.
- **Cutover runbook** for the live host: `deploy/devops-handoff.md`, "Cutover to PostgreSQL" (last SQL Server
  backup, `POSTGRES_PASSWORD`, replace `db`, `pg_restore`, deploy, rollback onto the kept `sqldata` volume).
- **Backup v8**: `backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump`, 153 MB (v7 `.bak`: 497 MB), SHA-256
  `73593f3e…78f94`, from the test container (phase 3 load + `AddGroupRuleOrdinal` + Ilal reload). Restored with the
  documented command into a fresh `postgres:18`: no errors, 168 s, per-book counts identical. Not uploaded yet.
- **Local compose stack run end to end** (separate project): restore 84 s, both images built, books, search,
  narrator search, web 200, CORS, no migration failure.
- **Scripts**: `export_chains.ps1` and `phase6/export_db_chains.ps1` now use `psql` (JSON built by PostgreSQL, SQL
  passed as a UTF-8 file); run on the full corpus: 274,597 hadiths and 1,266,022 links, and al-Bukhari +
  al-Mustadrak 16,532 / 87,313, matching the per-book counts. `setup*.ps1` / `fix.ps1` name the Npgsql packages.
- **Docs**: `CLAUDE.md`, `AGENTS.md`, `README.md`, `docs/architecture.md`, `docs/data_ingestion.md`,
  `deploy/deployment.md`, `deploy/devops-handoff.md`, `backups/README.md`, code comments (the API's
  "migration check failed" message, which `deploy-remote.sh` greps for, keeps that phrase). Historical records
  (changelog entries, `docs/shamela_migration.md`, `docs/backlog.md`, `docs/plan-prompt.md`) and the generators of the
  committed challenge presentation (`scripts/generate_deck.py`, `generate_pdf.py`) are unchanged on purpose.
- **Correction to phase 5**: Testcontainers 4.6.0 brought `SSH.NET` 2024.2.0 with two high-severity advisories
  (NU1903, test project only); phase 5 only looked for compiler warnings. Now 4.15.0 (`SSH.NET` 2026.0.0) and the
  image-taking `PostgreSqlBuilder` constructor: full rebuild with 0 warnings, 301/303 tests.

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
