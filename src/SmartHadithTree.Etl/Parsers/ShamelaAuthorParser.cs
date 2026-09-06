using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Domain.Entities;
using System.Text.RegularExpressions;

namespace SmartHadithTree.Etl.Parsers;

public class ShamelaAuthorParser(ILogger<ShamelaAuthorParser> logger) : IDataSourceParser
{
    public string Name => "Shamela SQLite Author Parser";

    public bool CanParse(string sourcePath)
    {
        return File.Exists(sourcePath) && 
               Path.GetFileName(sourcePath).Equals("master.db", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ParsedDataset> ParseAsync(string sourcePath, CancellationToken ct = default)
    {
        var dataset = new ParsedDataset();
        var narratorCache = new Dictionary<string, Narrator>(StringComparer.OrdinalIgnoreCase);

        logger.LogInformation("Connecting to Shamela master database at {FilePath}", sourcePath);

        // We use Microsoft.Data.Sqlite to connect directly to the Shamela master.db
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT author_name, death_number, death_text FROM author WHERE author_name IS NOT NULL AND author_name != '';";

        await using var reader = await command.ExecuteReaderAsync(ct);
        
        while (await reader.ReadAsync(ct))
        {
            var authorName = reader.GetString(0).Trim();
            var deathNumber = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
            var deathText = reader.IsDBNull(2) ? null : reader.GetString(2).Trim();

            // Try to extract a known alias or title from the name if there's a comma or dash, 
            // though Shamela names are usually just full strings.
            var key = SmartHadithTree.Domain.Utilities.ArabicNormalizer.Normalize(authorName);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            if (!narratorCache.ContainsKey(key))
            {
                var narrator = new Narrator
                {
                    Id = Guid.CreateVersion7(),
                    FullName = authorName,
                    KnownAs = deathText, // Using death_text to store contextual info for now
                    DeathYearHijri = deathNumber == 0 ? null : deathNumber,
                    GenerationTier = "عالم/مؤلف (شاملة)", // Scholar/Author from Shamela
                };

                narratorCache[key] = narrator;
                dataset.Narrators.Add(narrator);
            }
        }

        logger.LogInformation("Extracted {Count} scholars/authors from Shamela.", dataset.Narrators.Count);

        return dataset;
    }
}
