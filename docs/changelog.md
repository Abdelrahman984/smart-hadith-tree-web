# Changelog: Advanced Features Integration

**Date:** September 6, 2026
**Summary:** This session focused on integrating a robust dataset (Itqan) and building advanced analytical, AI, and interactive features on top of the base graph architecture.

## 1. Itqan Dataset Integration (ETL)
Replaced legacy/manual data ingestion with a fully automated ETL pipeline for the open-source `Itqan` repository.
- **Data Extracted**: Over 115,000 unified narrators, 7,200 Hadiths from Sahih Al-Bukhari, 22,000 transmission links, and 106,000 scholar evaluations (Jarh wa Ta'deel).
- **Backend Changes**: Added `ItqanId` and `ItqanGrade` to the `Narrator` EF Core entity. Built `ItqanDatasetParser.cs` to ingest and map the complex JSON schema directly into SQL Server.

## 2. RAG & AI Integration (Phase 1)
Implemented a Retrieval-Augmented Generation (RAG) feature using Microsoft Semantic Kernel.
- **Backend (`AiEvaluationService`)**: Created a service that pulls all classical scholar evaluations (e.g., Al-Dhahabi, Ibn Hajar) from the database and injects them into an LLM prompt. The LLM acts as an expert Hadith scholar, synthesizing the conflicting quotes into a single, cohesive Arabic verdict.
- **Frontend (`NarratorDrawer.tsx`)**: Wired up a UI button to trigger the API endpoint and stream the AI summary to the user.

## 3. Advanced Full-Text Search (Phase 2)
Polished the global search functionality to allow researchers to find Hadiths by their raw Arabic text (`MatnArabic`) and book source.
- **Implementation**: The backend normalizes Arabic search inputs and uses `Contains` to find exact matches across texts and narrator aliases.
- **UI**: A dedicated `/search` page routes users to the interactive tree view for any matched Hadith.

## 4. Graph Analytics & Inqita' Detection (Phase 3)
Added chronological anomaly detection to the Isnad engine to flag mathematically impossible transmission chains.
- **Backend Logic**: Post-processes the recursive CTE output to compare a Student's `BirthYearHijri` against their Sheikh's `DeathYearHijri`. If the student was born after the sheikh died, the node is flagged with `IsAnomaly = true`.
- **Frontend Visualization**: The React Flow engine (`TreeCanvas.tsx`) renders anomalous edges as thick, dashed red lines labeled "انقطاع" (Inqita'). The `NarratorNode` component also displays a `lucide-react` warning icon with the exact reason on hover.

## 5. Interactive UI Filters & Exports (Phase 4)
Gave users more control over the complex graph canvas:
- **Filter**: Added a "Highlight Weak Links" toggle via `GraphControls.tsx`. This dynamically adjusts the opacity of all `reliable` narrators down to 30%, isolating problematic/weak links in the chain for quick visual inspection.
- **Export**: Integrated `html-to-image` to capture the entire React Flow viewport, allowing academics to download high-resolution, perfectly scaled PNGs of the Isnad trees for sharing.

## 6. Book Exploration & ETL Cleanup (Phase 5)
Implemented an end-to-end flow to hierarchically browse the Hadith corpus and finalized the adoption of the Itqan dataset.
- **Backend (`BooksService`)**: Created a dedicated service to dynamically aggregate available Books and Chapters directly from the `HadithText` metadata, ordering chapters sequentially by their starting `HadithNumber`.
- **Frontend Pages**: Added a hierarchical browsing flow (`/books` -> `/books/[bookId]` -> `/books/[bookId]/chapters/[chapterId]`) allowing users to drill down from a Book to a Chapter to a list of Hadiths.
- **Data Cleanup**: Ran a SQL migration to normalize the `FawazAhmed` generated "كتاب 1" placeholders in the database to their authentic Arabic Sahih al-Bukhari names (e.g., "كتاب الإيمان").
- **ETL Optimization**: Purged legacy parsers (`FawazAhmedParser`, `ShamelaAuthorParser`, `JsonHadithParser`) and their raw data files from the repository. Upgraded the `ItqanDatasetParser` with a hardcoded static map of all 97 Bukhari chapters to ensure future ingestion runs natively produce Arabic chapter metadata.

## 7. Full Itqan Corpus Ingestion (All 18 Sunni Hadith Collections)
**Date:** September 7, 2026
Expanded the repository from 4 collections to the complete 18 Sunni Hadith collections in the `Itqan` dataset.
- **Data Ingested**: Ingested 88,839 additional Hadiths and 148,188 transmission links across 1,418 chapters. The total corpus now stands at **112,813 Hadiths** and **225,807 Transmissions**, fully unified with the 115,735 narrators.
- **ETL Parser Upgrade (`ItqanDatasetParser.cs`)**:
  - Automatically loads existing narrator ID mappings directly from the database to prevent duplicate narrator insertions and preserve foreign-key integrity.
  - Detects already imported collections (`صحيح البخاري`, `صحيح مسلم`, `سنن أبي داود`, `جامع الترمذي`) and skips them cleanly.
  - Dynamically discovers all book subdirectories in `data/itqan/sunni/` and uses `index.json` to extract authentic Arabic chapter titles for every chapter without hardcoding.
  - Maps compiler Itqan IDs for all 18 collections and falls back to chain-initiating sheikhs if the compiler is uncatalogued.
- **Collections Active**:
  1. مصنف ابن أبي شيبة (37,943 hadiths)
  2. مسند أحمد (26,539 hadiths)
  3. صحيح مسلم (7,368 hadiths)
  4. صحيح البخاري (7,277 hadiths)
  5. سنن النسائي (5,905 hadiths)
  6. سنن أبي داود (5,276 hadiths)
  7. مشكاة المصابيح (4,447 hadiths)
  8. سنن ابن ماجه (4,321 hadiths)
  9. جامع الترمذي (4,053 hadiths)
  10. سنن الدارمي (2,953 hadiths)
  11. موطأ مالك (1,860 hadiths)
  12. بلوغ المرام (1,767 hadiths)
  13. الأدب المفرد (1,326 hadiths)
  14. رياض الصالحين (1,245 hadiths)
  15. الشمائل المحمدية (411 hadiths)
  16. الأربعون النووية (42 hadiths)
  17. أربعون شاه ولي الله (40 hadiths)
  18. الأربعون القدسية (40 hadiths)

