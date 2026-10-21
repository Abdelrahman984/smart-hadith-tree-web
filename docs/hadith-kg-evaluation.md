# hadith-kg evaluation (stage 1), 2026-10-09

Question: can `emadjumaah/hadith-kg` (Hugging Face, `hadith-kg.db`, 1.63 GB, CC-BY-4.0 by its author) replace or feed our own pipeline (Shamela texts, Python resolver, registry)? Stage 1 measures it; nothing in the repo or the database was changed. The file sits outside the repo (`~/Downloads/hadith-kg.db`, opened read-only).

## What the file is
- Source recorded in its `meta` table: `mongodump hadithdb/hadith (2017-10-12) + rawa_cat.csv`. The origin site is not named; the README says only «raw 2017 source dump». One author, 0 stars, no accuracy figures.
- 425 books, 715,790 hadiths, 577,024 chains (`sanads` + `sanad_rawis`, 4.4M links), 49,844 narrators (`rawis`: tabaqa, rank text, tadlis/ikhtilat flags, birth/death), 127,863 critic statements (`aqwal`), 20,745 `meaning_groups` (hadiths of one matn across books), 54,862 topics.
- Narrator mentions are linked **inside the hadith text** (`hadith_rawis`, character offsets), like Shamela's own links, so identity comes from the source's links, not from guessing. `hadiths.matn_start/matn_end` mark the matn.
- `sanads.hukum` is a formulaic per-chain verdict («إسناده متصل ، رجاله ثقات», «إسناد ضعيف لأن به موضع إرسال»), so continuity is part of its grading.
- Not in it: ilal findings, matn comparison, shawahid/madar logic, a UI. The README mentions `hadiths.grading`; the file has no such column (only `takhrij`, an integer).
- Teacher/student relations are **computed from adjacency in chains**, not taken from Tahdhib lists, so they cannot replace ours for «لم يثبت اللقاء».

## Measurement 1: narrators, against Shamela's own links
Method: records matched by the first 70 letters of the text; narrator mentions aligned by their text; two people count as the same by name (ism + father), shared name words, or same ism + same death year. Oracle: Shamela's links in the 7 linked books. Our side: `data/shamela_rijal/chains/*.json`. Script: `measure.py` (kept in the session scratchpad; promote to `scripts/` if it is to be repeated).

| | hadith-kg | ours |
|---|---|---|
| Mentions compared | 81,289 | 67,997 |
| Agreement with Shamela | **99.38%** | **98.32%** |
| Mentions resolved (of ~83k aligned) | **99.9%** | **81.6%** (15,283 unresolved) |
| Of our unresolved, kg answers and agrees with Shamela | 13,867 (91%) | |

Hand reading of 30 disagreements each (random):
- kg: about 3 real errors (two bare «سفيان» given to al-Thawri where Shamela and we say Ibn Uyayna; «قابوس بن أبي ظبيان» given to his son), 5 unclear, the rest are the same person under another name (matcher noise). Real error rate is about 0.1%.
- ours: about 27 of 30 are real errors. Repeating classes: **«طاوس» resolved to طاوس بن عبد الله بن طاوس** (wrong in every sampled mention, the "shaykh-book duplicate" class, ~11 of the kg-side sample too), bare **«عبد الله» resolved to Abu Musa al-Ash'ari** instead of Ibn Mas'ud (4 of 30), «جابر», «عطاء», «عبدان», «ابن عمر» to namesakes.

Limits:
- Only 14,032 of 34,331 Shamela-linked records matched by text, almost all Bukhari (3,768), Muslim (4,547) and al-Nasa'i (5,133). Tirmidhi (182), Abu Dawud (372), the Muwatta (8), al-Tayalisi (22) are **not measured**: the text keys differ, which says nothing about kg's coverage there.
- Shamela's links are not perfect (earlier review: 5 of 60 disagreements were Shamela's error), and kg and Shamela may share upstream linking; independence is unknown.
- 30-item samples; the matcher is lenient.

## Measurement 2: meaning groups as automatic takhreej
- «إنما الأعمال بالنيات»: group 319, 158 narrations in 84 books; it covers every source of our reviewed page (Bukhari 7, Bayhaqi Kubra 9, Muslim 1, Ahmad 2, Humaydi 1, Ibn Majah 1, Tabarani Awsat 2, Abu Awana 2, Shu'ab 1).
- «الطهور شطر الإيمان»: group 474, 82 narrations in 41 books (Ibn Abi Shayba 6, Ahmad 7, Shu'ab 6, Muslim 2, Bayhaqi 2, Abu Awana 1).
- «أبعد المذهب» (Mughira): not found by that wording.
- Our own `related` endpoint returns **0** for these hadiths (`docs/takhreej-rules-review.md`, problem 11).

## Measurement 3: our narrators without a rank
Of 17,551 narrators used in our chains, 10,325 have no Ibn Hajar rank. By a 3-part name match, 2,891 have a candidate in kg, 1,789 a unique one, and **1,389 (13%) a useful grade** (not «مجهول الحال»); 1,766 have critic quotes. Upper bound (namesakes). kg itself rates 45% of its narrators «مجهول الحال». It narrows the grading gap but does not close it.

## Reading
- Narrator identity and chain coverage: kg is better than our resolver on what was measured (Bukhari, Muslim, al-Nasa'i): fewer errors and far fewer gaps.
- Takhreej grouping: kg's meaning groups do what our `related` fails to do.
- Analysis layer (Ilal rules, matn comparison, shawahid, grade, UI): not in kg; stays ours.

## Open before any decision
1. Which of our 31 books does kg hold, and with what record counts? (Muwatta 1,823 vs our 2,946; Muslim 5,367 vs 7,757 records.)
2. Text key matching failed for four books; a better key is needed to measure them.
3. Provenance and licence of the underlying texts (CC-BY covers the author's work only). Ask the author before any public use.
4. Check `matn_start/matn_end` quality: they could replace our regex matn extraction.
5. How to keep our Ilal data (mudallisin, mukhtalitun, Tahdhib relations) tied to kg narrator ids.
