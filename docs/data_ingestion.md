# Data Ingestion (ETL) & Corpus Architecture

The Smart Hadith Tree relies on classical Hadith and Rijal data ingested from verified sources into SQL Server via our C# Console Application (`src/SmartHadithTree.Etl`).

## Current Corpus Scale (31 Canonical Sunni Collections)
- **Total Collections:** `31` canonical Sunni Hadith works
- **Total Hadiths:** `233,224` complete, normalized Arabic Hadiths & Athar
- **Total Isnad Links (`Transmissions`):** `1,078,668` directed Sheikh → Student links
- **Total Narrators (`Narrators`):** `115,735` biographical profiles with classical Jarh wa Ta'deel evaluations

---

## 1. Unified Dataset Architecture (`data/itqan/`)

All 31 collections are unified under `data/itqan/sunni/<slug>/` in a single standardized JSON format (`index.json` + numbered chapter files `1.json`, `2.json`, ...) and parsed by [`ItqanDatasetParser.cs`](../src/SmartHadithTree.Etl/Parsers/Itqan/ItqanDatasetParser.cs):

### A. Primary 12 Collections (Native Itqan Source)
1. `bukhari` — صحيح البخاري (Compiler ID: `336`)
2. `muslim` — صحيح مسلم (Compiler ID: `618`)
3. `abudawud` — سنن أبي داود (Compiler ID: `74`)
4. `tirmidhi` — جامع الترمذي (Compiler ID: `297`)
5. `nasai` — سنن النسائي (Compiler ID: `134`)
6. `ibnmajah` — سنن ابن ماجه (Compiler ID: `514`)
7. `ahmed` — مسند أحمد (Compiler ID: `353`)
8. `malik` — موطأ مالك (Compiler ID: `664`)
9. `darimi` — سنن الدارمي (Compiler ID: `168`)
10. `aladab_almufrad` — الأدب المفرد (Compiler ID: `336`)
11. `shamail_muhammadiyah` — الشمائل المحمدية (Compiler ID: `297`)
12. `musannaf_ibnabi_shaybah` — مصنف ابن أبي شيبة (Compiler ID: `748`)

### B. Expanded 19 Collections (Extracted 100% Complete from Shamela 4 Local Store)
13. `musannaf_abdurrazzaq` — مصنف عبد الرزاق (Shamela ID: `13174`, Compiler ID: `44`, `18,422` hadiths)
14. `mujam_kabir_tabarani` — المعجم الكبير للطبراني (Shamela ID: `1733`, Compiler ID: `202`, `14,549` hadiths)
15. `mustakhraj_abi_awanah` — مستخرج أبي عوانة (Shamela ID: `18144`, Compiler ID: `1123`, `13,036` hadiths)
16. `sunan_kubra_bayhaqi` — السنن الكبرى للبيهقي (Shamela ID: `148486`, Compiler ID: `34`, `11,655` hadiths)
17. `sunan_kubra_nasai` — السنن الكبرى للنسائي (Shamela ID: `8361`, Compiler ID: `134`, `11,444` hadiths)
18. `mujam_awsat_tabarani` — المعجم الأوسط للطبراني (Shamela ID: `28171`, Compiler ID: `202`, `9,444` hadiths)
19. `sahih_ibn_hibban` — صحيح ابن حبان (Shamela ID: `537`, Compiler ID: `706`, `7,447` hadiths)
20. `musnad_abi_yala` — مسند أبي يعلى الموصلي (Shamela ID: `12520`, Compiler ID: `462`, `7,333` hadiths)
21. `shuab_iman_bayhaqi` — شعب الإيمان للبيهقي (Shamela ID: `10660`, Compiler ID: `34`, `6,215` hadiths)
22. `mustadrak_hakim` — المستدرك على الصحيحين (Shamela ID: `1424`, Compiler ID: `10`, `5,799` hadiths)
23. `musnad_bazzar` — مسند البزار (Shamela ID: `12981`, Compiler ID: `196`, `4,470` hadiths)
24. `sunan_daraqutni` — سنن الدارقطني (Shamela ID: `9771`, Compiler ID: `460`, `4,231` hadiths)
25. `musnad_tayalisi` — مسند أبي داود الطيالسي (Shamela ID: `1456`, Compiler ID: `171`, `2,891` hadiths)
26. `sahih_ibn_khuzaymah` — صحيح ابن خزيمة (Shamela ID: `1446`, Compiler ID: `278`, `2,851` hadiths)
27. `sunan_said_ibn_mansur` — سنن سعيد بن منصور (Shamela ID: `13122`, Compiler ID: `1959`, `2,712` hadiths)
28. `musnad_ishaq` — مسند إسحاق بن راهويه (Shamela ID: `13159`, Compiler ID: `695`, `2,083` hadiths)
29. `musnad_shafii` — مسند الشافعي (Shamela ID: `9344`, Compiler ID: `2734`, `1,678` hadiths)
30. `musnad_humaydi` — مسند الحميدي (Shamela ID: `8493`, Compiler ID: `82`, `1,214` hadiths)
31. `mujam_saghir_tabarani` — المعجم الصغير للطبراني (Shamela ID: `13068`, Compiler ID: `202`, `1,187` hadiths)

---

## 2. How Shamela 4 Local Extraction Works (`scripts/shamela4-extractor/`)

Unlike Shamela 3 (which stored raw Arabic text in SQLite `bpage.nass` and `title.tit` columns), **Shamela 4** splits storage into:
1. **SQLite structural databases (`database/book/<id%1000>/<id>.db`)**:
   - `page (id, part, page, number, services)` — maps internal `page.id` to printed `part`/`page` and the canonical hadith `number`.
   - `title (id, page, parent)` — maps chapter hierarchy (`title.id` and `parent`) to starting `page.id`.
2. **Apache Lucene 10.4.0 Stores (`database/store/page` and `database/store/title`)**:
   - Holds the actual UTF-8 Arabic text (`body` and `foot`) indexed by `book_key = "<bookId>"` and `id = "<bookId>-<itemId>"`.

To extract books from a local Shamela 4 installation in seconds:
```powershell
# Step 1: Compile ShamelaLuceneDumper.java (uses reflection so any JDK 17+ can compile it)
javac -encoding UTF-8 scripts/shamela4-extractor/ShamelaLuceneDumper.java

# Step 2: Run using Shamela 4's bundled OpenJDK 21 JRE and Lucene 10.4.0 JARs
& "D:\Islamic\shamela4\app\win\64\jre\2\bin\java.exe" "--add-modules=jdk.incubator.vector" `
  -cp "D:\Islamic\shamela4\app\lucene\2\*;scripts\shamela4-extractor" ShamelaLuceneDumper `
  "D:\Islamic\shamela4\database\store" "data\shamela_dump"

# Step 3: Build standardized Itqan JSON chapters in data/itqan/sunni/<slug>/
python scripts/shamela4-extractor/build_itqan_books.py "data\shamela_dump" "D:\Islamic\shamela4\database\book"
```

---

## 3. Running the ETL Pipeline

1. **Ingest All New Collections (`data/itqan`)**:
   ```powershell
   dotnet run --project src/SmartHadithTree.Etl -- data/itqan
   ```
   `ItqanDatasetParser` automatically skips books already present in `dbo.Hadiths`, resolves narrators via `ContextualDisambiguator`, and bulk-inserts new Hadiths and Transmissions.

2. **Reprocess Isnad Chains for Existing Books (`reprocess-chains`)**:
   ```powershell
   # Reprocess all books or pass an optional book slug/name filter as 3rd arg
   dotnet run --project src/SmartHadithTree.Etl -- reprocess-chains data/itqan
   ```

3. **Seed Teacher/Student Relations, Mudallisin & Mukhtalitun (`seed-ilal`)**:
   ```powershell
   dotnet run --project src/SmartHadithTree.Etl -- seed-ilal data/itqan
   ```

---

## 4. Database Backup & Instant Restore

The latest verified full SQL Server backup of the 31-book database (`237,558` Hadiths, `1,110,677` Transmissions, `115,735` Narrators) is stored at:
- `backups/SmartHadithTree_v2_2026-10-03.bak` (~466 MB, compressed). Older versions and their contents are listed in [`backups/README.md`](../backups/README.md).

To restore instantaneously on a local SQL Server instance:
```powershell
sqlcmd -S . -Q "RESTORE DATABASE [SmartHadithTree] FROM DISK = N'd:\Programming\Full-Stack\Smart-Hadith-Tree\backups\SmartHadithTree_v2_2026-10-03.bak' WITH REPLACE, STATS = 25;"
```
