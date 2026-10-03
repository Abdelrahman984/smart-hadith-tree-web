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

    [Fact]
    public async Task ResolveSheikh_WithPrefixMatchTieBreaker_PrefersExactPrefixMatchOverHigherIdScoreSubstring()
    {
        // Arrange: candidate 1126 has higher id_score (6) but name starts with "حماد بن سلمة",
        // whereas candidate 303 starts with "أبو سلمة بن عبد الرحمن"
        var rijalDir = Path.Combine(_tempDir, "rijal");
        File.WriteAllText(
            Path.Combine(rijalDir, "by_name.json"),
            @"{ ""أبو سلمة"": [59420, 303, 1126], ""محمد بن عمرو"": [17, 670], ""أبو بكر بن أبي شيبة"": [748], ""أبي"": [353] }");
        File.WriteAllText(
            Path.Combine(rijalDir, "profiles_1.json"),
            @"{
                ""59420"": { ""id"": 59420, ""full_name"": ""أبو سلمة"", ""id_score"": 1, ""grade_score"": 9 },
                ""303"": { ""id"": 303, ""full_name"": ""أبو سلمة بن عبد الرحمن بن عوف"", ""id_score"": 5, ""grade_score"": 8, ""teachers"": [1512], ""students"": [17] },
                ""1126"": { ""id"": 1126, ""full_name"": ""حماد بن سلمة بن دينار أبو سلمة"", ""id_score"": 6, ""grade_score"": 8, ""teachers"": [17], ""students"": [353] },
                ""17"": { ""id"": 17, ""full_name"": ""محمد بن عمرو بن علقمة بن وقاص"", ""id_score"": 4, ""grade_score"": 6, ""teachers"": [303] },
                ""670"": { ""id"": 670, ""full_name"": ""يزيد بن هارون بن زاذي ومحمد بن عمرو"", ""id_score"": 6, ""grade_score"": 9 },
                ""748"": { ""id"": 748, ""full_name"": ""عبد الله بن محمد بن أبي شيبة"" },
                ""353"": { ""id"": 353, ""full_name"": ""أحمد بن محمد بن حنبل"" }
            }");

        var disambiguator = new ContextualDisambiguator();
        await disambiguator.InitializeAsync(_tempDir);

        // Act & Assert
        disambiguator.ResolveSheikh("أبو سلمة", 17).Should().Be(303);
        disambiguator.ResolveSheikh("محمد بن عمرو", 14).Should().Be(17);
        disambiguator.ResolveSheikh("أبو بكر بن أبي شيبة", 514).Should().Be(748);
        disambiguator.ResolveSheikh("أبي", 333).Should().Be(353);
    }

    [Theory]
    [InlineData("أبي بكر بن أبي شيبة", "أبو بكر بن أبي شيبة")]
    [InlineData("إسماعيل، يعني ابن جعفر،", "إسماعيل بن جعفر")]
    [InlineData("أبي،", "أبي")]
    [InlineData("المغيرة بن شعبة رضي الله عنه قال", "المغيرة بن شعبة")]
    public void CleanNarratorSegment_NormalizesFilialKunyahsAndExplicativeNasab(string raw, string expected)
    {
        var cleaned = SmartHadithTree.Etl.Services.ChainReprocessingService.CleanNarratorSegment(raw);
        cleaned.Should().Be(expected);
    }
}
