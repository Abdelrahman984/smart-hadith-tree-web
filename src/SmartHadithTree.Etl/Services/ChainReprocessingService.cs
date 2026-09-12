using System.Diagnostics;
using System.Text.RegularExpressions;
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

    public async Task ReprocessChainsAsync(string itqanSourcePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting Contextual Disambiguation Engine: Reprocessing Chains...");
        var sw = Stopwatch.StartNew();

        // 1. Initialize Contextual Disambiguator (loads graph into memory)
        await _disambiguator.InitializeAsync(itqanSourcePath, ct);

        // 2. Load mappings from DB
        var narratorsDb = await _dbContext.Narrators
            .Where(n => n.ItqanId != null)
            .Select(n => new { n.Id, ItqanId = n.ItqanId!.Value })
            .ToListAsync(ct);

        var itqanToGuidMap = narratorsDb.ToDictionary(n => n.ItqanId, n => n.Id);
        
        // Needed for starting compilers
        var compilerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["صحيح البخاري"] = 55562,
            ["صحيح مسلم"] = 618,
            ["سنن أبي داود"] = 74,
            ["جامع الترمذي"] = 69584,
            ["سنن النسائي"] = 57802,
            ["سنن ابن ماجه"] = 64080,
            ["مسند أحمد"] = 12657,
            ["موطأ مالك"] = 60209,
            ["سنن الدارمي"] = 56570,
            ["الأربعون النووية"] = 58153,
            ["رياض الصالحين"] = 58153,
            ["الأدب المفرد"] = 55562,
            ["بلوغ المرام"] = 1642,
            ["مصنف ابن أبي شيبة"] = 57598,
        };

        // 3. Delete existing transmissions
        _logger.LogWarning("Truncating existing Transmissions table...");
        await _dbContext.Database.ExecuteSqlRawAsync("TRUNCATE TABLE [Transmissions]", ct);

        // 4. Load all Hadiths
        _logger.LogInformation("Loading Hadiths from database...");
        var hadiths = await _dbContext.Hadiths.AsNoTracking().ToListAsync(ct);

        _logger.LogInformation("Re-parsing {Count} chains...", hadiths.Count);

        var newTransmissions = new List<Transmission>();
        int parsedHadiths = 0;

        foreach (var hadith in hadiths)
        {
            if (string.IsNullOrWhiteSpace(hadith.MatnArabic)) continue;

            var parts = Regex.Split(hadith.MatnArabic, @"(حَدَّثَنَا|حَدَّثَنِي|أَخْبَرَنَا|أَخْبَرَنِي|أَنْبَأَنَا|أَنَّهُ\s+سَمِعَ|أَنَّهَا\s+سَمِعَتْ|سَمِعْتُ|سَمِعَ|سَمِعَتْ|\bعَنْ\b)");
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

                // Remove honorifics
                nameRaw = Regex.Replace(nameRaw, @"(رَضِيَ\s+اللَّهُ\s+عَنْهُ|رَضِيَ\s+اللَّهُ\s+عَنْهُمَا|رَضِيَ\s+اللَّهُ\s+عَنْهَا|رَضِيَ\s+اللَّهُ\s+عَنْهُمْ|صَلَّى\s+اللَّهُ\s+عَلَيْهِ\s+وَسَلَّمَ|عَلَيْهِ\s+السَّلَامُ|رَحِمَهُ\s+اللَّهُ)", "");
                
                // Split on speech boundaries
                nameRaw = Regex.Split(nameRaw, @"(قَالَ|يَقُولُ|أَنَّهُ|أَنَّ|أَنَّهَا|عَلَى\s+الْمِنْبَرِ|وَهُوَ\s+عَلَى\s+الْمِنْبَرِ)")[0];
                nameRaw = nameRaw.Trim(' ', '،', ',', '.', ':', '؛');

                var nameClean = Regex.Replace(nameRaw, @"[^\p{L}\s]", "").Trim();
                nameClean = Regex.Replace(nameClean, @"\s+", " ");

                if (string.IsNullOrWhiteSpace(nameClean)) continue;

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
