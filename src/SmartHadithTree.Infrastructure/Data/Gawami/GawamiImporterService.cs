using SmartHadithTree.Application.Interfaces;

namespace SmartHadithTree.Infrastructure.Data.Gawami;

/// <summary>
/// Placeholder for the Gawami al-Kalim importer. The interface and admin endpoints
/// exist, but the import logic has not been implemented yet.
/// </summary>
public class GawamiImporterService : IGawamiImporterService
{
    public Task ImportNarratorsAsync(string dataDir, CancellationToken cancellationToken) =>
        throw new NotImplementedException("Gawami narrator import is not implemented yet.");

    public Task ImportScholarEvaluationsAsync(string dataDir, CancellationToken cancellationToken) =>
        throw new NotImplementedException("Gawami evaluation import is not implemented yet.");

    public Task ImportIsnadJudgmentsAsync(string dataDir, CancellationToken cancellationToken) =>
        throw new NotImplementedException("Gawami isnad judgment import is not implemented yet.");
}
