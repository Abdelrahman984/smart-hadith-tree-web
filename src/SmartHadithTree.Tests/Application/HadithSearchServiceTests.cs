using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Interfaces;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Infrastructure.Data;
using Xunit;

namespace SmartHadithTree.Tests.Application;

public class HadithSearchServiceTests
{
    private HadithTreeDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<HadithTreeDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        return new HadithTreeDbContext(options);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithShamelaAndOperator_ReturnsMatchingHadiths()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 1,
                MatnArabic = "نهى رسول الله صلى الله عليه وسلم عن بيع الغرر",
                NormalizedMatn = "نهى رسول الله صلى الله عليه وسلم عن بيع الغرر",
                NormalizedBookName = "صحيح البخاري"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 2,
                MatnArabic = "إنما الأعمال بالنيات",
                NormalizedMatn = "انما الاعمال بالنيات",
                NormalizedBookName = "صحيح البخاري"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Phrases = new List<string> { "نهى", "بيع" },
            Operator = SearchLogicalOperator.And,
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(1);
        result[0].HadithNumber.Should().Be(1);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithShamelaExclude_FiltersOutExcludedHadiths()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 101,
                MatnArabic = "صيام يوم عرفة أحتسب على الله أن يكفر السنة",
                NormalizedMatn = "صيام يوم عرفه احتسب على الله ان يكفر السنه",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 102,
                MatnArabic = "من صام رمضان إيمانا واحتسابا",
                NormalizedMatn = "من صام رمضان ايمانا واحتسابا",
                NormalizedBookName = "صحيح مسلم"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Phrases = new List<string> { "صيام" },
            ExcludePhrases = new List<string> { "رمضان" },
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(1);
        result[0].HadithNumber.Should().Be(101);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithOrdered_MatchesOnlyCorrectOrder()
    {
        // Arrange
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 201,
                MatnArabic = "نهى عن بيع الملامسة",
                NormalizedMatn = "نهى عن بيع الملامسه",
                NormalizedBookName = "صحيح البخاري"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح البخاري",
                HadithNumber = 202,
                MatnArabic = "البيع بالخيار ما لم يتفرقا ولا نهى في ذلك",
                NormalizedMatn = "البيع بالخيار ما لم يتفرقا ولا نهى في ذلك",
                NormalizedBookName = "صحيح البخاري"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        var request = new SearchRequestDto
        {
            Phrases = new List<string> { "نهى", "بيع" },
            Operator = SearchLogicalOperator.And,
            IsOrdered = true,
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert
        result.Should().HaveCount(1);
        result[0].HadithNumber.Should().Be(201);
    }

    [Fact]
    public async Task SearchHadithsAsync_WithBothAndAndOrPhrases_MatchesCorrectly()
    {
        // Arrange:
        // Hadith 301: Contains "نهى" (AND) + "الغرر" (OR match 1)
        // Hadith 302: Contains "نهى" (AND) + "النجش" (OR match 2)
        // Hadith 303: Contains "نهى" (AND) + "السلم" (neither OR matches)
        // Hadith 304: Contains "الغرر" (OR match) but NOT "نهى"
        var context = GetInMemoryDbContext();
        context.Hadiths.AddRange(
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 301,
                MatnArabic = "نهى رسول الله عن بيع الغرر",
                NormalizedMatn = "نهى رسول الله عن بيع الغرر",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 302,
                MatnArabic = "نهى رسول الله عن النجش",
                NormalizedMatn = "نهى رسول الله عن النجش",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 303,
                MatnArabic = "نهى عن بيع السلم في التمر",
                NormalizedMatn = "نهى عن بيع السلم في التمر",
                NormalizedBookName = "صحيح مسلم"
            },
            new HadithText
            {
                Id = Guid.NewGuid(),
                BookName = "صحيح مسلم",
                HadithNumber = 304,
                MatnArabic = "في بيع الغرر أحكام متعددة",
                NormalizedMatn = "في بيع الغرر احكام متعدده",
                NormalizedBookName = "صحيح مسلم"
            }
        );
        await context.SaveChangesAsync();

        var chainRepo = new Mock<IHadithChainRepository>();
        var taqwiyah = new Mock<ITaqwiyahService>();
        var service = new HadithSearchService(context, chainRepo.Object, taqwiyah.Object);

        // Search for (AND: "نهى") + (OR: "الغرر" OR "النجش")
        var request = new SearchRequestDto
        {
            AndPhrases = new List<string> { "نهى" },
            OrPhrases = new List<string> { "الغرر", "النجش" },
            Scope = SearchScope.Matn
        };

        // Act
        var result = await service.SearchHadithsAsync(request);

        // Assert:
        // Must match 301 and 302, but NOT 303 (missing OR) and NOT 304 (missing AND)
        result.Should().HaveCount(2);
        result.Select(r => r.HadithNumber).Should().BeEquivalentTo(new[] { 301, 302 });
    }
}

