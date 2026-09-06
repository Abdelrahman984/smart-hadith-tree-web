using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Application.Interfaces;

public interface IHadithSearchService
{
    Task<List<HadithSearchResultDto>> SearchHadithsAsync(string query, CancellationToken ct = default);
    Task<IsnadTreeResponseDto?> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default);
}

public interface INarratorService
{
    Task<NarratorDetailDto?> GetNarratorDetailsAsync(Guid narratorId, CancellationToken ct = default);
    Task<NarratorSummaryDto?> GetNarratorTooltipAsync(Guid narratorId, CancellationToken ct = default);
}
