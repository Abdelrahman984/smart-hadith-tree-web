# Changelog

## 2026-10-05: Shamela data and review fixes (merged into `master`)

- **Data source**: all 31 books and the narrator registry are built from Shamela 4 (Tahdhib al-Kamal, Taqrib and the compilers' shaykh books); Itqan is no longer read. Database `SmartHadithTree_ShamelaV5`: 274,597 hadiths, 1,217,978 transmissions, 23,502 narrators, 116,670 quotes, 115,816 relations. The migration is documented in `docs/shamela_migration.md`.
- **Chains**: multi-chain isnads (tahwil) are kept as several chains; first-person «أبي» and «جده» are resolved; the compilers' own teacher lists were added.
- **Grading**: the overall grade is computed from each route's weakest narrator above the compiler.
- **Ilal and the comparative page**: fewer false findings (the compiler's link, matn framing words, a medoid text as the base, the order of the requested hadiths); geographic mismatch is a travel note, not a break; low-confidence findings fold into a group; fixes to the header badges and the refit on resize.
- **Challenge work merged** (`claude/happy-johnson-5vqndl`): evidence-status badges, disagreement between critics, a glossary, an AI notice, accessibility fixes, Docker packaging and the challenge documents.
- **Docs**: `docs/backlog.md` (open work and how to review a page), `docs/challenge/sources-and-licenses.md`.
- The previous master is kept as the tag `legacy-master-itqan` and the branch `legacy/itqan-master`.

---

## 2026-09-06: Advanced features integration (Itqan-based, superseded)

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

---

# Changelog: Ilal Engine (علل الحديث)

**Date:** October 2, 2026
**Summary:** Added a rule-based engine that detects hidden defects (علل) across the turuq of a hadith, with an optional AI explanation and canvas overlays. The design is described in `docs/ilal.md`.

## Backend
- **Domain**
  - `Narrator` gained `MudallisTier` and `IkhtilatNote`.
  - New entities `NarratorRelation` (teacher/student graph) and `MukhtalitHearing` (before/after ikhtilat).
  - New enums `IllahType` and `IllahSeverity`.
  - New utilities `MatnText` and `MatnAligner` (word-level LCS diff).
- **Migration `AddIlalEngine`.** Adds the new tables and columns. It also adds the drift columns that earlier commits put in the model without a migration (`ItqanId`, `ItqanGrade`, the Gawami columns, `HadithClusters`). Those statements use `IF NOT EXISTS`, so the migration also works on databases that were patched by hand.
- **Application.** Six rules in `Services/Ilal/Rules`: `TadlisRule`, `IkhtilatRule`, `HiddenInqitaRule`, `MatnAtMadarRule`, `RafWaqfRule` and `WaslIrsalRule`.
  - `IlalAnalysisService` runs them.
  - `IlalExplanationService` writes the Gemini explanation.
  - `NarratorGradeScale` is now the grade-to-tier mapping shared with `TaqwiyahService`.
  - `TaqwiyahService` downgrades the grade to "ضعيف (معلول)" when every tariq has a decisive defect.
- **API**
  - New endpoints `GET /api/ilal?ids=`, `GET /api/ilal/{hadithId}` and `POST /api/ilal/explain`.
  - `GET /api/takhreej` now includes `ilalReport`.
  - Isnad nodes now carry `isMudallis` and `hasMukhtalit`.
- **ETL.** The new `seed-ilal` mode imports Itqan teacher/student relations and applies the curated `Seeds/mudallisin.json` and `Seeds/mukhtalitun.json`.
- **Fix.** Added a stub `GawamiImporterService`, so the API builds again after the Gawami commit.
- **Fix: compiler IDs.** `ItqanDatasetParser` and `ChainReprocessingService` disagreed on the Itqan IDs of the compilers, and several were wrong in both (for example, 57802 is a Companion, not al-Nasa'i). Both now use one table in `ItqanDatasetParser.BookMetadata`, checked against the Itqan rijal profiles: Bukhari 336, Muslim 618, Abu Dawud 74, al-Tirmidhi 297, al-Nasa'i 134, Ibn Majah 514, Ahmad 353, Malik 664, al-Darimi 168, Ibn Abi Shaybah 748. Re-run `reprocess-chains` to rebuild the chains with the correct compilers.

## Frontend
- New `features/ilal` module with:
  - `IlalPanel`: findings grouped by type, with severity colors.
  - `MatnDiffView`: aligned matn comparison.
  - `IlalAiExplanation` and `IlalLauncher`.
  - A Zustand store that shares the report and the selected finding with the canvas.
- **Takhreej page.** The sidebar now has an "العلل" tab. Selecting a finding highlights its narrators on the tree.
- **Tree page.** A "فحص العلل" button gathers the hadith's turuq and analyzes them.
- **Canvas**
  - The مدلس / اختلط badges are now populated.
  - Tadlis, unproven-meeting and ikhtilat links get distinct edge styles, and the legend lists them.

## Tests
- Added 33 test cases for the rules, the aligner, the Taqwiyah integration, DB loading and `IlalController`. All 44 tests pass.


---

# Changelog: 31-Book Corpus Expansion & Shamela 4 Local Lucene Ingestion

**Date:** October 2, 2026
**Summary:** Expanded the Smart Hadith Tree corpus from 12 Sunni collections to **31 complete canonical Sunni collections** (`233,224` Hadiths and `1,078,668` Isnad Transmissions) by building a high-speed local Shamela 4 Lucene 10.4.0 + SQLite extractor, enhancing `ItqanDatasetParser` and `ContextualDisambiguator`, creating a full verified SQL Server backup, and redesigning the Frontend Home page and book theme registry.

## 1. Shamela 4 Local Lucene + SQLite Extraction Pipeline (`scripts/shamela4-extractor/`)
- **Architectural Discovery**: Determined that Shamela 4 separates structural metadata (`database/book/<id%1000>/<id>.db` containing `page` and `title` tables) from the Arabic text (`database/store/page` and `database/store/title` stored in Apache Lucene 10.4.0 indices).
- **`ShamelaLuceneDumper.java`**: Built a reflection-based Java bulk extractor that invokes `ws.shamela.LuceneBulk.queryRows` on Shamela 4's bundled OpenJDK 21 runtime, dumping all `183,659` pages and `32,380` chapter titles across the 19 missing books in **34.6 seconds**.
- **`build_itqan_books.py`**: Joined the SQLite `page.number` and `title` hierarchy with the Lucene text dumps, stripped HTML markup and leading numbers, concatenated multi-page continuations, and generated standardized `index.json` + numbered chapter `.json` files inside `data/itqan/sunni/<slug>/` for all 19 books (`128,661` complete Hadiths).

## 2. ETL & Contextual Disambiguation Upgrades (`src/SmartHadithTree.Etl/`)
- **`ItqanDatasetParser.BookMetadata`**: Expanded to map all **31 canonical Sunni collections** to their authentic Arabic book titles, compiler names, and verified Itqan Rijal profile IDs (`Abd al-Razzaq: 44`, `al-Tayalisi: 171`, `al-Shafi'i: 2734`, `al-Humaydi: 82`, `Sa'id ibn Mansur: 1959`, `Ishaq ibn Rahawayh: 695`, `al-Bazzar: 196`, `Abu Ya'la: 462`, `Ibn Khuzaymah: 278`, `Abu Awanah: 1123`, `Ibn Hibban: 706`, `al-Tabarani: 202`, `al-Daraqutni: 460`, `al-Hakim: 10`, `al-Bayhaqi: 34`).
- **`ContextualDisambiguator.cs`**: Added regional and era-specific contextual overrides for ambiguous narrator names (e.g., resolving `سفيان` to `سفيان الثوري (434)` when narrated by `عبد الرزاق (44)` or `وكيع (112)`, vs. `سفيان بن عيينة (192)` when narrated by `الشافعي (2734)` or `الحميدي (82)`; resolving `حماد` to `حماد بن سلمة (138)` for `الطيالسي (171)` and `عفان (279)` vs. `حماد بن زيد (128)` for `سليمان بن حرب (161)`).
- **Bulk Ingestion**: Ingested **128,661 new Hadiths** and **655,980 new Isnad Transmissions** (`784,641` total records) in `146.3s`. Cleaned up temporary test records so the database holds exactly **31 collections**, **233,224 Hadiths**, and **1,078,668 Transmissions**.
- **Unit Tests (`src/SmartHadithTree.Tests/Etl/`)**: Added unit tests in `ContextualDisambiguatorTests.cs` and `ShamelaSqliteParserTests.cs` (all passing).

## 3. Database Backup (`backups/`)
- Created and verified (`RESTORE VERIFYONLY`) a full SQL Server backup at `backups/SmartHadithTree_31Books_Full.bak` (`1,390.14 MB`), since renamed to `SmartHadithTree_v1_2026-10-02.bak` (see `backups/README.md`).
- Added `backups/` and `*.bak` to `.gitignore`.

## 4. Frontend Home Page & Book Theme Registry (`frontend/`)
- **`frontend/src/lib/bookTheme.ts`**: Registered all 31 canonical collections with their traditional Hadith scholarly abbreviations (`خ`, `م`, `عب`, `ش`, `طي`, `شاف`, `حميد`, `سع`, `راه`, `بز`, `كب`, `يع`, `خز`, `عو`, `حب`, `طب`, `طس`, `طص`, `قط`, `كم`, `هق`, `شعب`, etc.) and distinct color badges.
- **`frontend/src/app/page.tsx`**: Upgraded the Home page with a direct search bar, quick search examples, live corpus statistics (`31` books, `233,224` hadiths, `1,078,668` transmissions, `115,735` narrators), 6 core feature cards, and a categorized 31-book library showcase.


---

# Changelog: Migration from Itqan to Shamela 4 (Phases 0-7)

**Date:** October 5, 2026
**Summary:** The hadith texts, the narrator registry, the isnad chains and the Ilal data now all come from a local Shamela 4 installation. The app runs on `SmartHadithTree_Shamela` (274,597 hadiths, 1,125,644 transmissions, 23,502 narrators, 116,670 critics' quotes). Details, measurements and pitfalls: `docs/shamela_migration.md`; the comparison with the old database: `docs/shamela_phase6_comparison.md`.

- **Registry and resolver (Python, `scripts/shamela4-extractor/rijal_pilot/`)**: narrators from Tahdhib al-Kamal, Taqrib and the compilers' shaykh books; names matched and isnads resolved jointly; 97.0% precision on a 500-name review.
- **Texts**: 31 books rebuilt from Shamela with the edition's own numbering, records cut at in-text hadith numbers (this fixes the shifted records of the old data), isnad / matn parts, footnotes.
- **Ilal**: mudallisin from Ibn Hajar's tiers (158) and mukhtalitun with the books' severity and hearings (131); `IkhtilatRule` weighs severity; grades come from Ibn Hajar's ranks.
- **Phase 6**: on 4,650 paired hadiths the Ilal engine finds tadlis in 793 (275 before) and ikhtilat in 341 (0 before); 74% position agreement on 220,603 paired chains, with the Shamela side right in 9 of 20 sampled disagreements.
- **Phase 7**: removed `ItqanDatasetParser`, `ContextualDisambiguator`, `ShamelaSqliteParser`, `IlalSeedService`, `ChainReprocessingService`, the old seeds, their tests and `data/itqan/rijal`. The Api and Etl connection strings point at `SmartHadithTree_Shamela`. Backups: v3 (the old database, before the switch) and v4 (the Shamela database).
