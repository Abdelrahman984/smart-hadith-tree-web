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
            .Where(h => h.NormalizedMatn.Contains(normalizedQuery) || h.NormalizedBookName.Contains(normalizedQuery))
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
}
