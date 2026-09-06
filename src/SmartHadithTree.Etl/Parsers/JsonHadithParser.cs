using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;

namespace SmartHadithTree.Etl.Parsers;

/// <summary>
/// Parses JSON files following the common community Hadith data format
/// (e.g., Sunnah.com-style exports, Hugging Face datasets, fawazahmed0/hadith-api).
/// </summary>
/// <remarks>
/// Expected JSON structure (array of objects):
/// <code>
/// [
///   {
///     "book": "صحيح البخاري",
///     "hadithNumber": 1,
///     "volume": "1",
///     "chapter": "بدء الوحي",
///     "arabicText": "إِنَّمَا الأَعْمَالُ بِالنِّيَّاتِ...",
///     "isnadText": "حَدَّثَنَا الحُمَيْدِيُّ ... عَنْ عُمَرَ بْنِ الخَطَّابِ...",
///     "narrators": [
///       { "name": "عمر بن الخطاب", "knownAs": "أمير المؤمنين", "tier": "صحابي" },
///       ...
///     ]
///   }
/// ]
/// </code>
/// </remarks>
public class JsonHadithParser(ILogger<JsonHadithParser> logger) : IDataSourceParser
{
    public string Name => "JSON Hadith Parser";

    public bool CanParse(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            return Directory.EnumerateFiles(sourcePath, "*.json").Any();
        }

        return File.Exists(sourcePath) &&
               Path.GetExtension(sourcePath).Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken ct = default)
    {
        var dataset = new ParsedDataset();

        // Narrator dedup cache: normalized name → entity
        var narratorCache = new Dictionary<string, Narrator>(StringComparer.OrdinalIgnoreCase);

        var files = Directory.Exists(sourcePath)
            ? Directory.EnumerateFiles(sourcePath, "*.json")
            : [sourcePath];

        foreach (var filePath in files)
        {
            ct.ThrowIfCancellationRequested();
            logger.LogInformation("Parsing JSON file: {FilePath}", filePath);

            await using var stream = File.OpenRead(filePath);
            var records = await JsonSerializer.DeserializeAsync<List<JsonHadithRecord>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ct);

            if (records is null || records.Count == 0)
            {
                logger.LogWarning("No records found in {FilePath}, skipping.", filePath);
                continue;
            }

            foreach (var record in records)
            {
                ProcessRecord(record, dataset, narratorCache);
            }

            logger.LogInformation("Parsed {Count} records from {FilePath}.", records.Count, filePath);
        }

        logger.LogInformation(
            "JSON parsing complete. Totals: {Narrators} narrators, {Hadiths} hadiths, " +
            "{Transmissions} transmissions, {Evaluations} evaluations.",
            dataset.Narrators.Count, dataset.Hadiths.Count,
            dataset.Transmissions.Count, dataset.ScholarEvaluations.Count);

        return dataset;
    }

    private void ProcessRecord(
        JsonHadithRecord record,
        ParsedDataset dataset,
        Dictionary<string, Narrator> narratorCache)
    {
        // 1. Create the HadithText entity
        var hadith = new HadithText
        {
            Id = Guid.CreateVersion7(),
            MatnArabic = record.ArabicText ?? string.Empty,
            NormalizedMatn = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(record.ArabicText ?? string.Empty),
            BookName = record.Book ?? "غير محدد",
            NormalizedBookName = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(record.Book ?? "غير محدد"),
            HadithNumber = record.HadithNumber,
            Volume = record.Volume,
            Chapter = record.Chapter,
            FullIsnadText = record.IsnadText
        };
        dataset.Hadiths.Add(hadith);

        // 2. Resolve narrators (dedup by normalized name)
        if (record.Narrators is null || record.Narrators.Count == 0)
            return;

        var resolvedNarrators = new List<Narrator>();
        foreach (var nr in record.Narrators)
        {
            var key = NormalizeName(nr.Name);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!narratorCache.TryGetValue(key, out var narrator))
            {
                narrator = new Narrator
                {
                    Id = Guid.CreateVersion7(),
                    FullName = nr.Name.Trim(),
                    KnownAs = nr.KnownAs?.Trim(),
                    Kunyah = nr.Kunyah?.Trim(),
                    GenerationTier = nr.Tier?.Trim(),
                    DeathYearHijri = nr.DeathYearHijri,
                    Biography = nr.Biography?.Trim()
                };
                narratorCache[key] = narrator;
                dataset.Narrators.Add(narrator);
            }

            resolvedNarrators.Add(narrator);
        }

        // 3. Build the transmission chain
        // Narrators are listed top-down: [0] = earliest (Prophet/Companion), last = compiler.
        // StepOrder: 1 = compiler (last in list), incrementing upward.
        for (int i = resolvedNarrators.Count - 1; i > 0; i--)
        {
            var student = resolvedNarrators[i];     // The one receiving
            var sheikh = resolvedNarrators[i - 1];  // The one transmitting

            var stepOrder = resolvedNarrators.Count - i; // 1 = compiler, 2 = compiler's sheikh, etc.

            var transmission = new Transmission
            {
                Id = Guid.CreateVersion7(),
                HadithId = hadith.Id,
                SheikhId = sheikh.Id,
                StudentId = student.Id,
                StepOrder = stepOrder,
                TransmissionTerm = ExtractTransmissionTerm(record.IsnadText, sheikh.FullName)
            };
            dataset.Transmissions.Add(transmission);
        }

        // 4. Parse scholar evaluations if present
        if (record.Narrators is not null)
        {
            foreach (var nr in record.Narrators)
            {
                if (nr.Evaluations is null || nr.Evaluations.Count == 0)
                    continue;

                var key = NormalizeName(nr.Name);
                if (!narratorCache.TryGetValue(key, out var narrator))
                    continue;

                foreach (var eval in nr.Evaluations)
                {
                    var evaluation = new ScholarEvaluation
                    {
                        Id = Guid.CreateVersion7(),
                        NarratorId = narrator.Id,
                        ScholarName = eval.Scholar?.Trim() ?? "غير محدد",
                        EvaluationText = eval.Text?.Trim() ?? string.Empty,
                        SourceBook = eval.Source?.Trim(),
                        VerdictRating = eval.Verdict?.Trim()
                    };
                    dataset.ScholarEvaluations.Add(evaluation);
                }
            }
        }
    }

    /// <summary>
    /// Attempts to extract the transmission formula (حدثنا، أخبرنا، عن) from the raw Isnad text
    /// based on proximity to a narrator's name.
    /// </summary>
    private static string? ExtractTransmissionTerm(string? isnadText, string narratorName)
    {
        if (string.IsNullOrWhiteSpace(isnadText))
            return null;

        // Common transmission terms in order of specificity
        string[] terms = ["حَدَّثَنَا", "حدثنا", "أَخْبَرَنَا", "أخبرنا", "سَمِعْتُ", "سمعت", "عَنْ", "عن"];

        // Find the narrator's name in the Isnad text
        var nameIndex = isnadText.IndexOf(narratorName, StringComparison.Ordinal);
        if (nameIndex < 0)
            return null;

        // Look for the closest preceding transmission term (within ~30 chars before the name)
        var searchStart = Math.Max(0, nameIndex - 30);
        var searchRegion = isnadText[searchStart..nameIndex];

        foreach (var term in terms)
        {
            if (searchRegion.Contains(term, StringComparison.Ordinal))
                return term.Replace("َ", "").Replace("ْ", "").Replace("ِ", "").Replace("ُ", "").Replace("ّ", "");
        }

        return null;
    }

    /// <summary>
    /// Normalizes an Arabic narrator name for deduplication by stripping
    /// common diacritics and extra whitespace.
    /// </summary>
    private static string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Strip Arabic diacritics (Tashkeel)
        var normalized = name
            .Replace("َ", "").Replace("ُ", "").Replace("ِ", "")  // Fatḥah, Ḍammah, Kasrah
            .Replace("ْ", "").Replace("ّ", "").Replace("ً", "")  // Sukūn, Shaddah, Tanwīn Fatḥ
            .Replace("ٌ", "").Replace("ٍ", "")                   // Tanwīn Ḍamm, Tanwīn Kasr
            .Trim();

        // Collapse multiple spaces
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // ── Internal JSON DTOs ─────────────────────────────────────────

    private sealed class JsonHadithRecord
    {
        public string? Book { get; set; }
        public int HadithNumber { get; set; }
        public string? Volume { get; set; }
        public string? Chapter { get; set; }
        public string? ArabicText { get; set; }
        public string? IsnadText { get; set; }
        public List<JsonNarratorRecord>? Narrators { get; set; }
    }

    private sealed class JsonNarratorRecord
    {
        public string Name { get; set; } = string.Empty;
        public string? KnownAs { get; set; }
        public string? Kunyah { get; set; }
        public string? Tier { get; set; }
        public int? DeathYearHijri { get; set; }
        public string? Biography { get; set; }
        public List<JsonEvaluationRecord>? Evaluations { get; set; }
    }

    private sealed class JsonEvaluationRecord
    {
        public string? Scholar { get; set; }
        public string? Text { get; set; }
        public string? Source { get; set; }
        public string? Verdict { get; set; }
    }
}
