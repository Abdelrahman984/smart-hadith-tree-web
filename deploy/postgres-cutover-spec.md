# Specification: production database cutover, SQL Server to PostgreSQL 18

Smart Hadith Tree, production host `/opt/smart-hadith-tree`. Status: **ready for implementation, not yet run on the
real host.** Date: 2026-10-22.

**This file is self-contained.** It says what the end state must be and how it is verified. How to get there is up to
the implementer; Appendix A is a suggested procedure. **The cutover as a whole has not been run anywhere**; its parts were
tested separately (a restore of the dump into a fresh `postgres:18`, the local compose stack, the deploy check with
stubs, the commands' syntax). Any procedure that meets the requirements is acceptable. MUST and SHOULD are used in
the usual sense: a MUST is part of acceptance, a SHOULD is expected unless there is a reason.

## 1. Purpose and scope

The application's database changes from SQL Server 2022 to **PostgreSQL 18**. The application code is already merged and
built for PostgreSQL. Production still runs the SQL Server version and cannot switch by itself: every automatic deploy
from `master` is refused by design while the running `db` container is SQL Server.

**In scope:** replacing the production `db` service, loading the corpus into it, redeploying `api` and `web` from
`master`, proving the result, and being able to go back.
**Out of scope:** schema or code changes, domain/proxy/TLS changes, other containers on the shared host, removing the
old SQL Server data (a later, separate decision, section 8).

## 2. Current state (verified 2026-10-21/22)

| Item | State |
|---|---|
| Live version | SQL Server build, commit `f9b232d`, https://smart-hadith.idealisticsolutions.com |
| Stack | Docker Compose project `smart-hadith-tree`: `db` (`mcr.microsoft.com/mssql/server:2022-latest`), `api` (.NET 9), `web` (Next.js), optionally `caddy`. The host's `.env` sets `COMPOSE_FILE=docker-compose.prod.yml:compose.edge.yml`, so plain `docker compose ...` there is production |
| Database data | Named volume `smart-hadith-tree_sqldata` (SQL Server). Database `SmartHadithTree`; `/api/books` lists 31 books. Which data version it holds was not checked (an older handoff mentions the v5 backup, the new data is v7-equivalent), so narrator and transmission counts will rise after the cutover; the final SQL Server backup (T6) keeps the old data |
| How to tell the builds apart | `GET /api/narrators/search?q=قتادة`: **empty list on SQL Server**, up to 50 narrators on PostgreSQL (SQL Server finds no name containing أ إ آ ة) |
| Deploys from `master` | Refused on purpose until the cutover (`deploy/deploy-remote.sh` checks that the `db` image would change, resets the checkout to the live commit, exits 1). A **red "deploy" run in GitHub Actions is expected** and the site is untouched. The latest, 2026-10-22: `refused, f9b232d left live` |
| `POSTGRES_PASSWORD` | Not set in the host's `.env` yet |

## 3. Target state

After the cutover:

| # | Requirement |
|---|---|
| T1 | `db` is the image **`postgres:18`**, user `sht`, database `smart_hadith_tree`, data in the named volume `smart-hadith-tree_pgdata` (mounted at `/var/lib/postgresql`). It has **no published host port**: only `api` reaches it, over the compose network. The `pg_trgm` extension is installed |
| T2 | The database contains the corpus of the v8 dump (section 5) and nothing else: row counts as in A3, both EF migrations recorded in `"__EFMigrationsHistory"`, five trigram (`gin_trgm_ops`) indexes |
| T3 | `api` and `web` are running images built from the `master` commit that contains the PostgreSQL code (any later commit is fine); the API connects with `Host=db;Port=5432;Database=smart_hadith_tree;Username=sht;Password=${POSTGRES_PASSWORD}` as set by `docker-compose.prod.yml` |
| T4 | `POSTGRES_PASSWORD` is set in the host's `.env` (kept `chmod 600`, git-ignored): random, 24+ characters, letters and digits only (no characters that need quoting). It MUST NOT be committed, printed in logs, or sent in chat |
| T5 | The SQL Server data is **kept intact**: volume `smart-hadith-tree_sqldata` still exists and was not written to after the final SQL Server backup (T6). The SQL Server image and `MSSQL_SA_PASSWORD` stay until section 8 |
| T6 | A **final SQL Server backup** exists on the host before anything is replaced: `backups/final_sqlserver_<date>.bak` (compressed, about 0.5 GB, `COPY_ONLY`) |
| T7 | Everything not part of this stack is untouched: other projects' containers, networks and volumes, the edge proxy, `compose.edge.yml`, `.env` values other than the one added. **No** `docker system prune`, no `docker compose down -v`, no volume removal (shared host) |
| T8 | The next automatic deploy from `master` (GitHub Actions, "deploy") **succeeds** and leaves the stack as in T1 to T3 |

## 4. Constraints and invariants

- **Downtime:** the API is unavailable only from the moment `api` is stopped until it is back; planned total window
  **about 15 minutes, API down about 10**. The web pages keep loading meanwhile but their API calls fail. The window is
  agreed with the project owner beforehand.
- **Reversibility:** until section 8's cleanup, the previous state MUST be recoverable without restoring any data
  (section 7).
- **Data loss:** none expected. The site is read-only apart from admin imports (disabled at the edge); nothing writes to
  the database during the window.
- **Disk:** at least 5 GB free on `/opt` and `/var/lib/docker` (dump 0.2 GB, restored database about 1.5 GB, one
  pre-deploy dump, new images; the old volume stays).
- **Tools:** Docker Compose v2.24 or newer; outbound internet for `postgres:18` and the image builds.
- **Fail-closed deploy check:** while any step leaves the new compose file unreadable (for example a missing
  `POSTGRES_PASSWORD`) or the `db` image mismatched, `deploy-remote.sh` refuses. That is intended; do not bypass or edit it.

## 5. Inputs and artifacts

| Input | Value |
|---|---|
| Dump | `SmartHadithTree_Shamela_v8_pg_2026-10-21.dump`, PostgreSQL 18.6 `pg_dump -Fc` (custom format) |
| Location | Google Drive, anyone with the link: <https://drive.google.com/file/d/1ri78j75J4R4C-lBZYXmucMGJjZTwT6cP/view?usp=sharing> |
| Size | 160,344,262 bytes (153 MB) |
| SHA-256 | `73593f3e9832076a9aed85ef8324c63bf38eab33fe9418f9984b4167d6f78f94` |
| Destination | `/opt/smart-hadith-tree/backups/` (git-ignored; mounted in `db` as `/backups`), readable by other users (`chmod 644`) |
| Caveats | The hash is of the local file that was uploaded; the Drive copy has not been downloaded and re-hashed, so the check in A1 is the proof. Drive serves a "cannot scan for viruses" HTML page for files over 100 MB, so a plain `curl` of the link does **not** fetch the dump: use a browser plus `scp` (reliable), or `gdown` (untested) |
| Restore | `pg_restore -U sht -d smart_hadith_tree --clean --if-exists --no-owner` takes about 3 minutes; it needs a role allowed to `CREATE EXTENSION pg_trgm` (the image's `sht` is a superuser) |

## 6. Acceptance criteria

The cutover is accepted when **every** criterion holds. Run them on the host in `/opt/smart-hadith-tree`. `API_DOMAIN`
and `WEB_DOMAIN` are in the host's `.env`:

```bash
API_DOMAIN=$(grep -E '^API_DOMAIN=' .env | tail -1 | cut -d= -f2-)
WEB_DOMAIN=$(grep -E '^WEB_DOMAIN=' .env | tail -1 | cut -d= -f2-)
```

| # | Criterion | How to check | Pass |
|---|---|---|---|
| A1 | The dump on the host is intact (before restoring) | `ls -l` and `sha256sum` of the file; `file` on it | 160344262 bytes, the SHA-256 of section 5, "PostgreSQL custom database dump". Anything else: do not restore |
| A2 | The database is PostgreSQL 18 as specified (T1) | `docker compose ps db`; `docker compose exec -T db psql -U sht -d smart_hadith_tree -tA -c "select extname from pg_extension where extname='pg_trgm'"`; `docker port $(docker compose ps -q db)` | image `postgres:18`, healthy; `pg_trgm`; **no** port mapping printed |
| A3 | The data is complete (T2) | `docker compose exec -T db psql -U sht -d smart_hadith_tree -tA -c '<query>'` per row below | Hadiths **274597**; Transmissions **1266022**; Narrators **44282**; ScholarEvaluations **131994**; NarratorRelations **153519**; MukhtalitHearings **168**; MukhtalitGroupRules **76**; migrations `20261020214331_InitialCreate` and `20261020231954_AddGroupRuleOrdinal`; trigram indexes 5. Tables are quoted PascalCase, e.g. `select count(*) from "Hadiths"`; migrations: `select "MigrationId" from "__EFMigrationsHistory" order by 1`; indexes: `select count(*) from pg_indexes where indexdef like '%gin_trgm_ops%'` |
| A4 | The API serves the new build | `curl -s "https://$API_DOMAIN/api/books"` and `curl -s "https://$API_DOMAIN/api/narrators/search?q=%D9%82%D8%AA%D8%A7%D8%AF%D8%A9"` | books: a list of **31** names (not `[]`); «قتادة»: **non-empty** (on SQL Server it is `[]`) |
| A5 | Search works | `curl -s "https://$API_DOMAIN/api/search?query=%D8%A7%D9%84%D8%A3%D8%B9%D9%85%D8%A7%D9%84"` | non-empty results; SHOULD answer in well under 2 s (`curl -w '%{time_total}'`; measured about 0.1 s on a development machine, the host will differ) |
| A6 | The web front end works | `curl -s -o /dev/null -w '%{http_code}' "https://$WEB_DOMAIN"`; open the site, search a phrase, open a tree page and a takhreej page in a browser | 200; the pages render data |
| A7 | The admin endpoint is still blocked at the edge | `curl -s -o /dev/null -w '%{http_code}' "https://$API_DOMAIN/api/admin/import-gawami"` | **404** |
| A8 | The API started cleanly | `docker compose logs api \| grep -i "migration check failed"` | prints nothing |
| A9 | The images come from `master` (T3) | `git merge-base --is-ancestor 68dfdb7 HEAD && echo yes`; `git rev-parse --short HEAD`; `docker compose ps api web` | `yes` (the checkout contains the merge of the PostgreSQL pull request); both running |
| A10 | The secret handling (T4) | `grep -c '^POSTGRES_PASSWORD=' .env`; `stat -c %a .env` | `1`; `600` |
| A11 | Old data kept, final backup exists (T5, T6) | `docker volume ls \| grep smart-hadith-tree_sqldata`; `ls -l backups/final_sqlserver_*.bak` | the volume exists; a `.bak` of about 0.5 GB |
| A12 | The deploy pipeline works again (T8) | GitHub, Actions, workflow "deploy", **Run workflow** on `master` | a **green** run; afterwards A2 to A8 still hold |

## 7. Rollback requirements

- **Trigger:** roll back if A2 to A8 are not all met by the end of the window and the cause is not quick and obvious
  to fix, or earlier if the cause is clearly not fixable in the window. Do **not** roll back merely because a step
  printed a warning; compare with the criteria.
- **Method requirement:** the previous service (SQL Server build at the commit noted before the cutover) MUST be
  restorable from the retained volume and the old commit **without a data restore**, within about 10 minutes.
- **Do not** use the old `deploy-remote.sh` alone for it: it would first try a `sqlcmd` backup inside the PostgreSQL
  container and fail. Appendix A.4 gives the working commands.
- **After a rollback:** A4's «قتادة» returns `[]` again and A1/A11 still hold. The extra `POSTGRES_PASSWORD` line and
  the `pgdata` volume are harmless. Tell the owner; do not delete anything.

## 8. After acceptance

| # | Requirement |
|---|---|
| P1 | **Backups (SHOULD):** a nightly `pg_dump -Fc` into `backups/` (none is scheduled today), 14 days kept, with a copy off the host. Appendix A.5 has a cron line. A restore into a throwaway `postgres:18` container SHOULD be tested once |
| P2 | **Cleanup (only after about a week on PostgreSQL with no rollback, and with the owner's agreement):** remove `MSSQL_SA_PASSWORD` from `.env`, volume `smart-hadith-tree_sqldata`, image `mcr.microsoft.com/mssql/server:2022-latest`, and old `backups/*.bak` not wanted. Keep `final_sqlserver_<date>.bak` off the host if the owner wants it |
| P3 | **Hardening (recommended, optional):** a second database role without superuser rights for the API's connection string; log rotation for the containers; `Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning` for the API |
| P4 | `/usr/local/sbin/smart-hadith-deploy` (the SSH deploy gate) needs no change |

## 9. Deliverables to the project owner

- The live commit after the cutover (A9) and the output of A2 to A12 (copy of the terminal output is enough).
- How long `api` was down, and the output of the restore and of `deploy-remote.sh`.
- Anything that differed from this specification or the appendix (a failed command, a different path, a timing), so
  it can be corrected.

## 10. Open points (for the owner and the implementer)

- The window date and time.
- Where the off-host copy of the nightly backup goes (P1), and who deletes the old SQL Server data (P2).
- The off-host handling of `final_sqlserver_<date>.bak`.

## Appendix A. Suggested procedure (informative, not mandatory)

Prepared and syntax-checked (`bash -n`, `shellcheck`); the SQL Server backup quoting and the `.env` edit were tested in
isolation. Run in `/opt/smart-hadith-tree`. Where this and section 6 disagree, section 6 decides.

### A.1 Before the window (nothing changes on the running site)

```bash
cd /opt/smart-hadith-tree
git rev-parse --short HEAD                                   # the live commit (f9b232d or later SQL Server one): WRITE IT DOWN
docker compose ps                                            # db (mssql), api, web up
docker volume ls | grep smart-hadith-tree_sqldata            # kept by the cutover
df -h /opt /var/lib/docker                                   # 5 GB free or more
docker compose version                                       # v2.24 or newer
grep -nE '\bdb\b|sqldata|mssql' compose.edge.yml             # should print nothing; if the edge file mentions db or sqldata, stop and tell the owner
grep -c '^POSTGRES_PASSWORD=' .env                           # 0 (added in A.2, step 2)
docker pull postgres:18                                      # saves time in the window
```

Get the dump into `backups/` (browser + `scp`, or on the host `pip install --user gdown` then
`gdown 1ri78j75J4R4C-lBZYXmucMGJjZTwT6cP -O backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump`), then check A1:

```bash
chmod 644 backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump
ls -l backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump
sha256sum backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump
file backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump
```

### A.2 The cutover (in the window)

```bash
cd /opt/smart-hadith-tree
OLD=$(git rev-parse HEAD); echo "rollback point: $OLD"

# 1. Final SQL Server backup (T6). Expect "BACKUP DATABASE successfully processed".
docker compose exec -T db bash -c '/opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -b -t 0 -Q "BACKUP DATABASE SmartHadithTree TO DISK=N'\''/backups/final_sqlserver_'"$(date -u +%F)"'.bak'\'' WITH COPY_ONLY, COMPRESSION, CHECKSUM, INIT"'
ls -l backups/final_sqlserver_*.bak

# 2. The new password (T4). Keep MSSQL_SA_PASSWORD for now.
printf '\nPOSTGRES_PASSWORD=%s\n' "$(openssl rand -base64 24 | tr -d '/+=')" >> .env
grep -c '^POSTGRES_PASSWORD=' .env                           # 1

# 3. New code, API stopped, database container replaced (API is down from here). .env, compose.edge.yml and
#    backups/ are untracked and survive the reset.
git fetch --quiet origin master && git reset --hard origin/master
docker compose stop api
docker compose up -d db                                      # postgres:18 on the new pgdata volume; sqldata is kept
until [ "$(docker inspect -f '{{.State.Health.Status}}' "$(docker compose ps -q db)")" = healthy ]; do sleep 2; done

# 4. Restore (about 3 minutes, no output on success)
docker compose exec -T db pg_restore -U sht -d smart_hadith_tree --clean --if-exists --no-owner \
  /backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump
echo "pg_restore exit: $?"                                   # 0

# 5. Redeploy. The db image now matches, so the check lets it through. Expect a pre-deploy dump
#    (backups/predeploy_*.dump, 1 to 2 minutes), the image builds and "OK, <commit> is live".
./deploy/deploy-remote.sh "$OLD"
```

If step 5 fails, `deploy-remote.sh` puts the previous images back itself, but the database is already PostgreSQL: fix a
quick and obvious cause, otherwise use A.4.

### A.3 Check

Run the commands of section 6 (A2 to A12).

### A.4 Roll back (section 7)

```bash
cd /opt/smart-hadith-tree
git reset --hard "$OLD"                               # the commit written down in A.1
docker compose up -d db                               # SQL Server again, on the untouched sqldata volume
docker compose build api web && docker compose up -d  # the old images
```

### A.5 Nightly backup (P1)

```cron
30 2 * * * cd /opt/smart-hadith-tree && docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f /backups/nightly_$(date +\%F).dump' && find backups -name 'nightly_*.dump' -mtime +14 -delete
```
