# Data Ingestion (ETL)

The Smart Hadith Tree relies on classical data ingested from robust external sources. We do not manually enter data; instead, we use a custom C# Console Application (`SmartHadithTree.Etl`) to parse raw files and bulk insert them into SQL Server.

## Current Data Sources

1. **R3GENESI5/Itqan GitHub Repository (Primary Source)**
   - **Source:** JSON dumps from the Itqan dataset.
   - **Data:** Contains 115k+ pre-disambiguated narrators, structured Jarh wa Ta'deel evaluations, and parsed chains for Sahih Al-Bukhari.
   - **Parsing Logic:** Our parser (`ItqanDatasetParser.cs`) reads the `profiles_*.json` for narrators and evaluations, and `bukhari/*.json` for the parsed transmission chains. It accurately builds `Narrator` and `Transmission` entities with mapped evaluations (`ScholarEvaluation`).

2. **Legacy/Fallback Sources**
   - **fawazahmed0**: JSON dumps containing raw text of the 9 major Hadith books. Parsed via heuristic string splitting (`FawazAhmedParser.cs`).
   - **Shamela v4**: Local SQLite files containing biographical data for scholars (`ShamelaAuthorParser.cs`).

## How to Run the ETL

1. First, download the raw data. We provide a PowerShell script that fetches the Itqan JSON files directly from GitHub:
   ```bash
   pwsh scripts/download_itqan_data.ps1
   ```
   *This will place the files inside the `data/itqan` directory.*

2. Run the ETL project, pointing it to the Itqan folder:
   ```bash
   dotnet run --project src/SmartHadithTree.Etl -- data/itqan
   ```

The orchestrator will automatically pick up the `ItqanDatasetParser`, parse the files, convert them to our Domain Entities (`HadithText`, `Narrator`, `Transmission`), and bulk insert them into the SQL Server database.
