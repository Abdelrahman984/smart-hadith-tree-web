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

## 7. Plan and status

| Phase | Status |
|---|---|
| 0. Preparation, versioned backups (`backups/README.md`) | ✅ Done |
| 1. Pilot: parse Tahdhib + Taqrib, link names, isnad test | ✅ Done |
| 2. Full narrator registry from Shamela | 🔶 **In progress**: six-books narrators, al-Hakim, al-Tabarani done |
| 3. Ilal data: Ibn Hajr's طبقات المدلسين (1186), الكواكب النيرات (309), المختلطين للعلائي (25846) | ⬜ Not started |
| 4. All hadith texts from Shamela (12 primary books + footnotes / editor grades) | ⬜ Not started |
| 5. Code: Domain (`ItqanId`/`ItqanGrade` → source refs / `Grade`; `ScholarEvaluation` critic + book), ETL parser, `ContextualDisambiguator` on the new IDs, `NarratorGradeScale`, tests | ⬜ Not started (largest phase) |
| 6. Build a separate DB (`SmartHadithTree_Shamela`) and compare with v2; switch only if it is as good or better | ⬜ Not started |
| 7. Remove Itqan (`data/itqan/rijal`, parser), update docs and `AGENTS.md` | ⬜ Not started |

### Phase 2: next steps in order

1. **al-Bayhaqi:** إتحاف المرتقي بتراجم شيوخ البيهقي (123666) and السلسبيل النقي في تراجم شيوخ البيهقي (123667). Add them to `BOOKS` / `COMPILERS` in `parse_shaykh_books.py`, then measure `sunan_kubra_bayhaqi` and `shuab_iman_bayhaqi` before and after with `compare_current.py`.
2. **Ibn Hibban:** ري الظمآن (1498).
3. **al-Daraqutni:** الدليل المغني (7852).
4. **The other compilers:**
   - Ibn Khuzaymah and Abu Awanah: no dedicated book found yet. Check `master.db`.
   - Abd al-Razzaq, al-Tayalisi, al-Shafi'i, al-Humaydi, Sa'id b. Mansur, Ishaq, al-Bazzar, Abu Ya'la: mostly early, so largely covered by Tahdhib. Measure first.
   - تعجيل المنفعة (1893) for Ahmad and Malik.
5. **General books for the remaining gap:** تاريخ بغداد (736), السير (10906), لسان الميزان (36357), الثقات ممن لم يقع في الستة (96165). These need parsers (layouts differ).
6. **Remaining resolver gaps:**
   - Common single names ("عطاء", "هشام", "عبدان", "سفيان") when context is weak.
   - "عن أبيه" after an unresolved narrator.
   - Port the contextual overrides of `ContextualDisambiguator` (e.g. Sufyan / Hammad by student) to the new IDs.

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
