# Deployment

The stack is three parts: SQL Server, the ASP.NET Core API, and the Next.js frontend.
All environment-specific values are configuration; nothing is hardcoded.

## Configuration

| Part | Setting | Purpose |
|---|---|---|
| Frontend (build time) | `NEXT_PUBLIC_API_BASE` | Public API URL, e.g. `https://api.example.com/api`. Inlined into the client bundle, so changing it needs a rebuild. Defaults to `http://localhost:5147/api`. |
| API | `ConnectionStrings__DefaultConnection` | SQL Server connection string. |
| API | `Gemini__ApiKey` | Gemini key for AI summaries and Ilal explanations. Without it those features fail gracefully. |
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

Single-host stack with SQL Server:

```bash
export MSSQL_SA_PASSWORD='<strong password>'
export GEMINI_API_KEY='<key>'
docker compose up --build
```

The database is not created with data automatically. The API applies EF migrations on start (empty schema);
load the corpus either by restoring `backups/SmartHadithTree_31Books_Full.bak` (git-ignored, mounted read-only at `/backups`
in the `db` container) or by running the ETL (`docs/data_ingestion.md`).

## Status

The Dockerfiles and compose file have **not been built or run** yet (no container runtime was available when they
were written). The frontend production build (`npm run build`) was verified, including that `NEXT_PUBLIC_API_BASE`
is inlined and no `localhost` URL remains in the client bundle. The API changes (configurable CORS, optional HTTPS
redirection) have not been compiled in CI. Build and smoke-test before relying on them.
