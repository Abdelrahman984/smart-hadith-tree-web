# Smart Hadith Tree (شجرة الأسانيد الذكية)

A MENA-targeted SaaS platform and scholarly research tool designed to digitize, visualize, and critically analyze Hadith narrator chains (Isnad) dynamically. It combines a 1-million+ edge graph database of classical Hadith transmissions with a rule-based **Ilal (Hidden Defects) Engine** and AI-powered Retrieval-Augmented Generation (RAG) to synthesize scholar evaluations (Jarh and Ta'deel).

## Corpus Scale
- **31 Canonical Sunni Collections**: Covering the Sihah, Sunan, Early Musannafat (`موطأ مالك`, `مصنف عبد الرزاق`, `مصنف ابن أبي شيبة`), Major Masanid (`مسند أحمد`, `الطيالسي`, `الشافعي`, `الحميدي`, `إسحاق بن راهويه`, `البزار`, `أبو يعلى`), Mustakhrajat & Mustadrakat (`صحيح ابن خزيمة`, `صحيح ابن حبان`, `مستخرج أبي عوانة`, `المستدرك للحاكم`), Ma'ajim (`المعجم الكبير والأوسط والصغير للطبراني`), and Sunan Kubra (`السنن الكبرى للنسائي والبيهقي`, `سنن الدارقطني`, `سنن سعيد بن منصور`, `شعب الإيمان`).
- **274,597 Hadiths & Athar**: each edition's own numbering, indexed and normalized for full-text and chain search.
- **1,217,978 Isnad Transmission Links**: Sheikh → Student edges; hadiths with several chains (tahwil) keep each chain (19,982 hadiths).
- **23,502 Narrator Profiles**: a registry built from Tahdhib al-Kamal, Taqrib al-Tahdhib and the compilers' shaykh books, with 116,670 attributed Jarh wa Ta'deel quotes, 115,816 teacher/student relations, 158 Mudallisin and 131 Mukhtalitun.
- **Sources**: every book and rijal reference comes from Shamela 4; see [`docs/challenge/sources-and-licenses.md`](docs/challenge/sources-and-licenses.md). The narrator and chain resolution is automatic and still makes mistakes; the known ones are tracked in [`docs/backlog.md`](docs/backlog.md).

## Project Structure
- `src/`: ASP.NET Core 9 Web API backend & ETL pipeline using Clean Architecture.
- `frontend/`: Next.js (App Router) React & TypeScript application.
- `docs/`: Technical documentation, Ilal engine specification, and changelogs.
- `scripts/`: PowerShell, Python, and Java utilities for dataset setup and Shamela 4 Lucene extraction.
- `backups/`: Local database backups (git-ignored): PostgreSQL `.dump` from v8 on, older SQL Server `.bak` files.
- `tests/` & `src/SmartHadithTree.Tests/`: xUnit test suites for Domain, Application, Infrastructure, API, and ETL.

## Tech Stack
- **Frontend**: Next.js (App Router), React, TypeScript, Tailwind CSS, React Flow (with `elkjs` layout engine & `html-to-image` exports), Zustand, TanStack Query.
- **Backend**: C# 13, .NET 9, ASP.NET Core Controllers & Services.
- **Database**: PostgreSQL 18, Entity Framework Core 9 with Npgsql (Bulk Extensions, Recursive CTEs for graph traversal, normalized Arabic columns with `pg_trgm` trigram indexes for search). Until 2026-10-21 it ran on SQL Server (git tag `pre-postgres`, [`docs/postgres_migration.md`](docs/postgres_migration.md)).
- **AI**: Microsoft Semantic Kernel (Gemini integration for Rijal RAG summaries and Ilal explanations).
- **Data Source**: a local Shamela 4 installation, read through the Lucene/SQLite extraction pipeline (`scripts/shamela4-extractor/`); see [`docs/data_ingestion.md`](docs/data_ingestion.md).

## Getting Started

**Just want to try it?** Open the live demo, no setup needed: <https://smart-hadith.idealisticsolutions.com>. The steps below run it locally.

### Prerequisites
- PostgreSQL 18: either Docker (the `db` service of `docker-compose.yml`, recommended) or a local installation (it includes the `pg_trgm` extension the schema needs). The commands below are PowerShell; on macOS/Linux the same steps work in a POSIX shell.
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (running the app). Running the test project (`src/SmartHadithTree.Tests`) needs the .NET 10 SDK, since it targets `net10.0`.
- [Node.js 18+](https://nodejs.org/)

### 1. Database Setup & Data Ingestion
#### Option A: Instant Restore from Full Backup (Recommended)
The database is not stored in git. Get the PostgreSQL backup (v8, about 153 MB, 31 books) and place it at `backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump` (the folder is git-ignored; download link and checksum in [`backups/README.md`](backups/README.md)). To only try the product, use the live demo instead: <https://smart-hadith.idealisticsolutions.com>. The source texts come from the Shamela library; see [`docs/challenge/sources-and-licenses.md`](docs/challenge/sources-and-licenses.md) for their rights status. From the repository root:
```powershell
$env:POSTGRES_PASSWORD = '<choose one>'
docker compose up -d db          # PostgreSQL 18 on localhost:5433, user sht, database smart_hadith_tree
docker compose exec -T db pg_restore -U sht -d smart_hadith_tree --clean --if-exists --no-owner /backups/SmartHadithTree_Shamela_v8_pg_2026-10-21.dump
# For the API and ETL run from this shell (both read these over appsettings.json):
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5433;Database=smart_hadith_tree;Username=sht'
$env:PGPASSWORD = $env:POSTGRES_PASSWORD
```
With a local PostgreSQL 18 instead: create the role `sht` and the database `smart_hadith_tree`, restore with `pg_restore -h localhost -U sht -d smart_hadith_tree --clean --if-exists --no-owner <file>` as a role that may create the `pg_trgm` extension, and set `PGPASSWORD` (the default connection string in `appsettings.json` is `Host=localhost;Port=5432;Database=smart_hadith_tree;Username=sht`, with no password in it).

#### Option B: Run Migrations & Load from `data/shamela_rijal`
Needs the data files built from a local Shamela 4 installation (see [`docs/data_ingestion.md`](docs/data_ingestion.md)).
1. Start an empty PostgreSQL 18 as in Option A (without the restore) and set the same environment variables.
2. Run the database migrations:
   ```powershell
   dotnet ef database update --project src/SmartHadithTree.Infrastructure --startup-project src/SmartHadithTree.Api
   ```
3. Load narrators, the 31-book corpus, isnad chains, quotes and relations:
   ```powershell
   dotnet run --project src/SmartHadithTree.Etl -- data/shamela_rijal
   ```
4. Load the Ilal data (Mudallisin, Mukhtalitun and their hearings):
   ```powershell
   dotnet run --project src/SmartHadithTree.Etl -- seed-ilal-shamela data/shamela_rijal
   ```

### 2. Running the Backend
The AI key is optional: without it the app runs fully (search, trees, takhreej, Ilal findings) and only the AI narrator summaries and Ilal explanations are unavailable. To enable them, set an AI API key in user secrets or environment variables. Together AI (default model `zai-org/GLM-5.3-Flash`) takes precedence; Google Gemini is the fallback:
```powershell
dotnet user-secrets set "Together:ApiKey" "YOUR_TOGETHER_KEY" --project src/SmartHadithTree.Api
# or: dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY" --project src/SmartHadithTree.Api
```
Optional: `Together:Model` overrides the model.

Start the API:
```powershell
dotnet run --project src/SmartHadithTree.Api
```
The API will run on `http://localhost:5147`.

### 3. Running the Frontend
In a new terminal:
```powershell
cd frontend
npm install
npm run dev
```
The frontend will run on `http://localhost:3000`.

To start the API and the frontend together, run `scripts/run.ps1` from the repository root.

## Core Features
- **Visual Isnad Trees & Comparative Takhreej**: Dynamically renders single-hadith and multi-hadith comparative transmission graphs (`ELK.js` + `React Flow`) highlighting the common Madar (pivot narrator) across 31 collections.
- **Ilal (Hidden Defects) Engine**: Detects 6 major categories of Hadith defects (`Tadlis`, `Ikhtilat`, `Hidden Inqita'`, `Matn Discrepancy at Madar`, `Raf'/Waqf Conflict`, `Wasl/Irsal Conflict`) with interactive graph overlays and Gemini AI scholarly explanations.
- **Narrator Resolution**: an offline Python resolver (`scripts/shamela4-extractor/rijal_pilot/`) matches each name in an isnad to a Tahdhib al-Kamal narrator using the teacher-student lists, and reads multi-chain isnads (tahwil). Names it cannot decide leave a gap in the chain.
- **AI Narrator Summaries (RAG)**: Synthesizes classical Jarh wa Ta'deel quotes into a concise Arabic scholarly verdict.
- **Advanced Search & Book Browser**: Instant normalized search across 274k+ Hadiths with narrator-in-chain filters and hierarchical browsing across all 31 collections (`/books`).

## Documentation
For detailed technical documentation, refer to the `docs/` folder:
- [Architecture](docs/architecture.md)
- [Data Ingestion & Shamela 4 Extractor](docs/data_ingestion.md)
- [Ilal Engine Design](docs/ilal.md)
- [Features Changelog](docs/changelog.md)
- [Missing Books Roadmap](docs/missing-books-roadmap.md)
- [API Reference](docs/api.md)

## Privacy and AI notice
- The application has no accounts and stores nothing about its users in the browser or the database.
- AI output (narrator summaries and Ilal explanations) is a reading aid, not a fatwa, and abstains when no quotation supports it. Ilal findings are automatic warnings with a confidence value and need a scholar's check.
