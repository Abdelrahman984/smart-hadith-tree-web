# DevOps tasks: switch production from SQL Server to PostgreSQL

For the DevOps engineer of the Smart Hadith Tree host. The commands for the cutover itself are in
`deploy/devops-handoff.md`, section "Cutover to PostgreSQL"; this file is the ordered checklist around them, with
what to expect at each step. Background: `docs/postgres_migration.md`.

## Where things stand (2026-10-21)

- **Production** (`/opt/smart-hadith-tree`, https://smart-hadith.idealisticsolutions.com) runs the **SQL Server**
  stack at commit `f9b232d`. Checked after the merge below: the site answers, 31 books, and narrator search for
  «قتادة» returns nothing, which is how the SQL Server build behaves (the PostgreSQL build returns up to 50).
- **`master`** has the PostgreSQL code since PR #6 (merge `68dfdb7`).
- **Every deploy from `master` is refused until the cutover**, by design: `deploy/deploy-remote.sh` stops when the
  `db` image would change, resets the checkout to the live commit and exits 1. The GitHub Actions run is red and the
  live site is untouched.
- **What happened on the first try** (run 39524924844): the tests passed, but that first version of the check
  failed open. The host's `.env` has no `POSTGRES_PASSWORD`, so `docker compose` could not read the new compose file,
  the check saw no `db` and let the deploy through. It then failed at the pre-deploy backup on the same missing
  variable and rolled back, and the same error stopped every `docker compose` command before it touched a container.
  Nothing changed on the host. The check now fails closed (branch `fix/deploy-guard-fail-closed`): it reads the
  running `db` from Docker labels and refuses when the compose file cannot be read. **Task 1 makes sure that
  version is on `master` before anything else.**

## What you need from the project owner

- [ ] The backup `SmartHadithTree_Shamela_v8_pg_2026-10-21.dump` (153 MB, PostgreSQL 18 `pg_dump -Fc`), sent privately.
  SHA-256 `73593f3e9832076a9aed85ef8324c63bf38eab33fe9418f9984b4167d6f78f94` (also in `backups/README.md`).
- [ ] A maintenance window of about 15 minutes. The API is down while the database is replaced, restored and the
  images rebuilt; the web pages load but their API calls fail meanwhile.

Domains, ports, the edge proxy, the Together key and the deploy key do not change.

## Tasks

### 1. Make sure the fail-closed check is on `master`

- [ ] After the owner has merged `fix/deploy-guard-fail-closed`, on the host:
  ```bash
  cd /opt/smart-hadith-tree && git fetch --quiet origin master
  git show origin/master:deploy/deploy-remote.sh | grep -c "cannot be read"    # must print 1
  ```
  That merge's own deploy is refused too (red run, log line `the new compose configuration cannot be read,
  refusing`). That is expected and confirms the check.

### 2. Pre-checks on the host (no changes)

- [ ] `git rev-parse --short HEAD` prints the live commit (`f9b232d`, or a later SQL Server one). Write it down: it
  is the rollback point (`$OLD` in the runbook).
- [ ] `docker compose ps` shows `db` (`mcr.microsoft.com/mssql/server:2022-latest`), `api`, `web` up.
- [ ] `docker volume ls | grep smart-hadith-tree_sqldata`: the SQL Server data. The cutover keeps it; do not remove it.
- [ ] `df -h /opt /var/lib/docker`: at least 5 GB free (dump 0.2 GB, restored database about 1.5 GB, a pre-deploy
  dump, new images; the old SQL Server volume stays until task 7).
- [ ] `docker compose version`: v2.24 or newer.
- [ ] `grep -nE 'db|sqldata|mssql' compose.edge.yml`: the host-only edge file must not reference the `db` service or
  the `sqldata` volume (it should only join `api`/`web` to the edge network). If it does, stop and tell the owner.
- [ ] `grep -c '^POSTGRES_PASSWORD=' .env` prints `0` (it is added during the cutover).

### 3. Put the backup in place

- [ ] Copy the dump to `/opt/smart-hadith-tree/backups/`.
- [ ] `sha256sum backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump` prints the SHA-256 above.

### 4. Cutover (in the maintenance window)

Run section "Cutover to PostgreSQL" of `deploy/devops-handoff.md`, steps 0 to 5, in `/opt/smart-hadith-tree`.
What to expect:

| Step | What it does | Expect |
|---|---|---|
| 0 | note `$OLD`, check the dump | the SHA-256 matches |
| 1 | last SQL Server backup `backups/final_sqlserver_<date>.bak` | `BACKUP DATABASE successfully processed`, a file of about 0.5 GB |
| 2 | add `POSTGRES_PASSWORD` to `.env` | one new line; keep `MSSQL_SA_PASSWORD` for now |
| 3 | check out `origin/master`, stop `api`, `up -d db` | `db` recreated as `postgres:18`, then `healthy` within about 20 s |
| 4 | `pg_restore --clean --if-exists` | no output, about 3 minutes |
| 5 | `./deploy/deploy-remote.sh "$OLD"` | `migrations may run, backing up …predeploy_….dump` (a dump of the just-restored database, about 1–2 min), image builds, then `OK, <commit> is live` |

If step 5 fails, `deploy-remote.sh` rolls the images back by itself, but the database is already PostgreSQL. Then
roll back completely ("Roll back" at the end of task 5) unless the cause is obvious and quick to fix.

### 5. Verify

- [ ] The smoke test of `deploy/devops-handoff.md` section 4 (books, search, `/api/admin` 404, web 200, no
  "migration check failed" in the api log).
- [ ] PostgreSQL is really serving:
  ```bash
  docker compose ps db                                   # image postgres:18, (healthy)
  curl -s "https://$API_DOMAIN/api/books" | head -c 120  # 31 book names, not []
  curl -s "https://$API_DOMAIN/api/narrators/search?q=%D9%82%D8%AA%D8%A7%D8%AF%D8%A9" | head -c 200   # «قتادة»: results, not []
  ```
  (On SQL Server the narrator search returned `[]`; a non-empty answer shows the new build and database are live.)
- [ ] CI path: GitHub, Actions, workflow "deploy", "Run workflow" on `master`. Expect a **green** run: the database
  image now matches, so the check lets it through.

**Roll back** if the site is broken and the cause is not quick to fix: the three commands under "Roll back" in the
runbook section (check out `$OLD`, `up -d db` brings SQL Server back on the untouched `sqldata` volume, rebuild).
Do not use the old `deploy-remote.sh` alone for it (explained there).

### 6. Report to the owner

- [ ] The live commit after the cutover, the output of step 5, the smoke-test output, and how long the API was down.
- [ ] Anything that differed from this file or the runbook, so both can be corrected. Then mark item 8 of section 8
  in `deploy/devops-handoff.md` ("Not run on the real host") as done.

### 7. Afterwards

- [ ] **Nightly backup** (none is scheduled today), kept off-host as well. For example in root's crontab:
  ```cron
  30 2 * * * cd /opt/smart-hadith-tree && docker compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" -Fc -f /backups/nightly_$(date +\%F).dump' && find backups -name 'nightly_*.dump' -mtime +14 -delete
  ```
  Restore test now and then: `pg_restore` into a throwaway `postgres:18` container (`backups/README.md`).
- [ ] **After about a week on PostgreSQL** with no rollback: remove `MSSQL_SA_PASSWORD` from `.env`, the old volume
  (`docker volume rm smart-hadith-tree_sqldata`), the SQL Server image (`docker image rm mcr.microsoft.com/mssql/server:2022-latest`)
  and old `backups/*.bak` files you do not want to keep. Keep `final_sqlserver_<date>.bak` off-host if the owner wants it.
- [ ] Recommended (`deploy/devops-handoff.md` section 8): a non-superuser database role for the api's connection
  string (`sht` is a superuser because the restore needs `CREATE EXTENSION pg_trgm`); log rotation for the
  containers; `Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning` to stop logging every SQL command.

Nothing to do for `deploy/ssh-deploy-gate.sh`: only a comment changed, so the installed copy in `/usr/local/sbin/`
stays as it is.
