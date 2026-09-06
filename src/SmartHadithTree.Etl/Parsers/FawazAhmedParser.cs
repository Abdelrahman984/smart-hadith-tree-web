using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Etl.Parsers;

public class FawazAhmedParser(ILogger<FawazAhmedParser> logger) : IDataSourceParser
{
    public string Name => "Fawaz Ahmed Hadith API Parser";

    private static readonly Regex TashkeelRegex = new(@"[\u0617-\u061A\u064B-\u0652\u0640\uFEFF]", RegexOptions.Compiled);
    private static readonly Regex TermRegex = new(@"(حدثنا|حدثني|أخبرنا|أخبرني|أنبأنا|عن|أنه سمع|سمعت|سمع|أخبره أن|أخبره)\s+", RegexOptions.Compiled);

    public bool CanParse(string sourcePath)
    {
        if (File.Exists(sourcePath) && Path.GetExtension(sourcePath).Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var firstBytes = new byte[2048];
                using var fs = File.OpenRead(sourcePath);
                var read = fs.Read(firstBytes, 0, 2048);
                var str = System.Text.Encoding.UTF8.GetString(firstBytes, 0, read);
                return str.Contains("\"metadata\"");
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken ct = default)
    {
        var dataset = new ParsedDataset();
        var hadithCache = new Dictionary<string, HadithText>(StringComparer.OrdinalIgnoreCase);
        var narratorCache = new Dictionary<string, Narrator>(StringComparer.OrdinalIgnoreCase);

        logger.LogInformation("Parsing Fawaz Ahmed JSON file with Isnad Extraction: {FilePath}", sourcePath);

        await using var stream = File.OpenRead(sourcePath);
        var root = await JsonSerializer.DeserializeAsync<FawazRoot>(
            stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            ct);

        if (root?.Hadiths == null || root.Hadiths.Count == 0)
        {
            logger.LogWarning("No records found in {FilePath}.", sourcePath);
            return dataset;
        }

        var rawBookName = root.Metadata?.Name ?? "غير محدد";
        var bookName = rawBookName.Contains("Bukhari", StringComparison.OrdinalIgnoreCase) 
            ? "صحيح البخاري" 
            : rawBookName;

        // Compiler narrator (Anchor for StepOrder 1)
        var compiler = new Narrator
        {
            Id = Guid.CreateVersion7(),
            FullName = "محمد بن إسماعيل بن إبراهيم البخاري",
            KnownAs = "الإمام البخاري",
            GenerationTier = "أمير المؤمنين في الحديث",
            DeathYearHijri = 256,
            Biography = "صاحب الجامع المسند الصحيح"
        };
        var compilerKey = ArabicNormalizer.Normalize(compiler.KnownAs);
        narratorCache[compilerKey] = compiler;
        narratorCache[ArabicNormalizer.Normalize(compiler.FullName)] = compiler;
        dataset.Narrators.Add(compiler);

        foreach (var record in root.Hadiths)
        {
            var hadithKey = $"{bookName}_{record.HadithNumber}";

            if (!hadithCache.TryGetValue(hadithKey, out var hadith))
            {
                var text = record.Text ?? string.Empty;
                var (isnad, matn) = SplitIsnadAndMatn(text);

                hadith = new HadithText
                {
                    Id = Guid.CreateVersion7(),
                    MatnArabic = matn,
                    NormalizedMatn = ArabicNormalizer.Normalize(matn),
                    BookName = bookName,
                    NormalizedBookName = ArabicNormalizer.Normalize(bookName),
                    HadithNumber = (int)record.HadithNumber,
                    Volume = record.Reference?.Book.ToString() ?? "",
                    Chapter = record.Reference?.Book != null ? $"كتاب {record.Reference.Book}" : "مجهول",
                    FullIsnadText = isnad
                };
                
                hadithCache[hadithKey] = hadith;
                dataset.Hadiths.Add(hadith);

                // Extract Isnad Chain & Transmissions
                var chainSteps = ExtractIsnadSteps(text);
                if (chainSteps.Count > 0)
                {
                    var chainNarrators = new List<Narrator> { compiler };

                    foreach (var step in chainSteps)
                    {
                        var normName = ArabicNormalizer.Normalize(step.NarratorName);
                        if (string.IsNullOrWhiteSpace(normName)) continue;

                        if (!narratorCache.TryGetValue(normName, out var narrator))
                        {
                            narrator = new Narrator
                            {
                                Id = Guid.CreateVersion7(),
                                FullName = step.NarratorName,
                                KnownAs = step.NarratorName,
                                GenerationTier = "راوٍ (صحيح البخاري)"
                            };
                            narratorCache[normName] = narrator;
                            dataset.Narrators.Add(narrator);
                        }
                        chainNarrators.Add(narrator);
                    }

                    // Create Transmissions between consecutive narrators in the chain
                    for (int i = 0; i < chainNarrators.Count - 1; i++)
                    {
                        var term = i < chainSteps.Count ? chainSteps[i].Term : "عن";
                        var transmission = new Transmission
                        {
                            Id = Guid.CreateVersion7(),
                            HadithId = hadith.Id,
                            StudentId = chainNarrators[i].Id,
                            SheikhId = chainNarrators[i + 1].Id,
                            StepOrder = i + 1,
                            TransmissionTerm = term
                        };
                        dataset.Transmissions.Add(transmission);
                    }
                }
            }
        }

        logger.LogInformation(
            "Parsed {Hadiths} hadiths, {Narrators} unique narrators, {Transmissions} transmissions from {FilePath}.",
            dataset.Hadiths.Count, dataset.Narrators.Count, dataset.Transmissions.Count, sourcePath);

        return dataset;
    }

    private static List<(string Term, string NarratorName)> ExtractIsnadSteps(string rawText)
    {
        var clean = TashkeelRegex.Replace(rawText, "");

        // Find where Matn begins
        string[] matnMarkers = 
        [
            "قال رسول الله", "سمعت رسول الله", "أن رسول الله", "ان رسول الله",
            "عن النبي صلى الله عليه وسلم قال", "عن النبي صلى الله عليه وسلم",
            "قال النبي صلى الله عليه وسلم", "يقول : سمعت رسول الله",
            "سأل رسول الله", "أنها قالت أول ما بدئ"
        ];

        int matnIndex = -1;
        foreach (var marker in matnMarkers)
        {
            var idx = clean.IndexOf(marker, StringComparison.Ordinal);
            if (idx != -1 && (matnIndex == -1 || idx < matnIndex))
            {
                matnIndex = idx;
            }
        }

        var isnadPart = matnIndex != -1 ? clean[..matnIndex] : clean[..Math.Min(350, clean.Length)];

        var matches = TermRegex.Matches(isnadPart);
        if (matches.Count == 0) return [];

        var results = new List<(string Term, string NarratorName)>();

        for (int i = 0; i < matches.Count; i++)
        {
            var term = matches[i].Groups[1].Value.Trim();
            var startIndex = matches[i].Index + matches[i].Length;
            var endIndex = (i + 1 < matches.Count) ? matches[i + 1].Index : isnadPart.Length;

            if (startIndex >= isnadPart.Length) break;

            var narratorChunk = isnadPart[startIndex..endIndex];

            narratorChunk = CleanNarratorName(narratorChunk);

            if (narratorChunk.Length >= 2 && narratorChunk.Length <= 60 && !narratorChunk.Contains("رسول الله"))
            {
                results.Add((term, narratorChunk));
            }
        }

        return results;
    }

    private static string CleanNarratorName(string name)
    {
        name = Regex.Replace(name, @"،\s*قال\s*[:\s]*", " ");
        name = Regex.Replace(name, @"قال\s*[:\s]*", " ");
        name = Regex.Replace(name, @"أنه\s+سمع\s+", " ");
        name = Regex.Replace(name, @"(رضي الله عنهما|رضى الله عنهما|رضي الله عنها|رضى الله عنها|رضي الله عنه|رضى الله عنه)", "");
        name = Regex.Replace(name, @"رحمه الله", "");
        name = Regex.Replace(name, @"[،,:.""”«»\[\]\(\)\{\}\-]", " ");
        name = Regex.Replace(name, @"\s+", " ").Trim();

        // Strip narrative context attachments (e.g. "أن الحارث بن هشام سأل", "على المنبر", "في قوله", etc.)
        name = Regex.Replace(name, @"\s+أن\s+.*$", "");
        name = Regex.Replace(name, @"\s+على\s+المنبر.*$", "");
        name = Regex.Replace(name, @"\s+في\s+قوله.*$", "");
        name = Regex.Replace(name, @"\s+ح\s+.*$", "");
        name = Regex.Replace(name, @"\s+(يقول|أنه|أنها|سأل|قالت|نحوه|ك|و|ف)$", "").Trim();

        return name;
    }

    private (string Isnad, string Matn) SplitIsnadAndMatn(string text)
    {
        string[] markers = ["قَالَ رَسُولُ اللَّهِ", "أَنَّ رَسُولَ اللَّهِ", "سَمِعْتُ رَسُولَ اللَّهِ", "يَقُولُ", "عَنِ النَّبِيِّ", "قَالَ النَّبِيُّ"];
        
        int splitIndex = -1;
        foreach (var marker in markers)
        {
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx > 0 && (splitIndex == -1 || idx < splitIndex))
            {
                splitIndex = idx;
            }
        }

        if (splitIndex > 0)
        {
            return (text[..splitIndex].Trim(), text[splitIndex..].Trim());
        }

        return (text, text); 
    }

    private sealed class FawazRoot
    {
        public FawazMetadata? Metadata { get; set; }
        public List<FawazHadith>? Hadiths { get; set; }
    }

    private sealed class FawazMetadata
    {
        public string? Name { get; set; }
    }

    private sealed class FawazHadith
    {
        public double HadithNumber { get; set; }
        public double ArabicNumber { get; set; }
        public string? Text { get; set; }
        public FawazReference? Reference { get; set; }
    }

    private sealed class FawazReference
    {
        public int Book { get; set; }
        public int Hadith { get; set; }
    }
}
