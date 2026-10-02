using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartHadithTree.Etl.Parsers.Itqan;
using Xunit;

namespace SmartHadithTree.Tests.Etl;

public class ContextualDisambiguatorTests : IDisposable
{
    private readonly string _tempDir;
    
    public ContextualDisambiguatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var rijalDir = Path.Combine(_tempDir, "rijal");
        Directory.CreateDirectory(rijalDir);

        // Create dummy by_name.json
        var byNameJson = @"{ ""سفيان"": [192, 434], ""معمر"": [40, 9260], ""ابن عيينة"": [192] }";
        File.WriteAllText(Path.Combine(rijalDir, "by_name.json"), byNameJson);

        // Create dummy profiles
        var profilesJson = @"{ ""40"": { ""id"": 40, ""full_name"": ""معمر بن راشد"" }, ""192"": { ""id"": 192 }, ""434"": { ""id"": 434 } }";
        File.WriteAllText(Path.Combine(rijalDir, "profiles_1.json"), profilesJson);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task ResolveSheikh_WithSufyanAndAbdurrazzaq_ReturnsThawri()
    {
        // Arrange
        var logger = new Mock<ILogger<ContextualDisambiguator>>();
        var disambiguator = new ContextualDisambiguator(logger.Object);
        await disambiguator.InitializeAsync(_tempDir);

        // Act
        var result = disambiguator.ResolveSheikh("سفيان", 44); // 44 is Abdurrazzaq

        // Assert
        result.Should().Be(434); // Sufyan al-Thawri
    }

    [Fact]
    public async Task ResolveSheikh_WithSufyanAndShafii_ReturnsUyaynah()
    {
        // Arrange
        var logger = new Mock<ILogger<ContextualDisambiguator>>();
        var disambiguator = new ContextualDisambiguator(logger.Object);
        await disambiguator.InitializeAsync(_tempDir);

        // Act
        var result = disambiguator.ResolveSheikh("سفيان", 2734); // 2734 is al-Shafi'i

        // Assert
        result.Should().Be(192); // Sufyan ibn Uyaynah
    }
    
    [Fact]
    public async Task ResolveSheikh_WithMamar_ReturnsMamarIbnRashid()
    {
        // Arrange
        var logger = new Mock<ILogger<ContextualDisambiguator>>();
        var disambiguator = new ContextualDisambiguator(logger.Object);
        await disambiguator.InitializeAsync(_tempDir);

        // Act
        var result = disambiguator.ResolveSheikh("معمر", null);

        // Assert
        result.Should().Be(40); // Ma'mar ibn Rashid
    }
}
