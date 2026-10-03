# Database Backups

Full SQL Server backups of the `SmartHadithTree` database live in this folder (git-ignored, local only).
Each file is a numbered version so later data migrations (e.g. the move from Itqan to Shamela 4) can always be compared against, or rolled back to, a known state.

| Version | File | Taken | Size | Compression / Checksum | SHA-256 |
|---|---|---|---|---|---|
| v1 | `SmartHadithTree_v1_2026-10-02.bak` | 2026-10-02 21:24 | 1,390 MB | No / No | `74353394703e7c896ec48c140df766b199cc1598667a6afe7eb8767bebb62945` |
| v2 | `SmartHadithTree_v2_2026-10-03.bak` | 2026-10-03 17:34 | 466 MB | Yes / Yes | `ba808ddd30021a56e162593f29a0363a5e9131d6f5f1c8b82d99b334463f61c2` |

Both files pass `RESTORE VERIFYONLY` (checked 2026-10-03).
The counts below were taken by restoring each backup and querying it directly.

---

## v1 — 31 books, first full ingestion

Previously named `SmartHadithTree_31Books_Full.bak`. Taken right after the 19 Shamela 4 books were ingested alongside the 12 Itqan books (see `docs/changelog.md`, "Database Backup").

| Table | Rows |
|---|---|
| Hadiths | 233,224 |
| Transmissions | 1,078,668 |
| Narrators | 115,735 |
| ScholarEvaluations | 106,164 |
| NarratorRelations / MukhtalitHearings | 0 / 0 |

- **Schema:** 9 EF migrations, last is `20261002140819_AddNarratorGeographyAndStats`. Missing `OptimizeSearchCollationAndIndexes`; the API applies it automatically on startup.
- **Sources:** hadith texts from Itqan (12 books) + Shamela 4 (19 books); all narrator profiles and Jarh wa Ta'deel from Itqan rijal.
- **Git state:** up to commit `88a5a0b` (2026-10-02 21:50).

## v2 — 31 books, reprocessed chains (pre-Shamela-migration baseline)

Current database as of 2026-10-03. This is the **baseline for the planned migration away from Itqan** to Shamela 4 as the sole source.

| Table | Rows | Change vs v1 |
|---|---|---|
| Hadiths | 237,558 | +4,334 |
| Transmissions | 1,110,677 | +32,009 |
| Narrators | 115,735 | — |
| ScholarEvaluations | 106,164 | — |
| NarratorRelations / MukhtalitHearings | 0 / 0 | — |

- **Schema:** 10 EF migrations, last is `20261002192731_OptimizeSearchCollationAndIndexes`.
- **Git state:** up to commit `a49b7af` (2026-10-03 16:05).
- **What changed since v1:**
  - `mujam_kabir_tabarani` grew from 14,549 to **18,883** hadiths (the whole `+4,334`).
  - Isnad chains were re-built with `reprocess-chains` after the narrator-segment cleaning and `ContextualDisambiguator` changes (`5b07f2d`, `3327aef`, `2bfc6a1`). Transmission counts moved in most books; the largest shifts:

    | Book | v1 | v2 |
    |---|---|---|
    | `mujam_kabir_tabarani` | 55,290 | 108,186 |
    | `mujam_awsat_tabarani` | 31,359 | 51,488 |
    | `sunan_daraqutni` | 12,435 | 26,042 |
    | `sunan_kubra_bayhaqi` | 126,322 | 84,516 |
    | `shuab_iman_bayhaqi` | 60,899 | 42,015 |
    | `mustadrak_hakim` | 51,401 | 39,660 |

## Known gaps (both versions)

- `seed-ilal` has not been run: `NarratorRelations`, `MukhtalitHearings` are empty and no narrator is flagged as mudallis/mukhtalit. Run `dotnet run --project src/SmartHadithTree.Etl -- seed-ilal data/itqan` after restoring if the Ilal rules need them.

---

## Restore

Replace the file name with the version you want:

```powershell
sqlcmd -S . -Q "RESTORE DATABASE [SmartHadithTree] FROM DISK = N'D:\Programming\Full-Stack\Smart-Hadith-Tree\backups\SmartHadithTree_v2_2026-10-03.bak' WITH REPLACE, STATS = 25;"
```

To inspect an old version without touching the live database, restore it under another name with `MOVE` (files must go to a folder the SQL Server service can write to, e.g. its default data path):

```powershell
sqlcmd -S . -Q "RESTORE DATABASE [SHT_v1_inspect] FROM DISK = N'D:\Programming\Full-Stack\Smart-Hadith-Tree\backups\SmartHadithTree_v1_2026-10-02.bak' WITH MOVE 'SmartHadithTree' TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\SHT_v1_inspect.mdf', MOVE 'SmartHadithTree_log' TO N'C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\DATA\SHT_v1_inspect_log.ldf';"
```

## Taking a new version

Use the next number and today's date, then add a section to this file:

```powershell
sqlcmd -S . -Q "BACKUP DATABASE [SmartHadithTree] TO DISK = N'D:\Programming\Full-Stack\Smart-Hadith-Tree\backups\SmartHadithTree_v3_YYYY-MM-DD.bak' WITH INIT, FORMAT, COMPRESSION, CHECKSUM, NAME = N'SmartHadithTree v3 - <description>', STATS = 25; RESTORE VERIFYONLY FROM DISK = N'D:\Programming\Full-Stack\Smart-Hadith-Tree\backups\SmartHadithTree_v3_YYYY-MM-DD.bak' WITH CHECKSUM;"
```
