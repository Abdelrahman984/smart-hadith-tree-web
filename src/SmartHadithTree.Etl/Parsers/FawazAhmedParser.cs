using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Etl.Parsers;

public class FawazAhmedParser(ILogger<FawazAhmedParser> logger) : IDataSourceParser
{
    public string Name => "Fawaz Ahmed Hadith API Parser";

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

        logger.LogInformation("Parsing Fawaz Ahmed JSON file: {FilePath}", sourcePath);

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

        var bookName = root.Metadata?.Name ?? "غير محدد";

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
                    Chapter = "مجهول",
                    FullIsnadText = isnad
                };
                
                hadithCache[hadithKey] = hadith;
                dataset.Hadiths.Add(hadith);
            }
        }

        logger.LogInformation("Parsed {Count} hadiths from {FilePath}.", dataset.Hadiths.Count, sourcePath);
        return dataset;
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
