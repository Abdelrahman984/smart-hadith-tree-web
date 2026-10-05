using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Application.Services;

public interface ISearchJudgeService
{
    Task<SearchJudgeResponseDto> JudgeAsync(string query, IReadOnlyList<Guid> ids, CancellationToken ct = default);
}

/// <summary>
/// Optional AI check of the results on a search page: does each matn match the meaning of the search?
/// The server loads the matn itself and sends the model only a short window around the searched words.
/// A label is accepted only when the model returns the result's number exactly once and a quote of three or more
/// words that really occurs in that matn; anything else is "not judged", never a match or a non-match.
/// Large pages are split into chunks sent in parallel. With no model, a timeout or a failure the page keeps the
/// code-computed relevance.
/// </summary>
public class SearchJudgeService(IHadithTreeDbContext context, Kernel kernel) : ISearchJudgeService
{
    public const int MaxIds = 50;
    public const int ChunkSize = 20;
    private const int WindowChars = 350;
    private const int MaxCachedItems = 5000;

    /// <summary>Per-call time limit. Override with SEARCH_AI_TIMEOUT_SECONDS (5-90).</summary>
    private static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(
        int.TryParse(Environment.GetEnvironmentVariable("SEARCH_AI_TIMEOUT_SECONDS"), out var seconds)
            ? Math.Clamp(seconds, 5, 90)
            : 20);

    private static readonly ConcurrentDictionary<string, SearchJudgeItemDto> Cache = new();
    private static readonly Regex Words = new(@"[ء-ي]+", RegexOptions.Compiled);

    public async Task<SearchJudgeResponseDto> JudgeAsync(string query, IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        var terms = RelevanceScorer.QueryTerms(query);
        var requested = ids.Distinct().Take(MaxIds).ToList();
        if (terms.Count == 0 || requested.Count == 0)
            return new SearchJudgeResponseDto { Status = SearchJudgeStatus.Invalid };

        var queryKey = string.Join(' ', terms);
        var judged = new Dictionary<Guid, SearchJudgeItemDto>();
        foreach (var id in requested)
            if (Cache.TryGetValue($"{queryKey}|{id}", out var cached)) judged[id] = cached;

        var missing = requested.Where(id => !judged.ContainsKey(id)).ToList();
        var failedChunks = 0;
        var timedOut = 0;
        var chunkCount = 0;

        if (missing.Count > 0)
        {
            var rows = (await context.Hadiths.AsNoTracking()
                    .Where(h => missing.Contains(h.Id))
                    .Select(h => new Row(h.Id, h.BookName, h.HadithNumber, h.MatnArabic, h.NormalizedMatn))
                    .ToListAsync(ct))
                .ToDictionary(r => r.Id);
            var ordered = missing.Where(rows.ContainsKey).Select(id => rows[id]).ToList();
            var chunks = ordered.Chunk(ChunkSize).ToList();
            chunkCount = chunks.Count;

            var outcomes = await Task.WhenAll(chunks.Select(chunk => JudgeChunkAsync(query, terms, chunk, ct)));
            foreach (var outcome in outcomes)
            {
                if (outcome.Items == null)
                {
                    failedChunks++;
                    if (outcome.TimedOut) timedOut++;
                    continue;
                }
                foreach (var item in outcome.Items)
                {
                    judged[item.Id] = item;
                    if (SearchJudgeLevel.IsJudged(item.Level)) CacheItem(queryKey, item);
                }
            }
        }

        return new SearchJudgeResponseDto
        {
            Status = Status(chunkCount, failedChunks, timedOut),
            Items = requested.Select(id => judged.TryGetValue(id, out var item)
                ? item
                : new SearchJudgeItemDto { Id = id }).ToList()
        };
    }

    private static string Status(int chunks, int failed, int timedOut) =>
        failed == 0 ? SearchJudgeStatus.Reviewed
        : failed < chunks ? SearchJudgeStatus.Partial
        : timedOut > 0 ? SearchJudgeStatus.Timeout
        : SearchJudgeStatus.Unavailable;

    private static void CacheItem(string queryKey, SearchJudgeItemDto item)
    {
        if (Cache.Count > MaxCachedItems) Cache.Clear();
        Cache[$"{queryKey}|{item.Id}"] = item;
    }

    private async Task<ChunkOutcome> JudgeChunkAsync(string query, List<string> terms, Row[] chunk, CancellationToken ct)
    {
        try
        {
            var prompt = BuildPrompt(query, terms, chunk);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(CallTimeout);
            var result = await kernel.InvokePromptAsync(prompt, cancellationToken: timeout.Token);
            var items = ParseAndValidate(result.GetValue<string>(), chunk.Select(r => (r.Id, r.MatnArabic)).ToList());
            return new ChunkOutcome(items, false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ChunkOutcome(null, true);
        }
        catch (Exception)
        {
            // No model configured, provider error, or unreadable answer. Do not leak details.
            return new ChunkOutcome(null, false);
        }
    }

    private static string BuildPrompt(string query, List<string> terms, Row[] chunk)
    {
        var list = string.Join("\n", chunk.Select((r, i) => $"[{i + 1}] {Window(r, terms)}"));
        return $@"
أنت مساعد يفحص نتائج بحث في متون الأحاديث. لديك عبارة بحث، وقائمة مقاطع مرقمة من متون أحاديث.
مهمتك فقط الحكم على كل مقطع: هل يتناول معنى عبارة البحث (match)، أم يتناوله جزئياً (partial)، أم أن كلمات البحث ترد فيه متفرقة دون أن يدور المقطع حول معناها (scattered)؟

قواعد صارمة:
- لا تحكم على صحة الحديث أو ضعفه.
- لا تضف معلومة من خارج المقاطع.
- لكل مقطع اكتب Quote: عبارة من ثلاث كلمات فأكثر منسوخة حرفياً من المقطع نفسه تدعم حكمك.
- أعطِ حكماً واحداً لكل رقم، ولا تكرر رقماً ولا تترك رقماً.

عبارة البحث: {query}

المقاطع:
{list}

أرجع مصفوفة JSON صالحة فقط بدون أي نص إضافي أو علامات Markdown:
[{{""Index"": 1, ""Level"": ""match"", ""Quote"": ""عبارة حرفية"", ""Reason"": ""جملة قصيرة بالعربية""}}]";
    }

    /// <summary>A short window of the raw matn around the tightest group of searched words.</summary>
    private static string Window(Row row, List<string> terms)
    {
        var raw = row.MatnArabic;
        var score = RelevanceScorer.Score(row.NormalizedMatn, terms);
        var start = 0;
        if (score != null && row.NormalizedMatn.Length > 0)
            start = (int)((double)score.WindowStartChar / row.NormalizedMatn.Length * raw.Length);
        var begin = Math.Max(0, start - 80);
        var length = Math.Min(WindowChars, raw.Length - begin);
        var text = raw.Substring(begin, length).Trim();
        var shown = (begin > 0 ? "… " : "") + text + (begin + length < raw.Length ? " …" : "");

        var normalizedShown = Normalize(shown);
        var missing = terms.Count(t => !normalizedShown.Contains(t, StringComparison.Ordinal));
        return missing > 0 ? shown + " (ملاحظة: بعض كلمات البحث لا تظهر في هذا المقطع)" : shown;
    }

    /// <summary>
    /// Reads the model's JSON and keeps only answers that pass every check. Returns one item per row, in order;
    /// rows without a valid answer are <see cref="SearchJudgeLevel.NotJudged"/>.
    /// </summary>
    public static List<SearchJudgeItemDto> ParseAndValidate(string? raw, IReadOnlyList<(Guid Id, string Matn)> rows)
    {
        var items = rows.Select(r => new SearchJudgeItemDto { Id = r.Id }).ToList();
        var json = (raw ?? string.Empty).Trim();
        if (json.StartsWith("```json")) json = json[7..];
        if (json.StartsWith("```")) json = json[3..];
        if (json.EndsWith("```")) json = json[..^3];

        List<Raw>? answers;
        try
        {
            answers = JsonSerializer.Deserialize<List<Raw>>(json.Trim(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return items;
        }
        if (answers == null) return items;

        // A number the model repeats is ambiguous, so none of its answers is trusted.
        foreach (var group in answers.GroupBy(a => a.Index).Where(g => g.Count() == 1))
        {
            var answer = group.Single();
            var index = answer.Index - 1;
            if (index < 0 || index >= rows.Count) continue;

            var level = (answer.Level ?? string.Empty).Trim().ToLowerInvariant();
            if (!SearchJudgeLevel.IsJudged(level)) continue;
            if (Words.Matches(Normalize(answer.Quote)).Count < 3) continue;
            if (!AiEvaluationService.IsQuoteGrounded(answer.Quote, new[] { rows[index].Matn })) continue;

            items[index].Level = level;
            items[index].Quote = answer.Quote!.Trim();
            items[index].Reason = Truncate((answer.Reason ?? string.Empty).Trim(), 200);
        }

        return items;
    }

    private static string Normalize(string? text) =>
        string.Join(' ', Words.Matches(SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(text ?? string.Empty)).Select(m => m.Value));

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private sealed record Row(Guid Id, string BookName, int HadithNumber, string MatnArabic, string NormalizedMatn);
    private sealed record ChunkOutcome(List<SearchJudgeItemDto>? Items, bool TimedOut);

    private sealed class Raw
    {
        public int Index { get; set; }
        public string? Level { get; set; }
        public string? Quote { get; set; }
        public string? Reason { get; set; }
    }
}
