using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Application.Services;

public class HadithSearchService(
    IHadithTreeDbContext context,
    IHadithChainRepository chainRepository,
    ITaqwiyahService taqwiyahService,
    IIlalAnalysisService? ilalService = null) : IHadithSearchService
{
    public async Task<List<HadithSearchResultDto>> SearchHadithsAsync(SearchRequestDto request, CancellationToken ct = default)
    {
        // 1. Determine phrases
        var rawAndPhrases = request.AndPhrases != null && request.AndPhrases.Count > 0
            ? request.AndPhrases.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
            : [];

        var rawOrPhrases = request.OrPhrases != null && request.OrPhrases.Count > 0
            ? request.OrPhrases.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
            : [];

        // Backward compatibility with legacy request.Phrases and request.Operator
        if (rawAndPhrases.Count == 0 && rawOrPhrases.Count == 0)
        {
            var legacyPhrases = request.Phrases != null && request.Phrases.Count > 0
                ? request.Phrases.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToList()
                : [];

            if (legacyPhrases.Count == 0 && !string.IsNullOrWhiteSpace(request.Query))
            {
                legacyPhrases = [request.Query.Trim()];
            }

            if (request.Operator == SearchLogicalOperator.Or)
            {
                rawOrPhrases = legacyPhrases;
            }
            else
            {
                rawAndPhrases = legacyPhrases;
            }
        }

        if (rawAndPhrases.Count == 0 && rawOrPhrases.Count == 0)
            return [];

        var normalizedAndPhrases = rawAndPhrases
            .Select(SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize)
            .ToList();

        var normalizedOrPhrases = rawOrPhrases
            .Select(SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize)
            .ToList();

        var excludePhrases = (request.ExcludePhrases ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(p.Trim()))
            .ToList();

        var queryable = context.Hadiths.AsQueryable();

        // 2. Exclude phrases (NOT / ليس)
        foreach (var excluded in excludePhrases)
        {
            queryable = queryable.Where(h => !h.NormalizedMatn.Contains(excluded));
        }

        // 3. Apply AND phrases
        if (normalizedAndPhrases.Count == 1 && normalizedOrPhrases.Count == 0 && request.Match != SearchMatchType.Exact)
        {
            var singleNormalized = normalizedAndPhrases[0];
            var words = singleNormalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (request.Match == SearchMatchType.AnyWord && words.Length > 1)
            {
                queryable = ApplyOrPhrases(queryable, words.ToList(), request.Scope);
            }
            else // AllWords or single word
            {
                foreach (var w in words)
                {
                    if (request.Scope == SearchScope.All)
                    {
                        queryable = queryable.Where(h => h.NormalizedMatn.Contains(w) ||
                                                         h.NormalizedBookName.Contains(w) ||
                                                         (h.FullIsnadText != null && h.FullIsnadText.Contains(w)));
                    }
                    else if (request.Scope == SearchScope.Matn)
                    {
                        queryable = queryable.Where(h => h.NormalizedMatn.Contains(w));
                    }
                    else if (request.Scope == SearchScope.Isnad)
                    {
                        queryable = queryable.Where(h => h.FullIsnadText != null && h.FullIsnadText.Contains(w));
                    }
                }
            }
        }
        else
        {
            foreach (var np in normalizedAndPhrases)
            {
                if (request.Scope == SearchScope.All)
                {
                    queryable = queryable.Where(h => h.NormalizedMatn.Contains(np) ||
                                                     h.NormalizedBookName.Contains(np) ||
                                                     (h.FullIsnadText != null && h.FullIsnadText.Contains(np)));
                }
                else if (request.Scope == SearchScope.Matn)
                {
                    queryable = queryable.Where(h => h.NormalizedMatn.Contains(np));
                }
                else if (request.Scope == SearchScope.Isnad)
                {
                    queryable = queryable.Where(h => h.FullIsnadText != null && h.FullIsnadText.Contains(np));
                }
            }
        }

        // 4. Apply OR phrases (if any)
        if (normalizedOrPhrases.Count > 0)
        {
            queryable = ApplyOrPhrases(queryable, normalizedOrPhrases, request.Scope);
        }

        // 5. In-Order (مرتبة) and Proximity (متقاربة) evaluation on AND phrases
        var orderingPhrases = normalizedAndPhrases.Count > 1 ? normalizedAndPhrases : normalizedOrPhrases;
        if ((request.IsOrdered || request.IsProximity) && orderingPhrases.Count > 1)
        {
            var candidates = await queryable
                .Take(100)
                .Select(h => new
                {
                    h.Id,
                    h.BookName,
                    h.HadithNumber,
                    h.Chapter,
                    h.MatnArabic,
                    h.NormalizedMatn
                })
                .ToListAsync(ct);

            var filtered = candidates.Where(h =>
            {
                if (request.IsOrdered && !CheckOrdered(h.NormalizedMatn, orderingPhrases))
                    return false;

                if (request.IsProximity && !CheckProximity(h.NormalizedMatn, orderingPhrases, request.ProximityWords))
                    return false;

                return true;
            })
            .Take(50)
            .Select(h => new HadithSearchResultDto
            {
                Id = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                Chapter = h.Chapter,
                MatnArabic = h.MatnArabic,
                MatnSnippet = h.MatnArabic.Length > 150 
                    ? h.MatnArabic.Substring(0, 150) + "..." 
                    : h.MatnArabic
            })
            .ToList();

            return filtered;
        }

        var hadiths = await queryable
            .Take(50)
            .Select(h => new HadithSearchResultDto
            {
                Id = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                Chapter = h.Chapter,
                MatnArabic = h.MatnArabic,
                MatnSnippet = h.MatnArabic.Length > 150 
                    ? h.MatnArabic.Substring(0, 150) + "..." 
                    : h.MatnArabic
            })
            .ToListAsync(ct);

        return hadiths;
    }

    private static IQueryable<HadithText> ApplyOrPhrases(IQueryable<HadithText> queryable, List<string> phrases, SearchScope scope)
    {
        if (phrases.Count == 0) return queryable;

        var parameter = Expression.Parameter(typeof(HadithText), "h");
        var containsMethod = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        Expression? combined = null;

        foreach (var phrase in phrases)
        {
            var phraseConst = Expression.Constant(phrase);
            Expression predicate;

            if (scope == SearchScope.Matn)
            {
                var matnProp = Expression.Property(parameter, nameof(HadithText.NormalizedMatn));
                predicate = Expression.Call(matnProp, containsMethod, phraseConst);
            }
            else if (scope == SearchScope.Isnad)
            {
                var isnadProp = Expression.Property(parameter, nameof(HadithText.FullIsnadText));
                var isnadNotNull = Expression.NotEqual(isnadProp, Expression.Constant(null, typeof(string)));
                var isnadContains = Expression.Call(isnadProp, containsMethod, phraseConst);
                predicate = Expression.AndAlso(isnadNotNull, isnadContains);
            }
            else
            {
                var matnProp = Expression.Property(parameter, nameof(HadithText.NormalizedMatn));
                var matnContains = Expression.Call(matnProp, containsMethod, phraseConst);

                var bookProp = Expression.Property(parameter, nameof(HadithText.NormalizedBookName));
                var bookContains = Expression.Call(bookProp, containsMethod, phraseConst);

                var isnadProp = Expression.Property(parameter, nameof(HadithText.FullIsnadText));
                var isnadNotNull = Expression.NotEqual(isnadProp, Expression.Constant(null, typeof(string)));
                var isnadContains = Expression.Call(isnadProp, containsMethod, phraseConst);
                var isnadPredicate = Expression.AndAlso(isnadNotNull, isnadContains);

                predicate = Expression.OrElse(Expression.OrElse(matnContains, bookContains), isnadPredicate);
            }

            combined = combined == null ? predicate : Expression.OrElse(combined, predicate);
        }

        if (combined != null)
        {
            var lambda = Expression.Lambda<Func<HadithText, bool>>(combined, parameter);
            queryable = queryable.Where(lambda);
        }

        return queryable;
    }

    private static bool CheckOrdered(string text, List<string> phrases)
    {
        int lastIdx = -1;
        foreach (var phrase in phrases)
        {
            int idx = text.IndexOf(phrase, lastIdx == -1 ? 0 : lastIdx + phrase.Length, StringComparison.Ordinal);
            if (idx == -1 || (lastIdx != -1 && idx <= lastIdx))
                return false;
            lastIdx = idx;
        }
        return true;
    }

    private static bool CheckProximity(string text, List<string> phrases, int maxWords)
    {
        var words = text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var wordIndices = new List<int>();

        foreach (var phrase in phrases)
        {
            var firstWord = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrEmpty(firstWord)) continue;

            int foundIdx = -1;
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Contains(firstWord, StringComparison.Ordinal))
                {
                    foundIdx = i;
                    break;
                }
            }

            if (foundIdx == -1) return false;
            wordIndices.Add(foundIdx);
        }

        if (wordIndices.Count < 2) return true;
        return (wordIndices.Max() - wordIndices.Min()) <= maxWords;
    }

    public async Task<IsnadTreeResponseDto?> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default)
    {
        var hadith = await context.Hadiths
            .Where(h => h.Id == hadithId)
            .FirstOrDefaultAsync(ct);

        if (hadith == null)
            return null;

        var nodes = await chainRepository.GetIsnadTreeAsync(hadithId, ct);

        return new IsnadTreeResponseDto
        {
            HadithId = hadith.Id,
            BookName = hadith.BookName,
            HadithNumber = hadith.HadithNumber,
            MatnArabic = hadith.MatnArabic,
            Nodes = nodes
        };
    }

    /// <summary>
    /// Merges the Isnad chains of multiple Hadiths into a single comparative tree (Takhreej).
    /// </summary>
    public async Task<ComparativeTreeResponseDto?> GetComparativeTreeAsync(
        List<Guid> hadithIds, CancellationToken ct = default)
    {
        if (hadithIds.Count == 0) return null;

        // Fetch source Hadith metadata
        var hadiths = await context.Hadiths
            .Where(h => hadithIds.Contains(h.Id))
            .ToListAsync(ct);

        if (hadiths.Count == 0) return null;

        var sources = hadiths.Select(h => new ComparativeHadithSourceDto
        {
            HadithId = h.Id,
            BookName = h.BookName,
            HadithNumber = h.HadithNumber,
            MatnArabic = h.MatnArabic,
            MatnSnippet = h.MatnArabic.Length > 150
                ? h.MatnArabic.Substring(0, 150) + "..."
                : h.MatnArabic
        }).ToList();

        // Get merged chain nodes
        var nodes = await chainRepository.GetComparativeIsnadTreeAsync(hadithIds, ct);

        var response = new ComparativeTreeResponseDto
        {
            Sources = sources,
            Nodes = nodes
        };

        // Basic Matn Variation Detection
        if (sources.Count > 1)
        {
            var baseSource = sources.First();
            var baseMatn = hadiths.First(h => h.Id == baseSource.HadithId).NormalizedMatn;

            foreach (var source in sources.Skip(1))
            {
                var compareMatn = hadiths.First(h => h.Id == source.HadithId).NormalizedMatn;
                if (baseMatn != compareMatn)
                {
                    // Find the compiler node for this source
                    var compilerNode = nodes.FirstOrDefault(n => n.StepOrder == 1 && n.SourceHadithIds.Contains(source.HadithId));
                    if (compilerNode != null)
                    {
                        compilerNode.HasMatnVariation = true;
                        compilerNode.MatnVariationSnippet = "يوجد اختلاف في لفظ المتن مقارنة بالرواية الأساسية.";
                    }
                }
            }
        }

        if (ilalService != null)
            response.IlalReport = await ilalService.AnalyzeAsync(hadithIds, ct);

        taqwiyahService.CalculateTreeStrength(response);

        return response;
    }

    /// <summary>
    /// Finds related Hadiths across all books by matching normalized Matn text.
    /// Extracts a representative substring from the source Hadith's text and
    /// searches for matches in other books.
    /// </summary>
    public async Task<List<HadithSearchResultDto>> FindRelatedHadithsAsync(
        Guid hadithId, CancellationToken ct = default)
    {
        var hadith = await context.Hadiths
            .Where(h => h.Id == hadithId)
            .FirstOrDefaultAsync(ct);

        if (hadith == null) return [];

        // Extract a representative substring from the Matn for cross-book matching.
        // Skip the first ~30 chars to avoid matching common Isnad prefixes.
        var normalizedMatn = hadith.NormalizedMatn;
        var searchSubstring = normalizedMatn.Length > 110
            ? normalizedMatn.Substring(30, 80)
            : normalizedMatn.Length > 40
                ? normalizedMatn.Substring(0, 40)
                : normalizedMatn;

        if (string.IsNullOrWhiteSpace(searchSubstring)) return [];

        var relatedHadiths = await context.Hadiths
            .Where(h => h.Id != hadithId && h.NormalizedMatn.Contains(searchSubstring))
            .Take(20)
            .Select(h => new HadithSearchResultDto
            {
                Id = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                Chapter = h.Chapter,
                MatnArabic = h.MatnArabic,
                MatnSnippet = h.MatnArabic.Length > 150
                    ? h.MatnArabic.Substring(0, 150) + "..."
                    : h.MatnArabic
            })
            .ToListAsync(ct);

        return relatedHadiths;
    }
}
