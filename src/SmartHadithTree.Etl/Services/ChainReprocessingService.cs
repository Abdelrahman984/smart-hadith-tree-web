using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Etl.Parsers.Itqan;
using SmartHadithTree.Infrastructure.Data;

namespace SmartHadithTree.Etl.Services;

public class ChainReprocessingService
{
    private readonly HadithTreeDbContext _dbContext;
    private readonly ContextualDisambiguator _disambiguator;
    private readonly BulkDataIngestionService _bulkIngestion;
    private readonly ILogger<ChainReprocessingService> _logger;

    public ChainReprocessingService(
        HadithTreeDbContext dbContext,
        ContextualDisambiguator disambiguator,
        BulkDataIngestionService bulkIngestion,
        ILogger<ChainReprocessingService> logger)
    {
        _dbContext = dbContext;
        _disambiguator = disambiguator;
        _bulkIngestion = bulkIngestion;
        _logger = logger;
    }

    public async Task ReprocessChainsAsync(string itqanSourcePath, string? bookFilter = null, CancellationToken ct = default, bool onlyMissing = false)
    {
        _logger.LogInformation("Starting Contextual Disambiguation Engine: Reprocessing Chains (BookFilter: {Filter}, OnlyMissing: {OnlyMissing})...", bookFilter ?? "ALL", onlyMissing);
        var sw = Stopwatch.StartNew();

        // 1. Initialize Contextual Disambiguator (loads graph into memory)
        await _disambiguator.InitializeAsync(itqanSourcePath, ct);

        // 2. Load mappings from DB
        var narratorsDb = await _dbContext.Narrators
            .Where(n => n.ItqanId != null)
            .Select(n => new { n.Id, ItqanId = n.ItqanId!.Value, n.ItqanGrade })
            .ToListAsync(ct);

        var itqanToGuidMap = narratorsDb.ToDictionary(n => n.ItqanId, n => n.Id);
        var companionItqanIds = narratorsDb
            .Where(n => string.Equals(n.ItqanGrade, "companion", StringComparison.OrdinalIgnoreCase))
            .Select(n => n.ItqanId)
            .ToHashSet();
        
        // Needed for starting compilers (supports both ArabicName and slug keys)
        var compilerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slug, meta) in ItqanDatasetParser.BookMetadata)
        {
            compilerMap[meta.ArabicName] = meta.CompilerItqanId;
            compilerMap[slug] = meta.CompilerItqanId;
        }

        // Resolve target Arabic book name if a slug was passed as bookFilter
        string? targetBookName = null;
        if (!string.IsNullOrWhiteSpace(bookFilter))
        {
            targetBookName = ItqanDatasetParser.BookMetadata.TryGetValue(bookFilter, out var meta)
                ? meta.ArabicName
                : bookFilter;
        }

        // 3. Delete existing transmissions (unless onlyMissing is true)
        if (!onlyMissing)
        {
            if (!string.IsNullOrWhiteSpace(targetBookName))
            {
                _logger.LogWarning("Deleting existing Transmissions for book '{Book}'...", targetBookName);
                await _dbContext.Transmissions
                    .Where(t => t.Hadith.BookName == targetBookName)
                    .ExecuteDeleteAsync(ct);
            }
            else
            {
                _logger.LogWarning("Truncating existing Transmissions table...");
                await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [Transmissions]", ct);
            }
        }

        // 4. Load father map
        var fatherMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fatherMapPath = Path.Combine(itqanSourcePath, "isnad_father_map.json");
        if (File.Exists(fatherMapPath))
        {
            var content = await File.ReadAllTextAsync(fatherMapPath, ct);
            using var doc = JsonDocument.Parse(content);
            foreach (var element in doc.RootElement.EnumerateObject())
            {
                if (element.Name.StartsWith("_comment")) continue;
                fatherMap[element.Name] = element.Value.GetString() ?? "";
            }
        }

        // 5. Load Hadiths (filtered if targetBookName or onlyMissing is specified)
        _logger.LogInformation("Loading Hadiths from database...");
        var hadithsQuery = _dbContext.Hadiths.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(targetBookName))
        {
            hadithsQuery = hadithsQuery.Where(h => h.BookName == targetBookName);
        }
        if (onlyMissing)
        {
            hadithsQuery = hadithsQuery.Where(h => !_dbContext.Transmissions.Any(t => t.HadithId == h.Id));
        }
        var hadiths = await hadithsQuery.ToListAsync(ct);

        _logger.LogInformation("Re-parsing {Count} chains...", hadiths.Count);

        var newTransmissions = ParseHadithChains(hadiths, compilerMap, itqanToGuidMap, companionItqanIds, fatherMap);

        _logger.LogInformation("Re-parsing complete. Found {Count} precise transmissions.", newTransmissions.Count);

        // 5. Bulk Ingest new transmissions
        var dataset = new SmartHadithTree.Etl.Parsers.ParsedDataset();
        dataset.Transmissions.AddRange(newTransmissions);

        await _bulkIngestion.IngestAsync(dataset, ct);

        sw.Stop();
        _logger.LogInformation("Chain reprocessing finished in {Elapsed:F1}s.", sw.Elapsed.TotalSeconds);
    }

    public async Task ReprocessHadithIdsAsync(string itqanSourcePath, IReadOnlyCollection<Guid> hadithIds, CancellationToken ct = default)
    {
        if (hadithIds.Count == 0) return;

        await _disambiguator.InitializeAsync(itqanSourcePath, ct);

        var narratorsDb = await _dbContext.Narrators
            .Where(n => n.ItqanId != null)
            .Select(n => new { n.Id, ItqanId = n.ItqanId!.Value, n.ItqanGrade })
            .ToListAsync(ct);

        var itqanToGuidMap = narratorsDb.ToDictionary(n => n.ItqanId, n => n.Id);
        var companionItqanIds = narratorsDb
            .Where(n => string.Equals(n.ItqanGrade, "companion", StringComparison.OrdinalIgnoreCase))
            .Select(n => n.ItqanId)
            .ToHashSet();

        var compilerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (slug, meta) in ItqanDatasetParser.BookMetadata)
        {
            compilerMap[meta.ArabicName] = meta.CompilerItqanId;
            compilerMap[slug] = meta.CompilerItqanId;
        }

        var fatherMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var fatherMapPath = Path.Combine(itqanSourcePath, "isnad_father_map.json");
        if (File.Exists(fatherMapPath))
        {
            var content = await File.ReadAllTextAsync(fatherMapPath, ct);
            using var doc = JsonDocument.Parse(content);
            foreach (var element in doc.RootElement.EnumerateObject())
            {
                if (element.Name.StartsWith("_comment")) continue;
                fatherMap[element.Name] = element.Value.GetString() ?? "";
            }
        }

        var idsList = hadithIds.Distinct().ToList();
        await _dbContext.Transmissions
            .Where(t => idsList.Contains(t.HadithId))
            .ExecuteDeleteAsync(ct);

        var hadiths = await _dbContext.Hadiths.AsNoTracking()
            .Where(h => idsList.Contains(h.Id))
            .ToListAsync(ct);

        var newTransmissions = ParseHadithChains(hadiths, compilerMap, itqanToGuidMap, companionItqanIds, fatherMap);

        var dataset = new SmartHadithTree.Etl.Parsers.ParsedDataset();
        dataset.Transmissions.AddRange(newTransmissions);
        await _bulkIngestion.IngestAsync(dataset, ct);

        _logger.LogInformation("Reprocessed {HadithCount} hadiths -> {TransmissionCount} transmissions.", hadiths.Count, newTransmissions.Count);
    }

    private List<Transmission> ParseHadithChains(
        List<HadithText> hadiths,
        Dictionary<string, int> compilerMap,
        Dictionary<int, Guid> itqanToGuidMap,
        HashSet<int> companionItqanIds,
        Dictionary<string, string> fatherMap)
    {
        var newTransmissions = new List<Transmission>();
        int parsedHadiths = 0;

        foreach (var hadith in hadiths)
        {
            if (string.IsNullOrWhiteSpace(hadith.MatnArabic)) continue;

            var matnNoVowels = Regex.Replace(hadith.MatnArabic, @"\p{Mn}", "");

            // Truncate at the start of the Prophetic Matn so words inside the Matn or trailing notes are not parsed as narrators
            var matnBoundary = Regex.Match(matnNoVowels, @"(?:قال\s*:?\s*«?\s*كنت\s+مع|كنت\s+مع\s+رسول\s+الله|(?:قال|أنه|أن|أنها|سمعت|سمع|رأيت|كان)\s+(?:رسول\s+الله|النبي|نبي\s+الله))");
            var isnadSection = matnBoundary.Success && matnBoundary.Index > 10
                ? matnNoVowels[..matnBoundary.Index]
                : matnNoVowels;

            var parts = Regex.Split(isnadSection, @"(حدثنا|حدثني|حدثنى|أخبرنا|أخبرني|أخبرنى|اخبرنا|اخبرني|أنبأنا|أنبأني|انبانا|انباني|\bثنا\b|\bأنا\b|\bنا\b|أنه\s+سمع|أنها\s+سمعت|سمعت|سمع|قرأت\s+على|\bعن\b)");
            var step = 1;

            Guid? compilerGuid = null;
            int? compilerItqanId = null;

            if (compilerMap.TryGetValue(hadith.BookName, out var cId))
            {
                compilerItqanId = cId;
                if (itqanToGuidMap.TryGetValue(cId, out var cGuid))
                {
                    compilerGuid = cGuid;
                }
            }

            Guid? studentGuid = compilerGuid;
            int? studentItqanId = compilerItqanId;
            (Guid StudentGuid, int Step, string Term)? pendingBranchHead = null;

            for (int i = 1; i < parts.Length - 1; i += 2)
            {
                var term = parts[i].Trim();
                var prevRawSegment = i >= 2 ? parts[i - 1] : "";

                // Check if the preceding segment ended with a Tahwil marker (e.g. ". وأخبرني" or "ح وحدثنا")
                if (i >= 3 && Regex.IsMatch(prevRawSegment, @"(?:\.\s*و|؛\s*و|\bح\s*و?)\s*$"))
                {
                    if (studentGuid.HasValue && studentGuid != compilerGuid && step > 1)
                    {
                        pendingBranchHead = (studentGuid.Value, step - 1, term);
                    }
                    studentGuid = compilerGuid;
                    studentItqanId = compilerItqanId;
                    step = 1;
                }

                // Stop if we already reached a Companion
                if (studentItqanId.HasValue && companionItqanIds.Contains(studentItqanId.Value))
                {
                    break;
                }

                bool hasQalaConvergence = i >= 3 && Regex.IsMatch(prevRawSegment, @"\bقالا\b");

                var nameClean = CleanNarratorSegment(parts[i + 1]);
                if (string.IsNullOrWhiteSpace(nameClean)) continue;

                string previousNameClean = i >= 3 ? CleanNarratorSegment(parts[i - 1]) : "";

                if (nameClean.Equals("أبيه", StringComparison.OrdinalIgnoreCase) ||
                    nameClean.Equals("ابيه", StringComparison.OrdinalIgnoreCase) ||
                    nameClean.Equals("أبي", StringComparison.OrdinalIgnoreCase) ||
                    nameClean.Equals("ابي", StringComparison.OrdinalIgnoreCase))
                {
                    if (fatherMap.TryGetValue(previousNameClean, out var fatherName) && !string.IsNullOrWhiteSpace(fatherName))
                    {
                        nameClean = fatherName;
                    }
                }

                var sheikhItqanId = _disambiguator.ResolveSheikh(nameClean, studentItqanId);

                Guid? sheikhGuid = null;
                if (sheikhItqanId.HasValue && itqanToGuidMap.TryGetValue(sheikhItqanId.Value, out var sGuid))
                {
                    sheikhGuid = sGuid;
                }

                if (sheikhGuid.HasValue)
                {
                    if (studentGuid.HasValue && studentGuid != sheikhGuid)
                    {
                        newTransmissions.Add(new Transmission
                        {
                            Id = Guid.NewGuid(),
                            HadithId = hadith.Id,
                            StepOrder = step,
                            StudentId = studentGuid.Value,
                            SheikhId = sheikhGuid.Value,
                            TransmissionTerm = term
                        });
                        step++;
                    }

                    // If two paths converged via "قالا", also connect the first branch head to this shared Sheikh
                    if (hasQalaConvergence && pendingBranchHead.HasValue && pendingBranchHead.Value.StudentGuid != sheikhGuid.Value)
                    {
                        newTransmissions.Add(new Transmission
                        {
                            Id = Guid.NewGuid(),
                            HadithId = hadith.Id,
                            StepOrder = pendingBranchHead.Value.Step + 1,
                            StudentId = pendingBranchHead.Value.StudentGuid,
                            SheikhId = sheikhGuid.Value,
                            TransmissionTerm = term
                        });
                        pendingBranchHead = null;
                    }

                    studentGuid = sheikhGuid;
                    studentItqanId = sheikhItqanId;
                }
            }

            parsedHadiths++;
            if (parsedHadiths % 10000 == 0)
            {
                _logger.LogInformation("Parsed {Count} / {Total} chains.", parsedHadiths, hadiths.Count);
            }
        }

        return newTransmissions;
    }

    public static string CleanNarratorSegment(string rawSegment)
    {
        var nameRaw = rawSegment;

        // Split by wao + comma for multiple sheikhs and take first
        var multipleNames = Regex.Split(nameRaw, @"،\s*و");
        if (multipleNames.Length > 1)
        {
            nameRaw = multipleNames[0].Trim();
        }

        // Remove honorifics (unvoweled)
        nameRaw = Regex.Replace(nameRaw, @"(رضي\s+الله\s+عنه|رضى\s+الله\s+عنه|رضي\s+الله\s+عنهما|رضى\s+الله\s+عنهما|رضي\s+الله\s+عنها|رضى\s+الله\s+عنها|رضي\s+الله\s+عنهم|رضى\s+الله\s+عنهم|صلى\s+الله\s+عليه\s+وسلم|عليه\s+السلام|رحمه\s+الله|ﷺ)", "");

        // Normalize explicative nasab clauses ("إسماعيل يعني ابن جعفر" -> "إسماعيل بن جعفر")
        nameRaw = Regex.Replace(nameRaw, @"[\s،\-\–\—]*\bيعني\s+(?:ابن|بن)\b[\s\-\–\—]*", " بن ");

        // Split on speech and tahwil boundaries
        nameRaw = Regex.Split(nameRaw, @"(\bقالا\b|\bقال\b|\bيقول\b|\bأنه\b|\bأن\b|\bأنها\b|على\s+المنبر|وهو\s+على\s+المنبر|\.\s*و|؛)")[0];
        nameRaw = nameRaw.Trim(' ', '،', ',', '.', ':', '؛');

        var nameClean = Regex.Replace(nameRaw, @"[^\p{L}\s]", " ").Trim();
        nameClean = Regex.Replace(nameClean, "ـ", ""); // Remove Kashida
        nameClean = Regex.Replace(nameClean, @"\s+", " ").Trim();

        // Replace leading/standalone kunyah "أبي"/"أبا" with "أبو" ONLY when followed by a kunyah noun and NOT preceded by "بن" or "ابن"
        nameClean = Regex.Replace(nameClean, @"(?<!\b(?:بن|ابن)\s+)\b(أبي|أبا)(?=\s+\p{L})", "أبو");
        return nameClean.Trim();
    }
}
