# Smart Hadith Tree (شجرة الأسانيد الذكية)

A MENA-targeted SaaS platform and research tool designed to digitize and visualize Hadith narrator chains (Isnad) dynamically. It combines robust relational databases for classical narrator data with AI-powered Retrieval-Augmented Generation (RAG) to summarize scholar evaluations (Jarh and Ta'deel).

## Project Structure
- `src/`: ASP.NET Core 9 Web API backend using Clean Architecture.
- `frontend/`: Next.js (App Router) React application.
- `docs/`: Technical documentation and architecture diagrams.
- `scripts/`: PowerShell scripts for setting up local data.
- `tests/`: xUnit tests for the backend.

## Tech Stack
- **Frontend**: Next.js (App Router), React, Tailwind CSS, React Flow (with `elkjs` layout engine & `html-to-image` exports).
- **Backend**: C# 13, .NET 9, ASP.NET Core Minimal APIs/Controllers.
- **Database**: SQL Server, Entity Framework Core 9 (with Recursive CTEs for graph traversal & Full-Text Search).
- **AI**: Microsoft Semantic Kernel (for RAG with Gemini-1.5-Pro).
- **Primary Data Source**: [R3GENESI5/Itqan](https://github.com/R3GENESI5/Itqan) (115k+ narrators, 7k+ parsed Hadiths, 100k+ structured scholar evaluations).

## Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- SQL Server (LocalDB or Docker)

### 1. Database Setup & Data Ingestion
1. Ensure SQL Server is running and accessible. Update the connection string in `src/SmartHadithTree.Api/appsettings.json` if necessary.
2. Run the database migrations:
   ```bash
   dotnet ef database update --project src/SmartHadithTree.Infrastructure --startup-project src/SmartHadithTree.Api
   ```
3. To download the primary **Itqan** dataset (~115k narrators, Sahih Al-Bukhari chains), use the ingestion script:
   ```bash
   pwsh scripts/download_itqan_data.ps1
   ```
4. Run the ETL project to ingest the structured data into the SQL Server database (may take a few minutes):
   ```bash
   dotnet run --project src/SmartHadithTree.Etl -- data/itqan
   ```

### 2. Running the Backend
Set your Google Gemini API key in user secrets or environment variables for the AI narrator summary to work:
```bash
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY" --project src/SmartHadithTree.Api
```

Start the API:
```bash
dotnet run --project src/SmartHadithTree.Api
```
The API will run on `http://localhost:5147`.

### 3. Running the Frontend
In a new terminal:
```bash
cd frontend
npm install
npm run dev
```
The frontend will run on `http://localhost:3000`.

## Core Features
- **Visual Isnad Trees**: Dynamically renders complex chains of transmission top-to-bottom using React Flow and ELK.js.
- **Graph Analytics (Inqita')**: Automatically detects and highlights temporal anomalies (e.g., a student born after their sheikh died) using red dashed edges on the graph.
- **AI Narrator Summaries**: Integrates Microsoft Semantic Kernel to synthesize conflicting classical Jarh wa Ta'deel quotes into a single Arabic verdict.
- **Full-Text Search**: Robust search functionality over Hadith Matn and Narrator aliases.
- **UI Filters**: Ability to dim reliable narrators to isolate weak links, and export graphs to high-resolution PNGs.

## Documentation
For more detailed documentation, please refer to the `docs/` folder:
- [Architecture](docs/architecture.md)
- [Data Ingestion](docs/data_ingestion.md)
- [Features Changelog](docs/changelog.md)
- [API Reference](docs/api.md)
