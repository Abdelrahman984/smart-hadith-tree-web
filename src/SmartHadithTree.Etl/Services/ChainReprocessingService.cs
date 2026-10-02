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
            .Select(n => new { n.Id, ItqanId = n.ItqanId!.Value })
            .ToListAsync(ct);

        var itqanToGuidMap = narratorsDb.ToDictionary(n => n.ItqanId, n => n.Id);
        
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

        var newTransmissions = new List<Transmission>();
        int parsedHadiths = 0;

        foreach (var hadith in hadiths)
        {
            if (string.IsNullOrWhiteSpace(hadith.MatnArabic)) continue;

            var matnNoVowels = Regex.Replace(hadith.MatnArabic, @"\p{Mn}", "");
            var parts = Regex.Split(matnNoVowels, @"(حدثنا|حدثني|أخبرنا|أخبرني|أنبأنا|أنه\s+سمع|أنها\s+سمعت|سمعت|سمع|سمعت|قرأت\s+على|\bعن\b)");
            var step = 1;
            
            Guid? studentGuid = null;
            int? studentItqanId = null;

            if (compilerMap.TryGetValue(hadith.BookName, out var cId))
            {
                studentItqanId = cId;
                if (itqanToGuidMap.TryGetValue(cId, out var cGuid))
                {
                    studentGuid = cGuid;
                }
            }

            for (int i = 1; i < parts.Length - 1; i += 2)
            {
                var term = parts[i].Trim();
                var nameRaw = parts[i + 1];

                // split by wao + comma for multiple sheikhs and take first
                var multipleNames = Regex.Split(nameRaw, @"،\s*و");
                if (multipleNames.Length > 1) {
                    nameRaw = multipleNames[0].Trim();
                }

                // Remove honorifics (unvoweled)
                nameRaw = Regex.Replace(nameRaw, @"(رضي\s+الله\s+عنه|رضى\s+الله\s+عنه|رضي\s+الله\s+عنهما|رضى\s+الله\s+عنهما|رضي\s+الله\s+عنها|رضى\s+الله\s+عنها|رضي\s+الله\s+عنهم|رضى\s+الله\s+عنهم|صلى\s+الله\s+عليه\s+وسلم|عليه\s+السلام|رحمه\s+الله)", "");
                
                // Split on speech boundaries (unvoweled)
                nameRaw = Regex.Split(nameRaw, @"(قال|يقول|أنه|أن|أنها|على\s+المنبر|وهو\s+على\s+المنبر)")[0];
                nameRaw = nameRaw.Trim(' ', '،', ',', '.', ':', '؛');

                var nameClean = Regex.Replace(nameRaw, @"[^\p{L}\s]", "").Trim();
                nameClean = Regex.Replace(nameClean, "ـ", ""); // Remove Kashida
                nameClean = Regex.Replace(nameClean, @"\s+", " ").Trim();
                nameClean = Regex.Replace(nameClean, @"\bأبي\b", "أبو");
                nameClean = Regex.Replace(nameClean, @"\bأبا\b", "أبو");
                nameClean = nameClean.Trim();

                if (string.IsNullOrWhiteSpace(nameClean)) continue;

                string previousNameClean = "";
                if (i >= 3)
                {
                    var prevRaw = parts[i - 1];
                    // split by wao + comma
                    var multiplePrevNames = Regex.Split(prevRaw, @"،\s*و");
                    if (multiplePrevNames.Length > 1) {
                        prevRaw = multiplePrevNames[0].Trim();
                    }
                    prevRaw = Regex.Replace(prevRaw, @"(رضي\s+الله\s+عنه|رضى\s+الله\s+عنه|رضي\s+الله\s+عنهما|رضى\s+الله\s+عنهما|رضي\s+الله\s+عنها|رضى\s+الله\s+عنها|رضي\s+الله\s+عنهم|رضى\s+الله\s+عنهم|صلى\s+الله\s+عليه\s+وسلم|عليه\s+السلام|رحمه\s+الله)", "");
                    prevRaw = Regex.Split(prevRaw, @"(قال|يقول|أنه|أن|أنها|على\s+المنبر|وهو\s+على\s+المنبر)")[0];
                    prevRaw = prevRaw.Trim(' ', '،', ',', '.', ':', '؛');
                    previousNameClean = Regex.Replace(prevRaw, @"[^\p{L}\s]", "").Trim();
                    previousNameClean = Regex.Replace(previousNameClean, "ـ", ""); // Remove Kashida
                    previousNameClean = Regex.Replace(previousNameClean, @"\s+", " ").Trim();
                    previousNameClean = Regex.Replace(previousNameClean, @"\bأبي\b", "أبو");
                    previousNameClean = Regex.Replace(previousNameClean, @"\bأبا\b", "أبو");
                    previousNameClean = previousNameClean.Trim();
                }

                if (nameClean.Equals("أبيه", StringComparison.OrdinalIgnoreCase) || 
                    nameClean.Equals("ابيه", StringComparison.OrdinalIgnoreCase))
                {
                    if (fatherMap.TryGetValue(previousNameClean, out var fatherName))
                    {
                        nameClean = fatherName;
                    }
                }

                // Resolve using the Contextual Disambiguator
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

        _logger.LogInformation("Re-parsing complete. Found {Count} precise transmissions.", newTransmissions.Count);

        // 5. Bulk Ingest new transmissions
        var dataset = new SmartHadithTree.Etl.Parsers.ParsedDataset();
        dataset.Transmissions.AddRange(newTransmissions);

        await _bulkIngestion.IngestAsync(dataset, ct);

        sw.Stop();
        _logger.LogInformation("Chain reprocessing finished in {Elapsed:F1}s.", sw.Elapsed.TotalSeconds);
    }
}
