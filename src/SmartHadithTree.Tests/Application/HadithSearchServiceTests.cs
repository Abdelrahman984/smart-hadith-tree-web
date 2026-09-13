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
}
