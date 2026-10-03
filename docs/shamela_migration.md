# Shamela 4 Migration (Replacing Itqan) — Status & Handoff

> Read this first when resuming the migration in a new session.
> Last updated: **2026-10-03** · Branch: **`feature/shamela-rijal`** (not merged into `master`).

## 1. Goal and decisions

The project currently depends on the **Itqan** dataset for (a) hadith texts of the 12 primary books and (b) the whole narrator registry: 115,735 narrators, their Jarh wa Ta'deel and the IDs used to resolve every isnad (`data/itqan/rijal/`, `ItqanDatasetParser`, `ContextualDisambiguator`, `Narrator.ItqanId`/`ItqanGrade`).

**User decision:** stop depending on Itqan and rely **entirely on the local Shamela 4 installation** (`D:\Islamic\shamela4`). The user was shown that a hybrid (keep Itqan for isnad resolution, enrich narrators from Shamela) would be cheaper and recommended it, and chose to **continue the full migration** ("اكمل").

Ground rules agreed so far:
- Nothing in the app or the live database changes until a new database built from Shamela is shown to be at least as good as the current one (Phase 6).
- The v2 backup is the baseline (`backups/SmartHadithTree_v2_2026-10-03.bak`, see `backups/README.md`).
- Work happens on `feature/shamela-rijal`; the user merges to `master` on request.

## 2. Why Shamela, and the hard part

Shamela gives **richer narrator data** than Itqan:

| | Itqan | Shamela (so far) |
|---|---|---|
| Teacher / student lists | ~21% of narrators | 85% / 88% of Tahdhib al-Kamal narrators |
| Jarh wa Ta'deel | one summary verdict per source book | the critics' own words, attributed (26,661 quotes from Tahdhib) |
| Overall grade | coarse (`reliable`, `weak`, ...) | Ibn Hajr's 12 ranks from Taqrib (same T1–T12 scale as `NarratorGradeScale`) |

The hard part is **identity resolution**: Itqan is a ready-made registry with IDs; from Shamela we must build our own registry and then recognise every name in every isnad.

## 3. How Shamela data is read

All data comes from the local Shamela 4 installation, read-only. No network access.

| Location | Contents |
|---|---|
| `database\master.db` (SQLite) | Book catalogue (`book_id`, `book_name`, ...) |
| `database\book\<id%1000>\<id>.db` (SQLite) | Book structure (`page`, `title`). The file exists only for books downloaded in the Shamela app. |
| `database\store\page`, `database\store\title` (Lucene 10.4) | **The Arabic text itself** |

Text is dumped with `scripts/shamela4-extractor/ShamelaLuceneDumper.java` using Shamela's bundled JRE and Lucene jars. Book IDs are passed as the third argument:

```powershell
javac -encoding UTF-8 -d data\shamela_rijal\dumper scripts\shamela4-extractor\ShamelaLuceneDumper.java
& "D:\Islamic\shamela4\app\win\64\jre\2\bin\java.exe" --add-modules=jdk.incubator.vector `
  -cp "D:\Islamic\shamela4\app\lucene\2\*;data\shamela_rijal\dumper" ShamelaLuceneDumper `
  "D:\Islamic\shamela4\database\store" data\shamela_rijal\dump 3722,8609
```

Each book produces `<id>_pages.tsv` and `<id>_titles.tsv`. The page bodies contain literal `\n`, which the parsers turn into real newlines.

## 4. Working data (`data/shamela_rijal/`, git-ignored)

| File | Produced by | Contents |
|---|---|---|
| `dump/<id>_pages.tsv`, `dump/<id>_titles.tsv` | `ShamelaLuceneDumper` | Raw book text |
| `tahdhib.json` | `parse_tahdhib.py` | 8,444 Tahdhib al-Kamal entries + 121 cross-references |
| `taqrib.json` | `parse_taqrib.py` | 8,828 Taqrib entries + 1,419 "X هو Y" redirects |
| `align.json` | `align_taqrib.py` | Tahdhib ↔ Taqrib alignment (92.8% of Tahdhib) |
| `extra_shaykh_books.json` | `parse_shaykh_books.py` | 8,148 narrators from the compilers' shaykh books + compiler entries |
| `current_chains.json`, `current_chains_tabarani.json` | `export_chains.ps1` | The **current system's** chains, for comparison |

`link_tahdhib.py` automatically loads `taqrib.json` and every `extra_*.json` that sits next to the `tahdhib.json` it is given.

**Shamela books dumped so far:**

| ID | Book | Used for |
|---|---|---|
| 3722 | تهذيب الكمال | Registry core |
| 8609 | تقريب التهذيب | Verdicts, ranks, aliases |
| 1692 | ميزان الاعتدال | Itqan-ID check only |
| 14463 | الروض الباسم في تراجم شيوخ الحاكم | al-Hakim's shaykhs |
| 29742 | رجال الحاكم في المستدرك | al-Hakim's narrators |
| 29745 | إرشاد القاصي والداني إلى تراجم شيوخ الطبراني | al-Tabarani's shaykhs |
| 1208 | تحفة الغريب بتراجم رجال معجمي الطبراني | Narrators of the Awsat / Saghir |
| 736, 10906, 96165, 36357 | تاريخ بغداد، السير، الثقات ممن لم يقع في الستة، لسان الميزان | Gap measurement only (not parsed yet) |

## 5. Scripts (`scripts/shamela4-extractor/rijal_pilot/`)

| Script | Role |
|---|---|
| `parse_tahdhib.py` | Splits Tahdhib into entries: book symbols, shuyukh/talamidh, attributed quotes, cross-references. Copes with typo'd or missing entry numbers (longest increasing subsequence + recovery). |
| `parse_taqrib.py` | Ibn Hajr's verdict (full phrase), tabaqa, death, symbols, redirects. |
| `align_taqrib.py` | Unique-name anchors, then an in-order fill between anchors. |
| `build_registry.py` | Merges Tahdhib + Taqrib and maps verdicts to ranks T1–T12. Output is not consumed yet. |
| `parse_shaykh_books.py` | **Generic parser for compilers' shaykh / rijal books**. Configured by `BOOKS` (layouts: `bracket`, `star`, `isnad`) and `COMPILERS`. Merges duplicates across books and adds one compiler entry per compiler, whose shuyukh are that compiler's shaykh-book narrators. |
| `parse_hakim_books.py` | **Superseded** by `parse_shaykh_books.py`; kept until the user agrees to delete it. |
| `link_tahdhib.py` | Name → entry matching (the core). Reports list-linking statistics when run directly. |
| `chain_resolver.py` | **Joint isnad resolution** (Viterbi over candidate sets, scored by mutual teacher/student listing). Exec'd by `compare_current.py`. |
| `gap_test.py` | Isnad segmentation and clean-up, plus a per-depth coverage report. Its helpers are reused by the other scripts. |
| `gap_books.py` | Which rijal books would cover the missing narrators. |
| `isnad_test.py` | Older Bukhari-only link-by-link test. |
| `compare_current.py` | **Main benchmark**: our resolution versus the current DB chains on the same hadiths, plus the agreement rate. |
| `export_chains.ps1` | Exports current chains: `-Books 'المعجم'` (substrings of `Hadiths.BookName`), `-Out <file>`. |

### Name matching rules in `link_tahdhib.py` (learned the hard way)

- **Tokens:** harakat removed, `أإآ→ا`, `ى→ي`, `ة→ه`, `أبي→أبو`, the article dropped, and **`عبد X` kept as one token**.
- **Anchoring:** a name must start with the entry's own ism, or with its own kunya followed by the ism. Otherwise relatives match ("أخو محمد بن سيرين").
- **Nasab order:** the chain must agree in order, so "محمد بن علي" does not match "محمد بن عمر بن علي". Only words linked by `بن` count as nasab; a trailing nisba is not a grandfather.
- **Kunya forms:**
  - "أبو بكر بن إسحاق": kunya, then father or ancestor.
  - "أبو زكريا العنبري": kunya, then nisba.
  - "أبو بكر محمد بن أحمد": kunya, then ism and nasab.
- **Shuhra:**
  - Taqrib redirects are aliases, used only when the plain match finds nobody.
  - "ابن X" is checked first: X must be a father or ancestor.
  - A bare laqab or nisba ("الأعمش", "الزهري") is matched only when it starts with `ال`.
- **Book symbols:** "(خ م)" must be covered by the candidate's own symbols.
- **Fame tie-break** (`fame_pick`): only when one candidate has three times the students of the next.
- **Edition typos tolerated:** "بن بن", "ثقه", numbers without a dash ("٩٣٣ ق:"), missing numbers.

### Isnad segmentation (`gap_test.py`)

The segmentation:
- Strips the previous hadith's verdict and number ("هذا حديث صحيح ... ٣٧٦٣ -"), honorifics (الشيخ، القاضي...), place suffixes (ببغداد...), "إملاء" and footnote marks.
- Resolves "X هو Y" to Y.
- Keeps only the first of two shaykhs joined by "و".
- Drops segments that are not shaped like a name; they are reported separately as noise.

## 6. Results so far (samples, same hadiths as the current DB)

Coverage means the share of narrator names in the first 8 links of each chain that were identified. Agreement means the share of our identified narrators that the current system also identified (same ism + father).

| Book (sample) | Ours: first greedy | Ours: now | Current system | Agreement |
|---|---|---|---|---|
| al-Bukhari (500) | 54% | **73%** | 89% | 77% |
| al-Mustadrak (1,000) | 59% | **70%** | 97% | 59% |
| al-Mu'jam al-Kabir (500) | 70%* | **79%** | 84% | 76% |
| al-Mu'jam al-Awsat (500) | 65%* | **76%** | 90% | 80% |
| al-Mu'jam al-Saghir (500) | 65%* | **78%** | 86% | 78% |

\* Joint resolver already applied, before the Tabarani books were added.

**Precision (manual review of the disagreements):**
- **Bukhari:** of 20 disagreements, ours was right in ~15, there was 1 clear error of ours, and the rest were the same person spelled differently or undecided. The current system has repeated errors: "نافع" → نافع بن همام, "الليث" → "الليثي", "أبو الوليد" (from Shu'ba) → هشام بن عمار, "أبو معمر" → إسماعيل بن إبراهيم.
- **Mustadrak:** most disagreements happen because the current chain is truncated or mixes two isnads.
- **Mu'jam al-Kabir:** 2 errors of ours in 18, both fixed.

**Summary:** the current system covers more; ours resolves less, but what it resolves is usually right. These are small manual samples, not a full precision measurement.

## 7. The plan we are following: phases and tasks

Update the checkboxes whenever a task is finished, and record the commit next to it.
**Next task:** the first unchecked item of Phase 2 (al-Bayhaqi).

### Overview

| Phase | Status |
|---|---|
| 0. Preparation and backups | ✅ Done |
| 1. Pilot (Tahdhib + Taqrib, name linking, isnad test) | ✅ Done |
| 2. Full narrator registry from Shamela | 🔶 In progress |
| 3. Ilal data (mudallisin, mukhtalitun) | ⬜ Not started |
| 4. All hadith texts from Shamela | ⬜ Not started |
| 5. Code changes (Domain, ETL, disambiguation, tests) | ⬜ Not started (largest phase) |
| 6. Build a separate database and compare with v2 | ⬜ Not started |
| 7. Remove Itqan and update docs | ⬜ Not started |

### Phase 0 — Preparation ✅

- [x] Rename the old backup to v1, take a compressed and checksummed v2 backup, document both in `backups/README.md` (`f08a0bc`)
- [x] Track `backups/README.md` in git while keeping `.bak` files ignored (`f08a0bc`)

### Phase 1 — Pilot ✅

- [x] Check whether Itqan's Mizan `entry_id`s match Shamela's numbering: 60% exact, ~90% recoverable with a nearby name match
- [x] `ShamelaLuceneDumper` accepts book IDs as an argument (`03fdae4`)
- [x] Parse Tahdhib al-Kamal: entries, symbols, shuyukh/talamidh, attributed quotes, cross-references (`03fdae4`)
- [x] Link list names to entries and verify precision by manual samples (`03fdae4`)
- [x] Isnad test on 500 Bukhari hadiths (`03fdae4`)
- [x] Parse Taqrib, align it with Tahdhib, map verdicts to Ibn Hajr's 12 ranks (`46b4035`)
- [x] Write the handoff document and point `AGENTS.md` to it (`c8e95fa`)

### Phase 2 — Full narrator registry 🔶

Measurement tools:
- [x] Gap measurement per isnad depth, and which books cover the gap (`1f51a2b`)
- [x] Side-by-side comparison with the current system, with an agreement rate (`286c48d`, `3ea458f`)

Resolution:
- [x] Shuhra index: Taqrib aliases, bare laqab/nisba, "ابن X", fame tie-break (`845e5f2`)
- [x] Stricter matching: compound "عبد X", nasab order, kunya forms, edition typos (`1a2ec3d`, `3ea458f`, `5cd9709`)
- [x] Joint isnad resolution, `chain_resolver.py` (`3ea458f`)
- [x] Isnad clean-up: previous hadith's verdict, honorifics, place suffixes, "X هو Y", "وهب" not treated as a conjunction (`1a2ec3d`, `3ea458f`)

Compilers' shaykh books (via `parse_shaykh_books.py`):
- [x] al-Hakim: الروض الباسم (14463), رجال الحاكم في المستدرك (29742) (`1a2ec3d`, `5cd9709`)
- [x] al-Tabarani: إرشاد القاصي والداني (29745), تحفة الغريب (1208) (`5cd9709`)
- [ ] **al-Bayhaqi:** إتحاف المرتقي (123666), السلسبيل النقي (123667). Measure `sunan_kubra_bayhaqi` and `shuab_iman_bayhaqi` before and after with `compare_current.py`
- [ ] Ibn Hibban: ري الظمآن بتراجم شيوخ ابن حبان (1498)
- [ ] al-Daraqutni: الدليل المغني لشيوخ الدارقطني (7852)
- [ ] Ibn Khuzaymah and Abu Awanah: search `master.db` for a dedicated book
- [ ] Ahmad and Malik: تعجيل المنفعة (1893)
- [ ] Early compilers (Abd al-Razzaq, al-Tayalisi, al-Shafi'i, al-Humaydi, Sa'id b. Mansur, Ishaq, al-Bazzar, Abu Ya'la): measure first, since they are mostly covered by Tahdhib
- [ ] Measure every remaining book of the 31 against the current system at least once

General rijal books for what remains (each needs its own parser):
- [ ] تاريخ بغداد (736)
- [ ] سير أعلام النبلاء (10906)
- [ ] لسان الميزان (36357)
- [ ] الثقات ممن لم يقع في الكتب الستة (96165)

Remaining resolver gaps:
- [ ] Common single names ("عطاء", "هشام", "عبدان", "سفيان") when context is weak
- [ ] "عن أبيه" after an unresolved narrator
- [ ] Port the contextual overrides of `ContextualDisambiguator` (e.g. Sufyan / Hammad by student) to the new IDs
- [ ] Measure precision on a larger, systematic sample (not only small manual reviews)

Housekeeping:
- [ ] Delete `parse_hakim_books.py`, which is superseded (needs the user's approval)
- [ ] Turn the pilot scripts into one reproducible pipeline that writes the final registry (IDs, names, verdicts, ranks, lists, quotes, sources)

### Phase 3 — Ilal data ⬜

- [ ] طبقات المدلسين لابن حجر (1186), replacing the 16 hand-written mudallisin
- [ ] الكواكب النيرات (309) and المختلطين للعلائي (25846), replacing the 7 hand-written mukhtalitun, with heard-before / heard-after students
- [ ] Link both to registry IDs

### Phase 4 — Hadith texts ⬜

- [ ] Choose a Shamela edition for each of the 12 primary books. All are downloaded except الشمائل المحمدية, which the user must download in Shamela.
- [ ] Extract them with footnotes (`foot`) for takhrij and editors' grades
- [ ] Separate the compiler's own remarks from the matn, and keep volume and page references
- [ ] Move all 31 books to `data/shamela/` in one format

### Phase 5 — Code ⬜

- [ ] Domain: `ItqanId` / `ItqanGrade` → a source reference and `Grade`; `ScholarEvaluation` with critic and book; EF migration
- [ ] ETL: a new parser in place of `ItqanDatasetParser`, and a name index built from the registry instead of `by_name.json`
- [ ] Chain building: the joint resolver ported into `ChainReprocessingService` / `ContextualDisambiguator`
- [ ] `NarratorGradeScale` from Ibn Hajr's ranks; `IlalSeedService` from the Phase 3 data
- [ ] Update the unit tests (`ContextualDisambiguatorTests`, `IlalAnalysisServiceDbTests`, ...)

### Phase 6 — Build and compare ⬜

- [ ] Build `SmartHadithTree_Shamela` separately, without touching `SmartHadithTree`
- [ ] Compare with v2: narrators identified per book, agreement, Ilal findings on known hadiths, manual review of famous isnads
- [ ] Switch only if the new database is as good or better (user decision); take a v3 backup first

### Phase 7 — Clean-up ⬜

- [ ] Remove `data/itqan/rijal`, the Itqan parser and the Itqan-only code
- [ ] Update `AGENTS.md`, `docs/data_ingestion.md` and `README.md`; merge `feature/shamela-rijal`

## 8. How to re-run everything

From the repo root, in Git Bash (the dump step in §3 comes first):

```bash
cd data/shamela_rijal
python ../../scripts/shamela4-extractor/rijal_pilot/parse_tahdhib.py dump tahdhib.json
python ../../scripts/shamela4-extractor/rijal_pilot/parse_taqrib.py dump taqrib.json
python ../../scripts/shamela4-extractor/rijal_pilot/align_taqrib.py tahdhib.json taqrib.json align.json
python ../../scripts/shamela4-extractor/rijal_pilot/parse_shaykh_books.py dump tahdhib.json extra_shaykh_books.json
python ../../scripts/shamela4-extractor/rijal_pilot/link_tahdhib.py tahdhib.json
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/bukhari current_chains.json "محمد بن إسماعيل بن إبراهيم بن المغيرة" 500 12
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/mustadrak_hakim current_chains.json "محمد بن عبد الله بن محمد بن حمدويه الحاكم" 1000 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/mujam_kabir_tabarani current_chains_tabarani.json "سليمان بن أحمد بن أيوب" 500 0
```

`compare_current.py` options (environment variables):
- `CHAIN_MODE=greedy` — use the old link-by-link walk instead of the joint resolver.
- `SHOW_DIFF=20` — list disagreements with the current system for review.

Python output with Arabic needs `PYTHONIOENCODING=utf-8` on Windows.

The sample input texts come from `data/itqan/sunni/<book>/`. The 19 non-Itqan books there were already extracted from Shamela; the 12 primary books will be re-sourced from Shamela in Phase 4.

## 9. Pitfalls (for whoever continues)

- **Shell escaping:** in the Bash tool, `\\n`, `\b` and `\s` inside heredocs or `python -c` get mangled. Write scripts with the file-writing tool, not heredocs.
- **Arabic from SQL Server:** `sqlcmd` output loses Arabic. Use `export_chains.ps1` (System.Data.SqlClient → UTF-8 JSON). `pyodbc` is not installed.
- **SQL Server file access:** restores must target the instance data folder; the service cannot write to user temp folders.
- **Measure the right thing:**
  - A higher "resolved" rate can hide wrong links. Always check agreement and review disagreements manually.
  - Numbers reported mid-way were corrected several times. For example, a 70.4% Bukhari figure turned out to include wrong shuhra matches.
- **Itqan's coverage of later narrators is broad:** it includes al-Hakim's and al-Tabarani's shaykhs. Do not assume that gap is unique to Shamela.
