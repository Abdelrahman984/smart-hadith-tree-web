# Data Ingestion (ETL)

The Smart Hadith Tree relies on classical data ingested from robust external sources. We do not manually enter data; instead, we use a custom C# Console Application (`SmartHadithTree.Etl`) to parse raw files and bulk insert them into SQL Server.

## Current Data Sources

1. **fawazahmed0 GitHub Repository**
   - **Source:** JSON dumps from `https://github.com/fawazahmed0/hadith-api`.
   - **Data:** Contains raw text of the 9 major Hadith books (Kutub al-Sittah + others).
   - **Parsing Logic:** Our parser (`FawazAhmedParser.cs`) reads the `ara-bukhari.json` file. Because this dataset combines the Isnad (chain) and Matn (text) into a single string, we use heuristic string splitting (e.g. looking for "قَالَ رَسُولُ اللَّهِ") to separate the Isnad from the Matn.

2. **Shamela v4 Database (المكتبة الشاملة)**
   - **Source:** Local SQLite files (`master.db`) from a Shamela v4 installation.
   - **Data:** Contains biographical data for over 3,000 scholars and authors.
   - **Parsing Logic:** Our parser (`ShamelaAuthorParser.cs`) connects directly to the Shamela SQLite database and extracts records from the `author` table, mapping them to our `Narrator` EF Core entity. 

## How to Run the ETL

1. First, download the raw data. We provide a PowerShell script that fetches the fawazahmed0 JSON files:
   ```bash
   pwsh scripts/download_real_data.ps1
   ```
   *(Note: For Shamela data, you must provide the path to your local `master.db` inside the ETL project's `appsettings.json` or connection string).*

2. Run the ETL project:
   ```bash
   dotnet run --project src/SmartHadithTree.Etl
   ```

The script will automatically parse the files, convert them to our Domain Entities (`HadithText`, `Narrator`), and insert them into the SQL Server database configured in `SmartHadithTree.Api/appsettings.json`.
