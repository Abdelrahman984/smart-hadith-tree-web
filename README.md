# Smart Hadith Tree (شجرة الأسانيد الذكية)

A MENA-targeted SaaS platform and scholarly research tool designed to digitize, visualize, and critically analyze Hadith narrator chains (Isnad) dynamically. It combines a 1-million+ edge graph database of classical Hadith transmissions with a rule-based **Ilal (Hidden Defects) Engine** and AI-powered Retrieval-Augmented Generation (RAG) to synthesize scholar evaluations (Jarh and Ta'deel).

## Corpus Scale
- **31 Canonical Sunni Collections**: Covering the Sihah, Sunan, Early Musannafat (`موطأ مالك`, `مصنف عبد الرزاق`, `مصنف ابن أبي شيبة`), Major Masanid (`مسند أحمد`, `الطيالسي`, `الشافعي`, `الحميدي`, `إسحاق بن راهويه`, `البزار`, `أبو يعلى`), Mustakhrajat & Mustadrakat (`صحيح ابن خزيمة`, `صحيح ابن حبان`, `مستخرج أبي عوانة`, `المستدرك للحاكم`), Ma'ajim (`المعجم الكبير والأوسط والصغير للطبراني`), and Sunan Kubra (`السنن الكبرى للنسائي والبيهقي`, `سنن الدارقطني`, `سنن سعيد بن منصور`, `شعب الإيمان`).
- **233,224 Complete Hadiths & Athar**: Fully indexed and normalized for instant full-text and chain search.
- **1,078,668 Isnad Transmission Links**: Contextually disambiguated Sheikh → Student graph edges.
- **115,735 Narrator Profiles**: Linked with classical Jarh wa Ta'deel evaluations, Tabaqat, Mudallis tiers, and Ikhtilat records.

## Project Structure
- `src/`: ASP.NET Core 9 Web API backend & ETL pipeline using Clean Architecture.
- `frontend/`: Next.js (App Router) React & TypeScript application.
- `docs/`: Technical documentation, Ilal engine specification, and changelogs.
- `scripts/`: PowerShell, Python, and Java utilities for dataset setup and Shamela 4 Lucene extraction.
- `backups/`: Local SQL Server `.bak` database backups (git-ignored).
- `tests/` & `src/SmartHadithTree.Tests/`: xUnit test suites for Domain, Application, Infrastructure, API, and ETL.

## Tech Stack
- **Frontend**: Next.js (App Router), React, TypeScript, Tailwind CSS, React Flow (with `elkjs` layout engine & `html-to-image` exports), Zustand, TanStack Query.
- **Backend**: C# 13, .NET 9, ASP.NET Core Controllers & Services.
- **Database**: SQL Server, Entity Framework Core 9 (with Bulk Extensions, Recursive CTEs for graph traversal, & Normalized Search).
- **AI**: Microsoft Semantic Kernel (Gemini integration for Rijal RAG summaries and Ilal explanations).
- **Data Sources**: [R3GENESI5/Itqan](https://github.com/R3GENESI5/Itqan) + Local Shamela 4 Lucene/SQLite extraction pipeline (`scripts/shamela4-extractor/`).

## Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- SQL Server (Local instance `.` or LocalDB)

### 1. Database Setup & Data Ingestion
#### Option A: Instant Restore from Full Backup (Recommended if `.bak` is present)
If `backups/SmartHadithTree_31Books_Full.bak` is present locally, restore the complete 31-book database in seconds:
```powershell
sqlcmd -S . -Q "RESTORE DATABASE [SmartHadithTree] FROM DISK = N'd:\Programming\Full-Stack\Smart-Hadith-Tree\backups\SmartHadithTree_31Books_Full.bak' WITH REPLACE, STATS = 25;"
```

#### Option B: Run Migrations & ETL Pipeline from `data/itqan`
1. Ensure SQL Server is running and update the connection string in `src/SmartHadithTree.Api/appsettings.json` and `src/SmartHadithTree.Etl/appsettings.json` if needed.
2. Run the database migrations:
   ```powershell
   dotnet ef database update --project src/SmartHadithTree.Infrastructure --startup-project src/SmartHadithTree.Api
   ```
3. Ingest the unified 31-book corpus in `data/itqan`:
   ```powershell
   dotnet run --project src/SmartHadithTree.Etl -- data/itqan
   ```
4. Seed the Ilal engine teacher/student relations, Mudallisin, and Mukhtalitun:
   ```powershell
   dotnet run --project src/SmartHadithTree.Etl -- seed-ilal data/itqan
   ```

### 2. Running the Backend
Set your Google Gemini API key in user secrets or environment variables for AI narrator summaries and Ilal explanations:
```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY" --project src/SmartHadithTree.Api
```

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

## Core Features
- **Visual Isnad Trees & Comparative Takhreej**: Dynamically renders single-hadith and multi-hadith comparative transmission graphs (`ELK.js` + `React Flow`) highlighting the common Madar (pivot narrator) across 31 collections.
- **Ilal (Hidden Defects) Engine**: Detects 6 major categories of Hadith defects (`Tadlis`, `Ikhtilat`, `Hidden Inqita'`, `Matn Discrepancy at Madar`, `Raf'/Waqf Conflict`, `Wasl/Irsal Conflict`) with interactive graph overlays and Gemini AI scholarly explanations.
- **Contextual Narrator Disambiguation**: Resolves shared/ambiguous narrator names in Isnads using the 115k-node teacher-student graph and compiler/era overrides.
- **AI Narrator Summaries (RAG)**: Synthesizes classical Jarh wa Ta'deel quotes into a concise Arabic scholarly verdict.
- **Advanced Search & Book Browser**: Instant normalized search across 233k+ Hadiths with narrator-in-chain filters and hierarchical browsing across all 31 collections (`/books`).

## Documentation
For detailed technical documentation, refer to the `docs/` folder:
- [Architecture](docs/architecture.md)
- [Data Ingestion & Shamela 4 Extractor](docs/data_ingestion.md)
- [Ilal Engine Design](docs/ilal.md)
- [Features Changelog](docs/changelog.md)
- [Missing Books Roadmap](docs/missing-books-roadmap.md)
- [API Reference](docs/api.md)
