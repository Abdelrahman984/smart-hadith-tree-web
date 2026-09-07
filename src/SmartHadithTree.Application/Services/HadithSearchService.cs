using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Application.Services;

public class HadithSearchService(IHadithTreeDbContext context, IHadithChainRepository chainRepository) : IHadithSearchService
{
    public async Task<List<HadithSearchResultDto>> SearchHadithsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return [];

        var normalizedQuery = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(query);

        var hadiths = await context.Hadiths
            .Where(h => h.NormalizedMatn.Contains(normalizedQuery) || 
                        h.NormalizedBookName.Contains(normalizedQuery) ||
                        h.Transmissions.Any(t => t.Student.FullName.Contains(query) || 
                                                 t.Sheikh.FullName.Contains(query)))
            .Take(50)
            .Select(h => new HadithSearchResultDto
            {
                Id = h.Id,
                BookName = h.BookName,
                HadithNumber = h.HadithNumber,
                Chapter = h.Chapter,
                MatnSnippet = h.MatnArabic.Length > 150 
                    ? h.MatnArabic.Substring(0, 150) + "..." 
                    : h.MatnArabic
            })
            .ToListAsync(ct);

        return hadiths;
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
            MatnSnippet = h.MatnArabic.Length > 150
                ? h.MatnArabic.Substring(0, 150) + "..."
                : h.MatnArabic
        }).ToList();

        // Get merged chain nodes
        var nodes = await chainRepository.GetComparativeIsnadTreeAsync(hadithIds, ct);

        return new ComparativeTreeResponseDto
        {
            Sources = sources,
            Nodes = nodes
        };
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
                MatnSnippet = h.MatnArabic.Length > 150
                    ? h.MatnArabic.Substring(0, 150) + "..."
                    : h.MatnArabic
            })
            .ToListAsync(ct);

        return relatedHadiths;
    }
}
