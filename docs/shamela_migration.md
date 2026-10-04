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
| `lisan.json` | `parse_lisan.py` | 9,170 Lisan al-Mizan entries + 1,646 cross-references (not merged, see §7) |
| `tahdhib.json` | `parse_tahdhib.py` | 8,471 Tahdhib al-Kamal entries + 120 cross-references |
| `taqrib.json` | `parse_taqrib.py` | 8,828 Taqrib entries + 1,419 "X هو Y" redirects |
| `align.json` | `align_taqrib.py` | Tahdhib ↔ Taqrib alignment (92.8% of Tahdhib) |
| `extra_shaykh_books.json` | `parse_shaykh_books.py` | 15,052 narrators from the compilers' shaykh books, تعجيل المنفعة and تاريخ بغداد, after merging 899 cross-book duplicates (plus Ithaf's name forms, ري الظمآن's kunyas and the compiler entries) |
| `current_chains.json`, `current_chains_tabarani.json`, `current_chains_bayhaqi.json`, `current_chains_hibban.json`, `current_chains_daraqutni.json`, `current_chains_khuzaymah_awanah.json`, `current_chains_ahmad_malik.json`, `current_chains_early.json` (the 8 early compilers), `current_chains_<slug>.json` for each of the last 10 books (`muslim`, `abudawud`, ..., `sunan_kubra_nasai`) | `export_chains.ps1` | The **current system's** chains, for comparison |

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
| 123667 | السلسبيل النقي في تراجم شيوخ البيهقي | al-Bayhaqi's shaykhs |
| 123666 | إتحاف المرتقي بتراجم شيوخ البيهقي | al-Bayhaqi's shaykhs, plus every form of each name as it appears in his books |
| 1498 | ري الظمآن بتراجم شيوخ ابن حبان | Ibn Hibban's 489 shaykhs, plus a table of the kunyas he uses for them |
| 7852 | الدليل المغني لشيوخ الإمام أبي الحسن الدارقطني | al-Daraqutni's 543 shaykhs, with the author's verdicts |
| 151171 | المسالك القويمة بتراجم رجال ابن خزيمة | Three parts: Ibn Khuzaymah's 370 shaykhs, his 204 students, and 198 of his narrators who are not in Tahdhib |
| 1893 | تعجيل المنفعة بزوائد رجال الأئمة الأربعة | Narrators of Ahmad, Malik, al-Shafi'i and Abu Hanifa who are not in Tahdhib (1,113 kept) |
| 241 | فوائد المستخرجات من خلال مسند أبي عوانة | Checked only: a study of the Mustakhraj's benefits, no biographies. Abu Awanah has no dedicated rijal book in Shamela. |
| 736 | تاريخ بغداد | 7,800 biographies: ~4,900 later narrators added, ~1,100 merged into narrators already in the shaykh books (`khatib` layout) |
| 10906, 96165 | السير، الثقات ممن لم يقع في الستة | Gap measurement only (not parsed; see §7) |
| 36357 | لسان الميزان | Parsed (`parse_lisan.py`), not merged into the resolver (§7) |

## 5. Scripts (`scripts/shamela4-extractor/rijal_pilot/`)

| Script | Role |
|---|---|
| `parse_tahdhib.py` | Splits Tahdhib into entries: book symbols, shuyukh/talamidh, attributed quotes, cross-references. Copes with typo'd, repeated or missing entry numbers (longest increasing subsequence + recovery; a repeated "٣٤٩١" had dropped Abu Musa al-Ash'ari, Ibn Mas'ud and three other Companions) and heads without a dash ("١٠٢٥: الحارث الأعور"). Lists: women's «روت عن» / «روى عنها»; a list goes on over a line break unless the line ends with a full stop or the next line is a remark; items are also split at a Latin comma or after a symbols bracket. |
| `parse_taqrib.py` | Ibn Hajr's verdict (full phrase), tabaqa, death, symbols, redirects. |
| `align_taqrib.py` | Unique-name anchors, then an in-order fill between anchors. |
| `build_registry.py` | Ibn Hajar's ranks T1–T12 from a Taqrib verdict (`rank()`, imported by `pipeline.py`); run alone it merges Tahdhib + Taqrib only. |
| `pipeline.py` | **One command for the whole registry** (first draft by a sub-agent, reviewed): runs the five parsers, links the lists, writes `registry.json` (stable ids `tk<num>` / `<source>:<num>`, name, header, sources, verdict, rank, tabaqa, death, kunyas, aliases, strictly resolved shuyukh/talamidh ids plus the raw lists, quotes) and `registry_stats.json`. `--from N` restarts at step N. |
| `parse_shaykh_books.py` | **Generic parser for compilers' shaykh / rijal books**. Configured by `BOOKS` (layouts: `bracket`, `paren`, `star`, `isnad`, `dash`, `runs`, `tajil`, `khatib`) and `COMPILERS`. Merges duplicates across books (see §6, تاريخ بغداد; `SHOW_MERGES=1` prints a sample of merged groups for review) and adds one compiler entry per compiler, whose shuyukh are that compiler's shaykh-book narrators. |
| `link_tahdhib.py` | Name → entry matching (the core). Reports list-linking statistics when run directly. |
| `chain_resolver.py` | **Joint isnad resolution** (Viterbi over candidate sets, scored by mutual teacher/student listing). Exec'd by `compare_current.py`. |
| `gap_test.py` | Isnad segmentation and clean-up, plus a per-depth coverage report. Its helpers are reused by the other scripts. |
| `gap_books.py` | Which rijal books would cover the missing narrators. |
| `check_record_boundaries.py` | Classifies every record in `data/itqan/sunni` as clean, prefix tail, or **shifted** (the isnad belongs to the next hadith). See §6.1. |
| `compare_current.py` | **Main benchmark**: our resolution versus the current DB chains on the same hadiths, plus the agreement rate. |
| `bench.py` | **Runs `compare_current.py` on 4 or all 31 books** (in parallel), keeps each run in `data/shamela_rijal/results/<tag>/`, and prints one line per book with the change since the previous run, the names gained and lost, and a sample of new disagreements to review. |
| `export_chains.ps1` | Exports current chains: `-Books 'المعجم'` (substrings of `Hadiths.BookName`; use `'بيهقي'`, not `'البيهقي'`, since the DB name is «للبيهقي»), `-Out <file>`. |

### Name matching rules in `link_tahdhib.py` (learned the hard way)

- **Tokens:** harakat removed, `أإآ→ا`, `ى→ي`, `ة→ه`, `أبي→أبو` (not before «بن»: «أبي بن كعب» is Ubayy), `زكرياء→زكريا`, the article dropped, and **`عبد X` and `عبيد الله` kept as one token**. As two words, "عبيد الله" cut al-Zuhri's nasab short, so "ابن شهاب" went to عاصم بن كليب بن شهاب.
- **Anchoring:** a name must start with the entry's own ism, or with its own kunya followed by the ism. Otherwise relatives match ("أخو محمد بن سيرين").
  - Kunya-only entries ("أبو زيد. عن: أبي هريرة") must match on their own kunya. Before this, any word in their header counted, which gave "أبي هريرة" 13 candidates.
  - "المعروف بأبي الزناد" counts as a kunya (عبد الله بن ذكوان), and so does a kunya right after the ism («ذكوان أبو صالح السمان»), but not in "ابن أبي X".
- **Nasab order:** the chain must agree in order, so "محمد بن علي" does not match "محمد بن عمر بن علي". Only words linked by `بن` count as nasab; a trailing nisba is not a grandfather.
  - The aligned Taqrib entry's nasab is accepted as a second nasab (1,277 entries). This corrects typos in Shamela's Tahdhib text, e.g. "عبيد الله بن عتبة" printed for عبيد الله بن عبد الله بن عتبة.
  - The words of the aligned Taqrib name also count as the narrator's own words (laqab, nisbas). Tahdhib may give them only in a later sentence: «الحميدي» comes after "وقيل:".
- **Kunya forms:**
  - "أبو بكر بن إسحاق": kunya, then father or ancestor. "أبو سلمة بن عبد الرحمن" is matched on the father (or the kunya-headed entry's own words), not on narrators whose ism is عبد الرحمن.
  - "أبو زكريا العنبري": kunya, then nisba.
  - "أبو بكر محمد بن أحمد": kunya, then ism and nasab.
- **Shuhra:**
  - Taqrib redirects are aliases, used only when the plain match finds nobody.
  - "ابن X" is checked first: X must be a father or ancestor. When a son of X is at least as cited as the grandsons, only the sons count («ابن عمر» is not Salim b. Abdullah b. Umar); «ابن جريج» stays the grandson.
  - A bare laqab or nisba ("الأعمش", "الزهري") is matched when it starts with `ال`.
  - A laqab without `ال` ("بندار") is matched only when it is nobody's ism, and only when it is the last word of the entry's kunya phrase ("أبو بكر البصري بندار") or follows "الملقب". Matching any header word picked up verbs ("خرج", "بعث").
- **Names by grandfather or "ابن X"** (only when nobody has the given father): the words after the ism come in order in the narrator's own name part, and a word from the nasab is at most the grandfather ("عبد الله بن أحمد بن حنبل", "علي بن المديني", "عثمان بن أبي شيبة"). Entries headed by a kunya («أم عثمان») are excluded.
- **Unnamed narrators** («رجل», «امرأة», «شيخ») match nobody.
- **Book symbols:** "(خ م)" must be covered by the candidate's own symbols.
- **Aliases from shaykh books:**
  - Exact: a form of 3+ words ("أبو القاسم الفقيه") matches only that narrator, before any other rule. A 2-word form is loose: as exact, Ithaf's "عبد الله بن يوسف" (ابن بامويه) replaced al-Tinnisi in Bukhari's and al-Bazzar's isnads.
  - Loose: a bare kunya ("أبو إسحاق", "أبو خليفة") only adds a candidate; the chain context decides. As exact aliases they turned "أبي إسحاق" from Shu'ba into al-Bayhaqi's shaykh يحيى بن إبراهيم.
- **Fame tie-break** (`fame_pick`): only when one candidate has three times the students of the next.
  - In the joint resolver it applies only when no candidate has any edge **and** the isnad gives kunya + ism + father ("أبو العباس محمد بن يعقوب" → al-Asamm, not al-Ahwazi).
  - On shorter forms it was wrong too often: "ابن أبي مليكة" → يعقوب بن زيد, "عبدان" → al-Marwazi, "ابن صاعد" → خلف بن خليفة.
  - It also breaks a **tie between candidates equally linked to their neighbours** (`chain_fame`): Abu Nadra lists both Abu Sa'id al-Khudri (126 students) and Samura b. Jundub (26), both «أبو سعيد». Not for the compiler's own shaykh given by kunya: al-Hakim's «أبو بكر بن إسحاق» is Ahmad b. Ishaq, not Ibn Khuzaymah.
- **Edition typos tolerated:** "بن بن", "ثقه", numbers without a dash ("٩٣٣ ق:"), missing numbers.

### Isnad segmentation (`gap_test.py`)

The segmentation:
- Strips the previous hadith's verdict and number ("هذا حديث صحيح ... ٣٧٦٣ -"), honorifics (الشيخ، القاضي...), place suffixes (ببغداد...), "إملاء" and footnote marks.
- Resolves "X هو Y" to Y.
- Keeps only the first of two shaykhs joined by "و".
- Drops segments that are not shaped like a name; they are reported separately as noise.

## 6. Results so far (samples, same hadiths as the current DB)

Coverage means the share of narrator names in the first 8 links of each chain that were identified. Agreement means the share of our identified narrators that the current system also identified (same ism + father; a Companion known by his kunya also agrees when his Tahdhib header gives the current system's name, «أبو هريرة» = «عبد الرحمن بن صخر»; the Taqrib form of a typo'd Tahdhib name counts too, «عبيد الله بن عتبة» = «عبيد الله بن عبد الله بن عتبة»; and a current-system narrator whose Itqan id is mapped to our entry agrees whatever name it carries, since Itqan names some narrators wrongly but means the right person: «نافع بن همام» is Nafi' mawla Ibn Umar, «عكرمة بن منصور» Ikrima mawla Ibn Abbas, «محمد بن أبي عمرة» Ibn Sirin — 77 ids in `review/itqan_id_map.json` so far). A name past the end of the current chain ("… ← عروة" with no «عائشة») has nothing to compare with: it is counted in the last column, not as a disagreement. Before the clean-up step below, those names counted as disagreements, so older agreement figures in commit messages are lower.

| Book (sample) | Ours: first greedy | Ours: now | Current system | Agreement | Ours past the current chain |
|---|---|---|---|---|---|
| al-Bukhari (500) | 54% | **86%** | 81% | 84% | 229 |
| al-Mustadrak (1,000) | 59% | **81%** | 92% | 61% † | 970 |
| al-Mu'jam al-Kabir (500) | 70%* | **87%** | 81% | 89% | 391 |
| al-Mu'jam al-Awsat (500) | 65%* | **83%** | 87% | 89% | 122 |
| al-Mu'jam al-Saghir (500) | 65%* | **86%** | 84% | 86% | 122 |
| al-Sunan al-Kubra, al-Bayhaqi (500) | 60%* | **82%** | ✱ † | 45% † | 550 |
| Shu'ab al-Iman (500) | 60%* | **73%** | 95% † | 80% † | 271 |
| Sahih Ibn Hibban (500) | 74%* | **89%** | 85% | 90% | 189 |
| Musnad Ahmad (500) | 75%* | **86%** | 85% | 87% | 129 |
| al-Muwatta (500) | 53%* | **70%** ‡ | 78% ‡ | 87% | 139 |
| Sunan al-Daraqutni (500) | 68%* | **84%** | 87% | 83% | 238 |
| Sahih Ibn Khuzaymah (500) | 68%* | **85%** | 80% | 86% | 322 |
| Mustakhraj Abi Awanah (500) | 67%* | **82%** | 68% | 85% | 532 |
| Musannaf Abd al-Razzaq (500) | — | **77%** | 76% | 91% | 120 |
| Musnad al-Tayalisi (500) | — | **85%** | ✱ | 80% | 22 |
| Musnad al-Shafi'i (500) | — | **85%** | 80% | 92% | 149 |
| Musnad al-Humaydi (500) | 44% | **83%** | 88% | 91% | 79 |
| Sunan Sa'id b. Mansur (500) | — | **78%** | ✱ | 82% | 43 |
| Musnad Ishaq (500) | — | **83%** | 82% | 88% | 216 |
| Musnad al-Bazzar (500) | — | **86%** | 94% | 81% | 75 |
| Musnad Abi Ya'la (500) | — | **84%** | 87% | 88% | 126 |
| Sahih Muslim (500) | — | **87%** | 69% | 87% § | 504 |
| Sunan Abi Dawud (500) | — | **84%** | 80% | 89% | 225 |
| Jami' al-Tirmidhi (500) | — | **90%** | 90% | 91% | 128 |
| Sunan al-Nasa'i (500) | — | **88%** | 82% | 88% | 219 |
| Sunan Ibn Majah (500) | — | **88%** | 83% | 92% | 236 |
| Sunan al-Darimi (500) | — | **85%** | 86% | 89% | 122 |
| al-Adab al-Mufrad (500) | 58% | **82%** | 82% | 85% | 100 |
| al-Shama'il (396) | 72% | **87%** | 86% | 94% | 97 |
| Musannaf Ibn Abi Shayba (500) | — | **77%** | 89% | 81% | 60 |
| al-Sunan al-Kubra, al-Nasa'i (500) | — | **88%** | 87% | 87% | 139 |

\* Joint resolver already applied, before that compiler's shaykh books were added.
✱ Over 100%: the current system counts more links than our name segments (it also links the book's transmitters), so its share is not comparable.
‡ Measured after the chain starts behind the compiler's own name (see §6.2). The earlier 53% counted the transmitter «يحيى» and «مالك» himself, mostly resolved wrongly. The drop from 62% to 57% with تاريخ بغداد is 65 wrong links removed: "عن مالك أنه بلغه" had been resolved to the Companion «مالك بن صعصعة»; agreement rose from 63% to 69%.
§ The current system's coverage is low because 18% of its Muslim chains are cut short (§6.3).
† Not comparable: many records have shifted boundaries (§6.1), so the two systems read different isnads from the same record. A manual review of ~50 resolved al-Bayhaqi narrators found 1 error ("أبي إسحاق" from Zuhayr, which should be al-Sabi'i).

Since the shifted-record fix in `gap_test.py`, al-Mustadrak's agreement is low (60%) for the same reason (†).

The table is measured after Ibn Hibban's book (`ري الظمآن`) was added. That step:
- Raised Ibn Hibban from 74% to 79%.
- Raised agreement in every comparable book. Bukhari has 1,417 agreeing names against 1,405, because bare kunyas are now loose aliases.
- Cost about 0.5–1 point in al-Mustadrak and al-Sunan al-Kubra. These are real namesakes from the same generation (Ibn Hibban's «محمد بن عبد السلام» against al-Hakim's), which the resolver now leaves undecided rather than guessing.

Adding al-Daraqutni's book (الدليل المغني) raised Sunan al-Daraqutni from 68% to 73%, and al-Sunan al-Kubra of al-Bayhaqi from 69% to 71%, with no losses elsewhere. A review of 12 al-Daraqutni chains (~50 names) found 2 errors:
- «عكرمة» after an unresolved «أيوب» → عكرمة بن خالد, instead of the mawla of Ibn Abbas.
- «شريك عن أبي حمزة» (a saying of al-Nakha'i) → Anas. The chain is consistent but wrong; Abu Hamza there is Maymun al-A'war.

Its misses are mostly Baghdadi narrators (سعدان بن نصر، سعيد بن بحر القراطيسي) who belong to تاريخ بغداد.

The Ibn Khuzaymah step (`المسالك القويمة`) added his shaykh and student lists, but his shaykhs are mostly in Tahdhib already. The larger gain came from general fixes found while reviewing his gaps, which raised every book: Bukhari from 73% to 76%, Ibn Hibban from 79% to 81% (agreement 84%). The fixes:
- Five Companions recovered in the Tahdhib parse.
- Kunya-only entries anchored on their own kunya.
- "المعروف بأبي X" read as a kunya.
- Laqabs without "ال".
- "عبيد الله" as one name.
- Taqrib's nasab used as a second nasab.

Corrected along the way: «أبو الزناد» (was أبو القاسم بن أبي الزناد), «الأعرج» (was ثابت بن عياض الأحنف), «أبي موسى الأشعري», «ابن مسعود», «بندار». A review of 12 Ibn Khuzaymah chains (~75 names) found 1 error, «ابن شهاب» → عاصم بن كليب, which is now fixed.

Abu Awanah's remaining misses are mostly his own shaykhs (أبو أمية الطرسوسي، الصغاني، ابن الجنيد، محمد بن حيويه), with no dedicated book in Shamela. They are left to تاريخ بغداد / السير.

تعجيل المنفعة adds 1,113 early narrators, but each is rare, so a 500-hadith sample barely moves: Malik +8 names, Ahmad +1, Ibn Hibban −6, Bukhari −2. What made it neutral rather than harmful:
- 263 entries are remarks on isnads of Tahdhib narrators («شعبة بن الحجاج», «عمرو بن شعيب», «محمد بن عبد الرحمن بن أبي ذئب»). They are skipped when their nasab is a prefix of a Tahdhib nasab, or with 3+ names an in-order subsequence of one.
- Entries without lists (notes on names, «عبد الرزاق») are skipped.
- Its lists name narrators briefly ("نافع"). Such 'short' items give a teacher/student link only when they resolve to one narrator; otherwise every Nafi' became a link.

The early compilers (no shaykh books needed) read 72–77% with 78–82% agreement, since their narrators are in Tahdhib. Two fixes came from them:
- Musnad al-Humaydi went from 44% to 75%. "حدثنا الحميدي، ثنا سفيان" opens almost every chain. «الحميدي» was neither recognised as the compiler nor found at all: his laqab is in Tahdhib's second sentence, and is now taken from Taqrib.
- Sa'id b. Mansur's agreement went from 60% to 81% once «سعيد» opening the chain was taken as the compiler.

Al-Bazzar's 71% agreement is mostly the current system's errors: «نافع» → نافع بن همام, «عكرمة» → عكرمة بن منصور, «عبيد الله» → عبيد الله بن معاذ.

The last 10 books (the six books, al-Darimi, al-Adab al-Mufrad, al-Shama'il, Ibn Abi Shayba, al-Nasa'i's al-Kubra) read 72–82%, with 76–87% agreement except Muslim (§6.3). Three fixes came from them:
- Itqan's text of al-Adab al-Mufrad and al-Shama'il puts invisible direction marks (U+200F) around the colon: "قال‏:‏". The verb was not recognised, so whole segments ("بشر بن محمد، قال‏:‏") failed. `chain_segments` now strips them: al-Adab al-Mufrad went from 58% to 77%, al-Shama'il from 72% to 82%.
- "عن أبيه" after a narrator whose father is named by a kunya («سهيل بن أبي صالح») found a kunya-only entry «أبو عبيد». It now looks for the shaykh with that kunya (ذكوان أبو صالح).
- «زكرياء» and «زكريا» are one spelling.

Ibn Abi Shayba has 22 of 500 hadiths with no narrator resolved (the current system: 1). Not reviewed yet.

**تاريخ بغداد (736)** added ~4,900 later narrators, with the shaykh and student lists from al-Khatib's opening paragraph ("سمع X، وY. روى عنه Z"), not from his own isnads. Al-Daraqutni went from 74% to 76%; the other books moved by at most a point. On the way:
- **Namesakes of Tahdhib narrators** («محمد بن الصباح، أبو يعقوب الصوفي» is not al-Dulabi) are kept. An entry is skipped as a Tahdhib narrator only when the nasab matches and, if al-Khatib gives a kunya or nisba, one of those words is in the Tahdhib header too; with ism + father only, a shared kunya is not enough.
- **Cross-book duplicates** («ابن صاعد» had four entries under four heads) made names tie that a single entry resolves. All shaykh-book entries now go through one merge: same nasab as far as both go (one-letter typos allowed), no conflicting kunya, never two entries of the same book, and a shared rare nisba/laqab (a common one such as «الكاتب» needs a second shared word or the kunya), or a shared kunya with four names. Complete linkage: a narrator joins a group only if he fits every member. A review of 25 merged groups under these rules found no clear wrong merge; the first, looser version had merged nine different «إبراهيم بن محمد بن أحمد».
- "المعروف بالشافعي" in a header now gives the laqab «الشافعي» (أبو بكر الشافعي).

**What is left is mostly our matching, not missing books.** In the al-Daraqutni, Kabir, Mustadrak and Bukhari samples, the unresolved names are mainly:
- Famous Tahdhib narrators named by grandfather or by a well-known "ابن X": «عبد الله بن أحمد بن حنبل» (56 times in the Kabir and Mustadrak samples), «عثمان بن أبي شيبة», «سعيد بن أبي مريم», «علي بن المديني», «إسحاق بن راهويه», «إسماعيل ابن علية», «محمد بن أبي عدي». Name matching wants the father right after the ism.
- Text not cleaned: «أبي هريرة رضى الله عنه» (ى), accusative «أبا هريرة» / «جابرا» / «أنسا», «مسدد قالا», «أبي، ح».
- Companions and famous single names left ambiguous when the neighbour is unresolved («ابن عمر», «ابن عباس», «الزهري»).

**Clean-up and names by grandfather** fixed the first two. Bukhari went from 77% to 83%, Muslim from 74% to 82%, the Kabir from 80% to 84%, al-Daraqutni from 76% to 80%, the Muwatta from 57% to 66%; every book gained. Ours is now above the current system in Bukhari, the Kabir, Muslim and Abu Awanah, and level in Ibn Hibban, Abu Dawud and al-Nasa'i.
- **Clean-up** (`clean_segment`): "رضي/رضى الله عنه" and similar end the name; a comma ends it unless the nasab goes on ("عائشة، زوج النبي", "ابن جريج، أخبرهم"; but "عبد الملك، بن أبي بكر"); a trailing "ح" (tahwil); "قالا:" after a name; accusative «أبا هريرة» → «أبي هريرة», «جابرا» → «جابر» (only when the form without ا is a known ism). Alone this added 33 «أبي هريرة», 17 «عائشة», 15 «ابن عباس» to 500 Bukhari isnads.
- **Unnamed narrators**: «رجل», «امرأة», «شيخ» no longer match entries such as "رجل من آل سهل بن حنيف".
- **Names by grandfather or by "ابن X"** (`by_grandfather`): when nobody has the given father, the words after the ism must come in order in the narrator's own name part, and a name taken from the nasab must be the grandfather at most. This finds «عبد الله بن أحمد بن حنبل», «عثمان بن أبي شيبة», «سعيد بن أبي مريم», «علي بن المديني», «إسحاق بن راهويه», «إسماعيل ابن علية», «محمد بن أبي عدي», «إسماعيل بن أبي أويس», «محمد بن إشكاب», «حفص بن أبي داود» (حفص بن سليمان). Allowing any ancestor gave «أحمد بن أسد» → Ahmad b. Hanbal and «أيوب بن جابر» → أيوب بن خالد بن صفوان بن أوس بن جابر.
- A manual review of 20 random new resolutions (four books, Companions excluded) found 1 error, from an older path: «ابن منيع» as al-Daraqutni's shaykh → محمد بن سعد بن منيع; it is al-Baghawi, whose entry does not record that name.

**Companions and common names** (the resolver gap of §7). The cause was mostly not ambiguity but **lists missing from the Tahdhib parse**, which left the most frequent narrators without context:
- Women's lists say «روت عن» / «روى عنها»: Aisha, Umm Salama, Hafsa, Asma', Amra had none.
- A line break inside a list ended it: Abu Hurayra's students stopped at 17 (now 329), Shu'ba's at 10, Ibn Uyayna's at 2 (now 188), Qatada's at 8.
- Items joined without "، و" («(بخ) ومسلم», «(س) , ويحيى») were swallowed by the item before: Abu Salih al-Samman had 17 students, Mu'awiya, Jabir and Mu'adh lost dozens.
- 21 entries with a head «١٠٢٥:» (no dash) were missing: al-Harith al-A'war, Jumay' b. Umayr, Abu Umayya al-Tarsusi.

Fixing them added ~5,600 list items. Complete lists also make real namesakes tie, so the fame tie-break now settles ties between candidates equally linked to their neighbours (rules in §5), and four matching bugs it exposed were fixed: «ابن عمر» → his grandsons, «أبو سلمة بن عبد الرحمن» missing Abu Salama b. Awf, «أبي بن كعب» read as «أبو كعب», and «ذكوان أبو صالح» without his kunya. The agreement metric was also corrected: a compound name («عبد الرحمن بن صخر») was cut to three words, and the header compared only to its first 120 characters. Against the old code measured with the same metric:
- Coverage rose 2–8 points in every book (Bukhari 83% → 85%, Muslim 82% → 86%, Ibn Majah 81% → 87%, al-Bazzar 76% → 84%); overall agreement 81.8% → 82.1%.
- 2,576 names newly resolved agree with the current system, 727 differ, 621 are past its chain (186 «عائشة»). 314 earlier disagreements were dropped and 212 corrected (Sufyan b. Uyayna, al-Sabi'i, al-Harith al-A'war); 218 agreeing names were lost or changed.
- Many "disagreements" are the current system's: «أبي صالح» from al-A'mash → باذام (it is Dhakwan al-Samman, whom it also names «ذكوان بن الله», so every Dhakwan counts as a disagreement), «عكرمة» → عكرمة بن منصور, «عبيد الله» from Yahya → عبيد الله بن معاذ, «نافع» → نافع بن همام, Ibn Sirin as «محمد بن أبي عمرة».
- Tie-break resolutions agree 73% (ordinary chain resolutions 81%), largely for the reasons above.

**The compiler's shaykh list for the first link.** It was already used (the edge from the compiler to his shaykh), but al-Hakim was never found as the compiler: the Mustadrak prefix «…بن حمدويه الحاكم» did not match his header «…بن حمدويه بن نعيم», and an empty duplicate entry from السلسبيل shares that prefix. The compiler is now the matching entry with the most lists. The Mustadrak rose from 78% to 79% (94 names gained); «أبو بكر بن إسحاق» is now Ahmad b. Ishaq al-Faqih, not Ibn Khuzaymah. Other findings:
- Tahdhib gives no shaykh list for al-Tirmidhi, al-Nasa'i and Ibn Majah («قد سميناهم في مواضعهم»); their link comes from each shaykh's student list, which works.
- «أبو بكر محمد بن أحمد» in the Mustadrak is a real ambiguity: several of al-Hakim's shaykhs carry it (Ibn Balawayh among them), so it stays undecided. Putting the compiler's shaykhs ahead of the 80-candidate cut changed nothing there, and for a bare «محمد» it made things worse, so it was not kept.

**Precision review (150 names, run `final`).** A fixed sample (`data/shamela_rijal/review/precision_sample.py final 150 2026`: 15 resolved names from each of 10 books) was judged against the isnad, the registry lists and knowledge of the narrators, ignoring the current system. A Sonnet sub-agent did the first pass; every "wrong" / "unsure" and 40 random "right" verdicts were then checked by hand, and all were confirmed. Result: **141 right, 8 wrong, 1 unsure: precision 94%** (95% CI 90–98%).
- By method: `unique` 65/66, `chain` 73/78, `chain_fame` 3/6 (small, but in line with its lower agreement).
- **Where we disagree with the current system (19 names), ours was right every time.** The agreement column above understates our accuracy.
- Names past the end of the current chain are the weak spot: 16 right, 6 wrong. The errors: a kunya stub entry («أبو التياح» instead of يزيد بن حميد); segmentation («إسماعيل - وهو ابن علية -» read as Ibn Ayyash, so «عبد العزيز» went wrong too; «سعيد وأبى سلمة» as one name); later namesakes chosen by the chain («أبى هريرة» → محمد بن أيوب الكلابي, «أبو الوليد الفقيه» → ابن عرق); «محمد» after Hisham → Abu al-Zubayr (Ibn Sirin); Daraqutni's «إسحاق بن إبراهيم» → Ibn Rahawayh (al-Dabari); «أبيه» after a wrong «طلحة».
- Verdicts are in `review/precision_firstpass.json`.

**Segmentation fixes from the review.** The trailer rule that strips a place after a name («ببغداد», «بنيسابور») stripped **any** last word starting with «ب»: «بكر», «بكير», «بشار», «بلال», «بشر», «بردة», «بريدة» (over 7,000 segments in Bukhari and al-Bayhaqi alone). «محمد بن بشار» became «محمد بن», «يحيى بن بكير» «يحيى بن»; the chain often still found them, which hid the bug. It now never strips a word after «بن / بنت / أبي / أبو / أبا / أم». Also: dashes around a note («إسماعيل، - وهو ابن علية -») hid «وهو»; «X وهو ابن Y» / «X يعني ابن Y» now give «X ابن Y», «يعني X» gives X like «هو X», but «وهو من بني …» (a description) ends the name; «X وأبي Y» keeps the first shaykh. Coverage +1 to +4 points in 17 books (Ibn Khuzaymah +4, al-Bayhaqi +3, Ahmad +2); agreement steady or +1. Counted by narrator per isnad: 1,460 gained (986 agree with the current system, 305 differ), 546 lost (317 had disagreed: the wrong guesses for «محمد بن»; 121 agreed, mostly a name pushed past the first-8 limit by a now-complete first name, or a full name spelled differently in the registry, «شبيب بن بشير» / «بشر»). The current system's column moves by −1 because our name-segment count, its denominator, grew.

**Rules of `ContextualDisambiguator`, ported where our evidence confirms them.** A Sonnet sub-agent listed its rules (111 entries, ~62 distinct; every condition is the student; targets are Itqan ids) and mapped the 77 Itqan ids to registry entries (67 exact, 10 probable; all spot-checks right). Measured against our resolutions on the 31-book bench, its unconditional rules agree with ours 95% of the time (15,105 / 15,895). The 28 bare names where we agree 97%+ over 30+ cases are a fallback (`DEFAULT_FOR` in `chain_resolver.py`) when the chain cannot decide: 471 names gained, none lost («ابن عباس» 136, «عائشة» 113, «ابن عمر» 36, «جابر بن عبد الله» 31, «الزهري» 26). Rules our evidence contradicts are not ported: «سفيان» (28% differ), «جابر» (26%), «يحيى بن سعيد» (35%), «عكرمة», «محمد بن كثير», «ابن علية». The conditional rules (student-specific) are what the chain already does from the lists.

**Correction to the earlier disagreement reviews:** Itqan's «نافع بن همام», «عكرمة بن منصور» and «محمد بن أبي عمرة» are naming errors for the right narrator, not identification errors. With the id mapping in the metric, agreement rose 1–4 points in most books and 9 in the Muwatta.

**Precision review, 500 names (run `rules`, after the segmentation, glued-kunya and rules fixes).** Same method on a new sample (`review/p500/`: `precision_sample.py rules 500 2027`, 50 names from each of 10 books): Sonnet first pass, then every "wrong" / "unsure" and 48 random "right" checked by hand — all confirmed. **485 right, 13 wrong, 2 unsure: precision 97.0%** (95% CI 95.5–98.5), up from 94% (90–98) on 150 names before those fixes.
- By method: `unique` 200/204, `chain` 271/280, `chain_fame` 11/13, `default` 3/3. By the current system: where it agrees 367/368, where it differs 60/67, past its chain 58/65.
- The errors, by cause: segmentation (a footnote mark with Latin digits «(1)» cut «ابن أبي أنيس» out, so «الزهري» became his nephew; «هذا» and «بعض» taken as names; «يعنون ابن جعفر» not read); «قال ابن المثنى», a speaker tag in Muslim repeating the previous narrator, read as a new link (2); «ابن علية» → his son Hammad (the Taqrib alias is used only when nothing else matches); «مالك بن يحيى» → مالك بن دينار أبو يحيى (the grandfather rule accepts a kunya word); chain errors («هشام» → Ibn Urwa instead of al-Dastuwa'i, «محمد بن جعفر» → al-Warkani instead of Ghundar, «الزهري» → سعد بن إبراهيم); al-Hakim's «أبو بكر» in a quoted isnad.

A manual review of 12 Ibn Hibban chains (~70 names) found 1 likely error: "أبي جعفر" from يحيى بن أبي كثير → al-Baqir, probably al-Ansari al-Mu'adhdhin.

### 6.1 Data finding: shifted hadith records (affects the live app today)

`check_record_boundaries.py` shows that in some books extracted earlier from Shamela (`build_itqan_books.py`), a record holds a matn followed by the **next** hadith's numbered isnad ("… ١٠٨٣٨ - أخبرنا …"). For those records, the matn and the isnad shown together do not belong to each other.

| Book | Prefix tail (own isnad, previous hadith's end in front) | **Shifted** (isnad of the next hadith) |
|---|---|---|
| السنن الكبرى للبيهقي | 45% | **30%** |
| شعب الإيمان | 24% | **34.5%** |
| المستدرك | 54% | **19%** |
| مسند البزار | 10% | 0.6% |

The other books are clean, or have no numbered markers. Abd al-Razzaq, Ibn Khuzaymah and al-Daraqutni open with other forms; these were not checked further. The current database was built from the same files, so its trees for these hadiths can pair a matn with another hadith's chain. Phase 4 fixes this; it can also be fixed earlier in `build_itqan_books.py` if the user wants.

### 6.2 Data finding: the Muwatta's chains loop through «يحيى بن سعيد» (affects the live app today)

The Muwatta's isnads open with its transmitter: "حدثني يحيى، عن مالك، عن ابن شهاب". The current database reads "يحيى" as يحيى بن سعيد الأنصاري and makes him Malik's shaykh. This produces the loop Malik → يحيى بن سعيد → Malik → ابن شهاب in **576 of the 1,860 Muwatta hadiths**. In 74 more, "نافع" is linked to نافع بن همام.

Our resolver had the same problem, and now `compare_current.py` starts each chain after the compiler's own name. The name, in the first two positions, must include the compiler among its candidates, and be either his most-cited candidate or one of the book's usual openers (first two positions of 20%+ of the sample: «الحميدي», «سعيد»). So "محمد" in a Bukhari isnad is not taken for al-Bukhari. Phase 5 must do the same when chains are rebuilt.

### 6.3 Data finding: Muslim's chains are often cut short (affects the live app today)

In the live database, **1,296 of Muslim's 7,368 hadiths (18%)** have a first chain of one narrator or none. In Bukhari it is 2%, in al-Tirmidhi 1%. Many of these are Muslim's follow-up isnads ("وحدثنا ... بهذا الإسناد"). In 500 sampled hadiths, the current system's coverage was 72%, below ours (74%), and agreement only 71%. The disagreements reviewed were mostly the current system's: a chain of one name, «نافع» → نافع بن همام, «عبيد الله» → عبيد الله بن معاذ, «عمرو» from Ibn Wahb → عمرو بن دينار (it is عمرو بن الحارث). Phase 5 must rebuild these chains.

**Precision (manual review of the disagreements):**
- **Bukhari:** of 20 disagreements, ours was right in ~15, there was 1 clear error of ours, and the rest were the same person spelled differently or undecided. The current system has repeated errors: "نافع" → نافع بن همام, "الليث" → "الليثي", "أبو الوليد" (from Shu'ba) → هشام بن عمار, "أبو معمر" → إسماعيل بن إبراهيم.
- **Mustadrak:** most disagreements happen because the current chain is truncated or mixes two isnads.
- **Mu'jam al-Kabir:** 2 errors of ours in 18, both fixed.

**Summary:** the current system covers more; ours resolves less, but what it resolves is usually right. These are small manual samples, not a full precision measurement.

## 7. The plan we are following: phases and tasks

Update the checkboxes whenever a task is finished, and record the commit next to it.
**Next task:** fix what the precision review found (Phase 2, resolver gaps). The record-boundary fix (§6.1) stays in Phase 4 unless the user asks for it earlier.

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
- [x] al-Bayhaqi: إتحاف المرتقي (123666), السلسبيل النقي (123667), with exact name forms and al-Hakim merged as his shaykh (`1741406`)
- [x] Ibn Hibban: ري الظمآن بتراجم شيوخ ابن حبان (1498), with his kunya table as loose aliases; bare-kunya aliases made loose; full-name fame fallback in the resolver (`c5dbe46`)
- [x] al-Daraqutni: الدليل المغني لشيوخ الدارقطني (7852), same `bracket` layout (`2c51097`)
- [x] Ibn Khuzaymah: المسالك القويمة (151171), `runs` layout. Abu Awanah: no dedicated book (241 checked). General matching fixes found on the way, and five Companions recovered in the Tahdhib parse (`38c03f9`)
- [x] Ahmad and Malik: تعجيل المنفعة (1893), `tajil` layout; chains start after the compiler's own name (`10461aa`)
- [x] Early compilers (Abd al-Razzaq, al-Tayalisi, al-Shafi'i, al-Humaydi, Sa'id b. Mansur, Ishaq, al-Bazzar, Abu Ya'la): measured, 72–77%; no shaykh books needed. Compiler openers and Taqrib laqabs fixed al-Humaydi (`34125c6`)
- [x] Measure every remaining book of the 31 against the current system at least once: 72–82%. Direction marks stripped from isnads, "عن أبيه" via the father's kunya (`d65b670`)

General rijal books for what remains (each needs its own parser):
- [x] تاريخ بغداد (736): `khatib` layout, stricter Tahdhib skip, cross-book duplicate merge; al-Daraqutni 74% → 76% (`bcf95f2`)
- [ ] سير أعلام النبلاء (10906): not planned for the resolver — Lisan showed that extra biography books add namesakes about as often as narrators; reconsider only if grading needs it
- [x] لسان الميزان (36357): parsed (`parse_lisan.py` → `lisan.json`, 9,170 entries, 1,646 cross-references; first draft by a sub-agent) but **not merged** into the resolver. Even with namesakes of Tahdhib narrators skipped (same ism + father) and Tahdhib laqabs used as isms («بندار بن محمد») skipped, its 3,544 added narrators gained 182 names and lost 221 across the 31 books: criticised men rarely meant in these isnads, tying with «قتيبة», «علي بن المديني», «الحسن بن سفيان». Kept for narrator grading (Phase 3/5); the `lisan` layout in `parse_shaykh_books.py` is ready but commented out (`61d1618`)
- [ ] الثقات ممن لم يقع في الكتب الستة (96165): same as السير

Remaining resolver gaps:
- [x] Names by grandfather or by "ابن X" («عبد الله بن أحمد بن حنبل», «عثمان بن أبي شيبة», «علي بن المديني», «إسحاق بن راهويه», «إسماعيل ابن علية») (`afaef5c`)
- [x] Isnad clean-up: «رضى الله عنه» with ى, accusative «أبا هريرة» / «جابرا», «قالا» after a name, «أبي، ح»; agreement no longer counts names past the end of the current chain (`afaef5c`)
- [x] Al-Baghawi as «ابن منيع»: tried and not kept. A maternal-grandfather candidate («ابن بنت أحمد بن منيع» → «ابن منيع») fixed 3 al-Daraqutni names but broke 2 real ties («ابن نمير» → سوادة بن علي, «ابن حرب» → الحسن بن عثمان); 73 headers say «ابن بنت X» and most are not called «ابن X». Worth a dedicated alias only if the precision review finds more
- [x] Companions and common names when context is weak: Tahdhib lists repaired (women, line breaks, separators, «N:» heads), fame tie-break for equally linked candidates, «ابن عمر» / «أبو سلمة بن عبد الرحمن» / «أبي بن كعب» / «ذكوان أبو صالح» matching (`b15bcd2`)
- [ ] Real ties left undecided: «أبو إسحاق» (al-Sabi'i / al-Shaybani), «أبو معاوية» (al-Darir / Hushaym), «عبد الله» bare
- [x] The compiler's own shaykh list as context for the first link: already in the resolver; al-Hakim was not found as the compiler, fixed (Mustadrak 78% → 79%) (`56105f6`)
- [ ] "عن أبيه" after an unresolved narrator
- [x] Port `ContextualDisambiguator`: rules listed and Itqan ids mapped (sub-agent), 28 unconditional rules our evidence confirms used as a fallback; the agreement metric uses the id mapping (`59df440`)
- [x] Map every Itqan id used in the current chains to the registry (sub-agent; `review/map_itqan.py` → `review/itqan_id_map_all.json`, git-ignored): 16,337 ids, 9,027 exact / 2,732 probable / 4,578 none; 70 of the 77 hand-checked ids reproduced. "Probable" can be wrong (Abu Hurayra → another «عبد الرحمن بن صخر»), and so can an "exact" reached by eliminating namesakes (1 of 12 in a spot check); exact with a single nasab candidate held up. Used in the metric it changed nothing (those narrators already agree by name), so the metric keeps the 77 hand-checked ids; the full map is for Phase 6's comparison of the two databases
- [ ] Known residual: later namesakes chosen by chain (a handful: «أبو هريرة» → الكلابي, «أبو الوليد الفقيه» → ابن عرق); needs generation data
- [x] Measure precision on a systematic sample: 150 names, 94% (`369fd56`)
- [x] Segmentation fixes from the review: the «ب» trailer bug, «- وهو ابن Y -», «يعني X», «X وأبي Y» (`a5a6751`)
- [x] Kunyas glued in the edition («أبوالتياح», «أبويحيى», ~170 headers) were not read, so «أبي التياح» found only a bare stub from تعجيل المنفعة instead of يزيد بن حميد: split in `norm` / `soft_norm` (not «أبوه», «أبواب», «أبوين»). +1 point in 6 books; «أبي البختري» is now سعيد بن فيروز, not a later namesake (`4453885`)
- [ ] Still from the review: later namesakes winning by chain («أبى هريرة» → الكلابي, «أبو الوليد الفقيه» → ابن عرق)
- [x] Re-measure on 500 names with the sub-agent first pass: **97.0%** (95% CI 95.5–98.5) (`afcfbee`)
- [x] Fix the 500-name review's error classes: Latin-digit footnote marks «(1)», «هذا» not a name, «بعض» unnamed, «يعنون / يعنيان», Muslim's speaker tags («… بن المثنى، قال ابن المثنى» repeats him), the Taqrib alias joins «ابن X» candidates («ابن علية» was his son Hammad), the grandfather rule ignores the narrator's own kunya («مالك بن يحيى» ≠ مالك بن دينار أبو يحيى; an ancestor's «بن أبي شيبة» stays), and Tahdhib heads with a bracketed number («(٢٢٥١) ع: سعيد بن أبي الحسن», 4 entries; 63 later entries renumbered by one, so their `tk` ids changed). 151 names gained, 84 changed — mostly the targeted errors; +1 point in 3 books (`b9b2ad0`)

Housekeeping:
- [x] Delete the superseded `parse_hakim_books.py` and `isnad_test.py` (user approved)
- [x] One reproducible pipeline that writes the final registry (`pipeline.py`; first draft by a sub-agent; strict list links and Companion ranks added in review) (`171eef4`)

### Phase 3 — Ilal data ⬜

- [ ] طبقات المدلسين لابن حجر (1186), replacing the 16 hand-written mudallisin
- [ ] الكواكب النيرات (309) and المختلطين للعلائي (25846), replacing the 7 hand-written mukhtalitun, with heard-before / heard-after students
- [ ] Link both to registry IDs

### Phase 4 — Hadith texts ⬜

- [x] Detect shifted record boundaries in the existing extraction (`check_record_boundaries.py`, `1741406`)
- [ ] Re-split al-Mustadrak, al-Sunan al-Kubra, Shu'ab al-Iman and al-Bazzar on the hadith-number markers, so each record holds its own isnad and matn (§6.1)
- [ ] Choose a Shamela edition for each of the 12 primary books. All are downloaded except الشمائل المحمدية, which the user must download in Shamela.
- [ ] Extract them with footnotes (`foot`) for takhrij and editors' grades
- [ ] Separate the compiler's own remarks from the matn, and keep volume and page references
- [ ] Move all 31 books to `data/shamela/` in one format

### Phase 5 — Code ⬜

- [ ] Domain: `ItqanId` / `ItqanGrade` → a source reference and `Grade`; `ScholarEvaluation` with critic and book; EF migration
- [ ] ETL: a new parser in place of `ItqanDatasetParser`, and a name index built from the registry instead of `by_name.json`
- [ ] Chain building: the joint resolver ported into `ChainReprocessingService` / `ContextualDisambiguator`, starting each chain after the compiler's own name (§6.2: the Muwatta's «يحيى» loop)
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

Day to day, from `data/shamela_rijal`:

```bash
python ../../scripts/shamela4-extractor/rijal_pilot/bench.py <tag>               # 4 books, vs the previous run
python ../../scripts/shamela4-extractor/rijal_pilot/bench.py <tag> --books all   # all 31, before a commit
```

`--base <tag>` picks the run to compare with, `--review N` the number of new disagreements shown (default 12; use 20–25 before a commit). Each name in a saved run carries `vs_current` (`agree` / `differ` / `past`). Checked against the verified run: the same numbers on all 31 books, the same saved results, and `vs_current` reproduces the agreement figures; with no change it reports 0 gained / 0 lost, and with the grandfather rule switched off it lists exactly the lost names.

The whole registry in one command (from the repo root, after the dump step in §3; `<out>` gets the five intermediate files, the cache and `registry.json`):

```bash
cd data/shamela_rijal
python ../../scripts/shamela4-extractor/rijal_pilot/pipeline.py dump <out>
```

Checked: the five intermediate files are byte-identical to the step-by-step commands below, and two runs give the same `registry.json`. In `registry.json`, a list name links to a narrator only when it has one candidate or exactly one candidate lists the narrator back (3.4 links per list against 4.8 names; the resolver itself keeps every candidate). Companions get rank 1 (Ibn Hajar gives them no verdict or tabaqa) when Taqrib says so, or when Taqrib has no verdict and the shaykh list opens with the Prophet; 870 Tahdhib narrators. Run alone, step by step:

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
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sunan_kubra_bayhaqi current_chains_bayhaqi.json "أحمد بن الحسين بن علي بن موسى" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sahih_ibn_hibban current_chains_hibban.json "محمد بن حبان بن أحمد" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sunan_daraqutni current_chains_daraqutni.json "علي بن عمر بن أحمد بن مهدي" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/sahih_ibn_khuzaymah current_chains_khuzaymah_awanah.json "محمد بن إسحاق بن خزيمة" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/mustakhraj_abi_awanah current_chains_khuzaymah_awanah.json "يعقوب بن إسحاق بن إبراهيم بن يزيد أبو عوانة" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/ahmed current_chains_ahmad_malik.json "أحمد بن محمد بن حنبل" 500 0
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/malik current_chains_ahmad_malik.json "مالك بن أنس بن مالك" 500 0
# Early compilers, all against current_chains_early.json: musannaf_abdurrazzaq "عبد الرزاق بن همام بن نافع",
# musnad_tayalisi "سليمان بن داود بن الجارود", musnad_shafii "محمد بن إدريس بن العباس",
# musnad_humaydi "عبد الله بن الزبير بن عيسى", sunan_said_ibn_mansur "سعيد بن منصور بن شعبة",
# musnad_ishaq "إسحاق بن إبراهيم بن مخلد", musnad_bazzar "أحمد بن عمرو بن عبد الخالق", musnad_abi_yala "أحمد بن علي بن المثنى"
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/musnad_humaydi current_chains_early.json "عبد الله بن الزبير بن عيسى" 500 0
# The last 10 books, each against current_chains_<slug>.json (export_chains.ps1 -Books 'صحيح مسلم' ..., then split by book):
# muslim "مسلم بن الحجاج بن مسلم", abudawud "سليمان بن الأشعث بن شداد", tirmidhi and shamail_muhammadiyah
# "محمد بن عيسى بن سورة", nasai and sunan_kubra_nasai "أحمد بن شعيب بن علي", ibnmajah "محمد بن يزيد الربعي",
# darimi "عبد الله بن عبد الرحمن بن الفضل", aladab_almufrad "محمد بن إسماعيل بن إبراهيم بن المغيرة",
# musannaf_ibnabi_shaybah "عبد الله بن محمد بن إبراهيم بن عثمان"
python ../../scripts/shamela4-extractor/rijal_pilot/compare_current.py tahdhib.json ../itqan/sunni/muslim current_chains_muslim.json "مسلم بن الحجاج بن مسلم" 500 0
cd ../.. && python scripts/shamela4-extractor/rijal_pilot/check_record_boundaries.py data/itqan/sunni
```

`compare_current.py` options (environment variables):
- `CHAIN_MODE=greedy` — use the old link-by-link walk instead of the joint resolver.
- `SHOW_DIFF=20` — list disagreements with the current system for review.
- `NO_CACHE=1` — do not use `data/shamela_rijal/cache/`. Linking the registry's ~70k list names takes most of a run (36 s per book); the result is cached under a hash of every registry file and every script in `rijal_pilot/`, so any change to data or code recomputes it. Checked on all 31 books: the old code, the new code without cache, with an empty cache and with a filled cache gave byte-identical results; a run of all 31 went from 373 s to 99 s. `bench.py` runs its first book alone so that, after a change, the other books load the rebuilt cache instead of each rebuilding it in parallel: a run of all 31 after a change went from 168 s to 133 s, with identical results.
- `SAVE=<file>.json` — save every sampled isnad with each name's resolution (ours, how, from which book) and the current chain, to review a run or diff two runs.

Python output with Arabic needs `PYTHONIOENCODING=utf-8` on Windows.

The sample input texts come from `data/itqan/sunni/<book>/`. The 19 non-Itqan books there were already extracted from Shamela; the 12 primary books will be re-sourced from Shamela in Phase 4.

## 9. Pitfalls (for whoever continues)

- **Shell escaping:** in the Bash tool, `\\n`, `\b` and `\s` inside heredocs or `python -c` get mangled. Write scripts with the file-writing tool, not heredocs.
- **Arabic from SQL Server:** `sqlcmd` output loses Arabic. Use `export_chains.ps1` (System.Data.SqlClient → UTF-8 JSON). `pyodbc` is not installed.
- **SQL Server file access:** restores must target the instance data folder; the service cannot write to user temp folders.
- **Workflow:** while changing the resolver, `bench.py <tag>` (4 books, ~15 s with a warm cache) with a 10–15 item review; before a commit, `bench.py <tag> --books all --review 25`.
- **Measure the right thing:**
  - A higher "resolved" rate can hide wrong links. Always check agreement and review disagreements manually.
  - Numbers reported mid-way were corrected several times. For example, a 70.4% Bukhari figure turned out to include wrong shuhra matches.
- **Itqan's coverage of later narrators is broad:** it includes al-Hakim's and al-Tabarani's shaykhs. Do not assume that gap is unique to Shamela.
