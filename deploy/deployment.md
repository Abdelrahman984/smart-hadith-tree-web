# Deployment

For a production server see `deploy/devops-handoff.md` (runbook for the DevOps engineer) and `docker-compose.prod.yml`.
This file covers configuration and the local Docker stack.
The hadith data is not in git: restore the database backup first (see "The database is not created with data automatically" below and `deploy/devops-handoff.md` section 3).

The stack is three parts: PostgreSQL 18, the ASP.NET Core API, and the Next.js frontend.
All environment-specific values are configuration; nothing is hardcoded.

## Configuration

| Part | Setting | Purpose |
|---|---|---|
| Frontend (build time) | `NEXT_PUBLIC_API_BASE` | Public API URL, e.g. `https://api.example.com/api`. Inlined into the client bundle, so changing it needs a rebuild. Defaults to `http://localhost:5147/api`. |
| API | `ConnectionStrings__DefaultConnection` | PostgreSQL connection string (Npgsql), e.g. `Host=db;Port=5432;Database=smart_hadith_tree;Username=sht;Password=...`. Without `Password`, Npgsql reads `PGPASSWORD` or `pgpass.conf`. |
| API | `Together__ApiKey` | Together AI key (model `zai-org/GLM-5.3-Flash`, override with `Together__Model`) for AI summaries and Ilal explanations. Takes precedence over Gemini. |
| API | `Together__ExtraBody` | JSON object merged into every chat request. Set `{"reasoning_effort":"low"}` for `zai-org/GLM-5.3-Flash` (turns the model's thinking down; AI check 3–4 s instead of 8–27 s). In Docker it comes from `TOGETHER_EXTRA_BODY`. |
| API | `Gemini__ApiKey` | Gemini key for AI summaries and Ilal explanations. Without it those features fail gracefully. |
| API | `VERIFY_REVIEW_TIMEOUT_SECONDS` | Time limit for the model review step of `/verify` (default 8, range 2–60). On timeout the answer comes from text matching alone. See `docs/verify-feature-handoff.md`. |
| API | `Cors__AllowedOrigins__0`, `__1`, ... | Browser origins allowed to call the API (the frontend URL). Defaults to `http://localhost:3000`. |
| API | `DisableHttpsRedirection=true` | Set when TLS is terminated by a proxy/platform (already set in the Docker image). |

Never commit real keys. Use platform secrets or `dotnet user-secrets` locally.

## Docker

```bash
# API (context = repository root)
docker build -f src/SmartHadithTree.Api/Dockerfile -t hadith-api .
# Frontend (context = frontend/)
docker build --build-arg NEXT_PUBLIC_API_BASE=https://api.example.com/api -t hadith-web frontend
```

Single-host stack with PostgreSQL:

```bash
export POSTGRES_PASSWORD='<strong password>'
export TOGETHER_API_KEY='<key>'   # preferred; or GEMINI_API_KEY as the fallback
docker compose up -d db
docker compose exec -T db pg_restore -U sht -d smart_hadith_tree --clean --if-exists --no-owner /backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump
docker compose up -d --build api web
```

The database is not created with data automatically. The API applies EF migrations on start (empty schema);
load the corpus either by restoring the current backup `backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump`
as above (31 books, 274,597 hadiths; git-ignored, mounted read-only at `/backups` in the `db` container; versions and
checksums in `backups/README.md`) or by rebuilding it with the Shamela pipeline (`docs/data_ingestion.md`,
`docs/shamela_migration.md`). The database is published on `localhost:5433` (`DB_PORT`), user `sht`, database
`smart_hadith_tree`, so the ETL and `psql` on the host can reach it.

## Status

Verified on 2026-10-21 with Docker Desktop on Windows, PostgreSQL stack (the commands above, as a separate compose
project): `db` healthy in 10 s, the v8 restore ran without errors in 84 s, `docker compose up -d --build api web`
built both images. `/api/books` lists 31 books, `/api/search` and `/api/narrators/search` return data, the web
container answers 200 on port 3000, the CORS header for `http://localhost:3000` is present, and the API logged no
migration failure.

Notes:
- Put `POSTGRES_PASSWORD` in a git-ignored `.env` file next to `docker-compose.yml` (compose reads it automatically).
  It is read only when the `pgdata` volume is created.
- Start `db` first, restore, then start `api` and `web`: `docker compose up -d db`, restore, `docker compose up -d api web`.
- The first web build once failed while next/font fetched Google Fonts and passed on retry, so the build needs
  internet access.
- The SQL Server stack of before 2026-10-21 (tag `pre-postgres`) was verified the same way on 2026-10-05 with the v5 backup.
- Not yet checked: AI summaries and Ilal explanations (they need a Together or Gemini key), and `/verify` end to end.
