using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;
using Microsoft.Extensions.Logging;
using SmartHadithTree.Etl.Parsers.Itqan;
using SmartHadithTree.Etl.Parsers.Shamela;
using Xunit;

namespace SmartHadithTree.Tests.Etl;

public class ShamelaSqliteParserTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ShamelaSqliteParser _parser;
    
    public ShamelaSqliteParserTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempFolder);
        _dbPath = Path.Combine(tempFolder, "13174.db");
        var disambiguator = new ContextualDisambiguator(Mock.Of<ILogger<ContextualDisambiguator>>());
        _parser = new ShamelaSqliteParser(disambiguator, Mock.Of<ILogger<ShamelaSqliteParser>>());

        CreateDummyDatabase(_dbPath);
    }

    private void CreateDummyDatabase(string path)
    {
        if (File.Exists(path)) File.Delete(path);

        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE title (id INTEGER PRIMARY KEY, tit TEXT);
            INSERT INTO title (id, tit) VALUES (1, 'كتاب الطهارة');
            
            CREATE TABLE bpage (id INTEGER PRIMARY KEY, nass TEXT, part INTEGER, page INTEGER);
            INSERT INTO bpage (id, nass, part, page) VALUES (1, '123 - عبد الرزاق عن معمر عن الزهري عن سالم عن أبيه قال...', 1, 15);
            INSERT INTO bpage (id, nass, part, page) VALUES (2, '[124] - أخبرنا عبد الرزاق...', 1, 16);
        ";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            var dir = Path.GetDirectoryName(_dbPath);
            File.Delete(_dbPath);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void CanParse_WithDbExtension_ReturnsTrue()
    {
        _parser.CanParse("test.db").Should().BeTrue();
    }

    [Fact]
    public void CanParse_WithOtherExtension_ReturnsFalse()
    {
        _parser.CanParse("test.json").Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_WithValidDb_ExtractsHadiths()
    {
        // Act
        var result = await _parser.ParseAsync(_dbPath);

        // Assert
        result.Should().NotBeNull();
        result.Hadiths.Should().HaveCount(2);

        var firstHadith = result.Hadiths.First();
        firstHadith.BookName.Should().Be("musannaf_abdurrazzaq"); // Maps from 13174
        firstHadith.HadithNumber.Should().Be(123);
        firstHadith.Chapter.Should().Be("كتاب الطهارة");
        firstHadith.MatnArabic.Should().Contain("عبد الرزاق عن معمر عن الزهري");

        var secondHadith = result.Hadiths.Last();
        secondHadith.HadithNumber.Should().Be(124);
        secondHadith.MatnArabic.Should().Contain("أخبرنا عبد الرزاق");
        
        result.Transmissions.Should().HaveCount(2);
    }
}
