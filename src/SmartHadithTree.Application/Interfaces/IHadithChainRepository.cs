using SmartHadithTree.Application.DTOs;

namespace SmartHadithTree.Application.Interfaces;

public interface IHadithChainRepository
{
    Task<List<IsnadNodeDto>> GetIsnadTreeAsync(Guid hadithId, CancellationToken ct = default);
}
