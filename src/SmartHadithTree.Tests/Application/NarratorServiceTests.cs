using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Infrastructure.Data;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class NarratorServiceTests
{
    private HadithTreeDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new HadithTreeDbContext(options);
    }

    [Fact]
    public async Task GetNarratorDetailsAsync_ShouldReturnNull_WhenNarratorNotFound()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var service = new NarratorService(context);

        // Act
        var result = await service.GetNarratorDetailsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNarratorDetailsAsync_ShouldReturnNarrator_WhenExists()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        var narratorId = Guid.NewGuid();
        context.Narrators.Add(new Narrator
        {
            Id = narratorId,
            FullName = "Test Narrator",
            KnownAs = "Test",
            Biography = "A test biography."
        });
        await context.SaveChangesAsync(default);
        
        var service = new NarratorService(context);

        // Act
        var result = await service.GetNarratorDetailsAsync(narratorId);

        // Assert
        result.Should().NotBeNull();
        result!.FullName.Should().Be("Test Narrator");
        result.Biography.Should().Be("A test biography.");
    }

    [Fact]
    public async Task SearchNarratorsAsync_ListsNamesThatStartWithTheQueryFirst_ThenShorterNames()
    {
        var context = GetInMemoryDbContext();
        context.Narrators.AddRange(
            new Narrator { Id = Guid.NewGuid(), FullName = "محمد بن مسلم بن عبيد الله بن شهاب الزهري" },
            new Narrator { Id = Guid.NewGuid(), FullName = "عبد الله بن محمد الزهري" },
            new Narrator { Id = Guid.NewGuid(), FullName = "الزهري بن عبد الرحمن بن عوف بن عبد عوف" },
            new Narrator { Id = Guid.NewGuid(), FullName = "عبيد الله بن سعد بن إبراهيم", KnownAs = "الزهري" });
        await context.SaveChangesAsync(default);

        var result = await new NarratorService(context).SearchNarratorsAsync("الزُّهْرِيّ");

        result.Select(r => r.FullName).Should().Equal(
            "عبيد الله بن سعد بن إبراهيم",              // KnownAs starts with the query, shorter
            "الزهري بن عبد الرحمن بن عوف بن عبد عوف",  // FullName starts with the query
            "عبد الله بن محمد الزهري",
            "محمد بن مسلم بن عبيد الله بن شهاب الزهري");
    }

    [Fact]
    public async Task GetNarratorTooltipAsync_WithoutAGrade_TakesTheEvaluationsInScholarOrder()
    {
        var context = GetInMemoryDbContext();
        var narratorId = Guid.NewGuid();
        context.Narrators.Add(new Narrator { Id = narratorId, FullName = "راو" });
        context.ScholarEvaluations.AddRange(
            new ScholarEvaluation { Id = Guid.NewGuid(), NarratorId = narratorId, ScholarName = "يحيى بن معين", EvaluationText = "ليس بشيء", VerdictRating = "ضعيف" },
            new ScholarEvaluation { Id = Guid.NewGuid(), NarratorId = narratorId, ScholarName = "أبو حاتم", EvaluationText = "صدوق", VerdictRating = "صدوق" });
        await context.SaveChangesAsync(default);

        var result = await new NarratorService(context).GetNarratorTooltipAsync(narratorId);

        result!.GradeSummary.Should().Be("صدوق");   // «أبو حاتم» sorts before «يحيى بن معين»
    }
}
