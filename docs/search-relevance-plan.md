# Plan: relevance scoring and AI judging on `/search`

**Status: plan only, nothing implemented (2026-10-05).** Branch `claude/friendly-keller-81748m`. The deferred `/verify` feature is in `docs/verify-feature-handoff.md`.

## Context

The owner's idea: let the AI judge every search result and give a percentage showing whether the hadith really matches what was searched. The problem it targets: long hadiths that contain the searched words far apart from each other.

What the code does today (`src/SmartHadithTree.Application/Services/HadithSearchService.cs`):

- With several words, `EvaluateTermCluster` looks for all words inside an 800-character window around the longest word, ranks by exact phrase first and then by smallest span (`ThenBy ClusterSpan`), and drops mega-records whose words are scattered. The test `SearchHadithsAsync_FiltersOutScatteredMegaRecord_AndRanksExactPhraseFirst` covers it.
- The span is measured in characters, not words, and says nothing about meaning (a negation, a different subject).
- **Likely recall problem (read from the code, not yet reproduced):** candidates are fetched with `OrderBy(MatnArabic.Length)` then `Take(skip + max(150, pageSize*3))` *before* the proximity ranking. For a broad query, the 150 shortest matching matns are ranked and longer ones never reach the ranking. No score can fix a hadith that was never fetched.
- The result DTO (`HadithSearchResultDto`) has no score field. The page (`frontend/src/app/search/page.tsx`, `HadithCard.tsx`) shows no relevance.

## Why not a literal "AI percentage on every result"

- A percentage from a language model is not calibrated: it looks precise, changes between runs and measures nothing definite. It would weaken the "reliability" criterion.
- Latency: one model review in `/verify` took 10–25 s. 50 results per page is not acceptable inside a search.
- Cost on every keystroke-driven search.

## Recommended design (two layers)

### Layer A: relevance percentage computed by code (instant, explainable)

New pure class `RelevanceScorer` (in `Application/Services/`), unit-tested, used for every result of both the standard and the advanced path.

| Component | Meaning | Weight |
|---|---|---|
| coverage | share of the query words found in the matn (after the project's normalization) | 40 |
| proximity | smallest window **in words** that contains all found words; `min(1, termCount / windowWords)` | 40 |
| phrase/order | exact phrase present, or words appear in the query order | 20 |

Output per result: `relevancePercent` (0–100) and `relevanceReason` (one Arabic line, for example "كل الكلمات (3 من 3) داخل 5 كلمات"). Both are added to `HadithSearchResultDto` and `frontend/src/types/api.ts`. Sorting uses the score first, then the existing tie-breakers (so current behavior is kept where scores tie). The weights are a starting point to tune on the evaluation set, not a measured truth.

### Layer B: on-demand AI judging of the top 10 (one model call)

- New endpoint `POST /api/Search/ai-judge` with `{ query, ids }` (at most 10). The server loads the matn from the database (the client text is not trusted), cuts each to a window around the query words (about 400 characters) and makes **one** Semantic Kernel call.
- The model returns, per result, one of three levels: `match` (يطابق), `partial` (يطابق جزئياً), `scattered` (كلمات متفرقة), plus a short reason that quotes a phrase from the matn. Three levels, not a percentage.
- Validation in code, same pattern as `/verify`: the index must be in the list, and the quote must occur verbatim in that matn (reuse `AiEvaluationService.IsQuoteGrounded`); otherwise the item gets no label. Prompt rules: judge only whether the matn matches the meaning of the query; never grade authenticity; add no content.
- Time limit (env `SEARCH_AI_TIMEOUT_SECONDS`, default 20) and a status field like `/verify` (`reviewed|timeout|unavailable`); in-memory cache by normalized query and hadith id. On failure the page keeps the Layer A score.
- UI: a button "تحقق بالذكاء الاصطناعي (أول 10)" above the results; badges appear on the cards when the answer arrives, without blocking the page; `AiNotice` is shown. Results are not silently reordered: labelled `scattered` items are marked and optionally moved down after the user clicks.

## Steps (in order)

0. **Baseline before changing anything.** Run the new search evaluation (step 6) against the current API and save `search_baseline.json`. (Owner runs it; about 5 minutes.)
1. **Reproduce and fix the candidate cap.** Add a test (in-memory database, like the existing ones in `HadithSearchServiceTests.cs`) with more than 150 short matches plus one long matn containing the exact phrase; show it is dropped. Fix: always include the SQL matches of the exact normalized phrase (cheap `Contains`) in the candidate set in addition to the shortest N, and raise N if latency allows. Re-run the existing tests.
2. **`RelevanceScorer` + tests** (coverage, proximity in words, phrase/order; scattered long matn scores low; exact phrase scores high; diacritics and hamza forms do not matter). Reuse `ArabicNormalizer` and the tokenizer from `HadithVerificationService` instead of writing a new one.
3. **Wire the scorer into `HadithSearchService`** (both paths), add DTO fields, show a bar and the reason on `HadithCard.tsx`. Keep the existing sort tie-breakers.
4. **AI judge endpoint + tests** with a fake kernel (valid answer, answer with a quote that is not in the matn, index out of range, timeout, no model).
5. **Search page button and badges.** Mutation through react-query like the existing search hook; Arabic labels; `AiNotice`.
6. **Evaluation set and runner.** `docs/challenge/search-eval-cases.json` (about 20 queries, each with a target identified by a distinctive matn substring so it does not depend on book numbers, plus decoys: long hadiths with the words scattered) and `scripts/eval_search.py` reporting the rank of the target, hit@1/3/10 and MRR for: baseline, Layer A, Layer A + B. Report `modelStatus` and warn when the model did not answer, so a timeout is not read as "the model added nothing".

## Files

- Backend: `HadithSearchService.cs` (candidates, scoring wiring), new `RelevanceScorer.cs`, `ApiDtos.cs` (`HadithSearchResultDto`), new `SearchJudgeService.cs` + DTOs, `SearchController.cs` (endpoint), `Program.cs` (registration), `HadithSearchServiceTests.cs` and new tests.
- Frontend: `features/search/components/HadithCard.tsx`, `app/search/page.tsx`, `lib/api.ts`, `types/api.ts`, reuse `components/AiNotice.tsx`.
- Eval: `docs/challenge/search-eval-cases.json`, `scripts/eval_search.py`.

## Verification

- `dotnet test src/SmartHadithTree.Tests -c Release` (use `DOTNET_ROLL_FORWARD=Major` in the cloud sandbox), `npx tsc --noEmit` and `npx eslint` in `frontend/`.
- Run `scripts/eval_search.py` on the owner's machine with the corpus: before (step 0) and after each layer. Nothing about accuracy is claimed without these numbers.
- Manual check in the browser on 5–10 queries, including one where a long hadith has the words far apart.

## Risks and decisions for the owner

1. **Time.** Submission closes 2026-10-06 23:59 (Riyadh) and the Live Demo, video and presentation are still open (`docs/challenge/gap-analysis.md`). Rough effort: steps 0–3 and 6 (deterministic, no model) about 3–4 hours; steps 4–5 (AI judge) about 2–3 more. Recommended cut if time is short: do steps 0–3 and 6, keep the AI judge for later.
2. **Model speed.** The AI judge needs an answer in roughly 5–8 seconds for 10 results. If the configured model needs 10–25 seconds, switch `Together__Model` to a faster non-reasoning model first, or the button will feel broken.
3. **Fairness of the comparison.** Layer A is the "simpler alternative" the AI is compared against. If Layer A alone solves the cases, say so; the AI judge then stays an optional second opinion.
4. **Weights are a guess** until tuned on the evaluation set; the evaluation set is written by the assistant and the owner should extend it with their own queries.
5. **Search ordering changes** affect everyone using `/search`; keep the existing tests green and compare the baseline run before merging.
