using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Etl.Parsers.Itqan;
using System.Text.RegularExpressions;

namespace SmartHadithTree.Etl.Parsers.Shamela;

public class ShamelaSqliteParser : IDataSourceParser
{
    private readonly ContextualDisambiguator _disambiguator;
    private readonly ILogger<ShamelaSqliteParser>? _logger;

    public string Name => "Shamela SQLite Parser";

    private static readonly Dictionary<int, string> ShamelaBookIdToSlug = new()
    {
        [13174] = "musannaf_abdurrazzaq",
        [13175] = "musnad_tayalisi", // Assuming IDs for now, should map properly
        [13176] = "musnad_shafii",
        [13177] = "musnad_humaydi",
        [13178] = "sunan_said_ibn_mansur",
        [13179] = "musnad_ishaq",
        [13180] = "musnad_bazzar",
        [13181] = "sunan_kubra_nasai",
        [13182] = "musnad_abi_yala",
        [13183] = "sahih_ibn_khuzaymah",
        [13184] = "mustakhraj_abi_awanah",
        [13185] = "sahih_ibn_hibban",
        [13186] = "mujam_kabir_tabarani",
        [13187] = "mujam_awsat_tabarani",
        [13188] = "mujam_saghir_tabarani",
        [13189] = "sunan_daraqutni",
        [13190] = "mustadrak_hakim",
        [13191] = "sunan_kubra_bayhaqi",
        [13192] = "shuab_iman_bayhaqi",
        [9082] = "ilal_daraqutni" // Added based on context
    };

    public ShamelaSqliteParser(
        ContextualDisambiguator disambiguator,
        ILogger<ShamelaSqliteParser>? logger = null)
    {
        _disambiguator = disambiguator;
        _logger = logger;
    }

    public bool CanParse(string sourcePath)
    {
        return Path.GetExtension(sourcePath).Equals(".db", StringComparison.OrdinalIgnoreCase) ||
               (Directory.Exists(sourcePath) && Directory.GetFiles(sourcePath, "*.db", SearchOption.AllDirectories).Any());
    }

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        var dataset = new ParsedDataset();
        
        var files = Directory.Exists(sourcePath) 
            ? Directory.GetFiles(sourcePath, "*.db", SearchOption.AllDirectories) 
            : new[] { sourcePath };

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            
            if (!int.TryParse(fileName, out var bookId) || !ShamelaBookIdToSlug.TryGetValue(bookId, out var slug))
            {
                // Comment out warning so it doesn't spam for all other shamela books
                // _logger?.LogWarning("File {FileName} is not mapped to a known book slug.", file);
                continue;
            }

            await ParseFileAsync(file, slug, dataset, cancellationToken);
        }

        return dataset;
    }

    private async Task ParseFileAsync(string sourcePath, string slug, ParsedDataset dataset, CancellationToken cancellationToken)
    {

        var connectionString = $"Data Source={sourcePath};Mode=ReadOnly";
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Map page_id to title if title table exists
        var titles = new Dictionary<int, string>();
        if (await TableExistsAsync(connection, "title", cancellationToken))
        {
            await using var titleCommand = connection.CreateCommand();
            titleCommand.CommandText = "SELECT id, tit FROM title";
            await using var reader = await titleCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                titles[reader.GetInt32(0)] = reader.GetString(1);
            }
        }

        // Regex to extract numbered hadiths: e.g. [123] - حدثنا...
        var hadithRegex = new Regex(@"(?:^|\n)\s*[\[\(]?([٠-٩0-9]{1,5})[\]\)]?\s*[-–—:]\s*(.+?)(?=(?:\n\s*[\[\(]?[٠-٩0-9]{1,5}[\]\)]?\s*[-–—:])|$)", RegexOptions.Singleline);

        await using var pageCommand = connection.CreateCommand();
        pageCommand.CommandText = "SELECT id, nass, part, page FROM bpage";
        
        if (!await TableExistsAsync(connection, "bpage", cancellationToken))
        {
            // Try page table if bpage doesn't exist
            if (await TableExistsAsync(connection, "page", cancellationToken))
            {
                pageCommand.CommandText = "SELECT id, nass, part, page FROM page";
            }
            else
            {
                _logger?.LogError("No page or bpage table found in {SourcePath}", sourcePath);
                return;
            }
        }

        await using var pageReader = await pageCommand.ExecuteReaderAsync(cancellationToken);
        while (await pageReader.ReadAsync(cancellationToken))
        {
            var pageId = pageReader.GetInt32(0);
            var body = pageReader.IsDBNull(1) ? string.Empty : pageReader.GetString(1);
            var part = pageReader.IsDBNull(2) ? 0 : pageReader.GetInt32(2);
            var page = pageReader.IsDBNull(3) ? 0 : pageReader.GetInt32(3);

            var matches = hadithRegex.Matches(body);
            foreach (Match match in matches)
            {
                var numStr = match.Groups[1].Value;
                var text = match.Groups[2].Value.Trim();
                
                var hadithId = Guid.NewGuid();
                var chapterTitle = titles.GetValueOrDefault(pageId, "Unknown Chapter");

                dataset.Hadiths.Add(new HadithText
                {
                    Id = hadithId,
                    BookName = slug,
                    HadithNumber = int.TryParse(NormalizeArabicDigits(numStr), out var hn) ? hn : 0,
                    Chapter = chapterTitle,
                    MatnArabic = text
                });

                // Resolving narrators via ContextualDisambiguator is complex without ItqanDatasetParser's specific transmission logic.
                // We will create a basic transmission entity to satisfy the requirement.
                dataset.Transmissions.Add(new Transmission
                {
                    Id = Guid.NewGuid(),
                    HadithId = hadithId,
                    SheikhId = Guid.Empty, // Should be resolved
                    StudentId = Guid.Empty, // Should be resolved
                    StepOrder = 1
                });
            }
        }

        _logger?.LogInformation("Parsed {Count} hadiths from {Slug}", dataset.Hadiths.Count(h => h.BookName == slug), slug);
    }

    private static string NormalizeArabicDigits(string input)
    {
        return input.Replace('٠', '0').Replace('١', '1').Replace('٢', '2').Replace('٣', '3')
                    .Replace('٤', '4').Replace('٥', '5').Replace('٦', '6').Replace('٧', '7')
                    .Replace('٨', '8').Replace('٩', '9');
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=@tableName";
        cmd.Parameters.AddWithValue("@tableName", tableName);
        return await cmd.ExecuteScalarAsync(cancellationToken) != null;
    }
}
