using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Infrastructure.Data.Repositories;

namespace SmartHadithTree.Tests.Integration;

/// <summary>
/// Search as PostgreSQL runs it: Contains → LIKE '%…%' on the normalized "C"-collated columns. Every test uses its
/// own made-up marker word, so rows of other tests never match.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SearchTranslationTests(PostgresFixture pg)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HadithSearch_IgnoresTashkeel_InTheQueryAndTheText(bool queryWithTashkeel)
    {
        var word = Marker.New();
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var hit = seed.Hadith("كتاب البيوع", 1, $"نَهَى رَسُولُ اللَّهِ ﷺ عَنْ بَيْعِ {Marker.WithTashkeel(word)} وَبَيْعِ الْغَرَرِ");
        seed.Hadith("كتاب البيوع", 2, "نَهَى رَسُولُ اللَّهِ ﷺ عَنْ بَيْعِ الْغَرَرِ");
        await seed.SaveAsync();

        var query = queryWithTashkeel ? $"بَيْعِ {Marker.WithTashkeel(word)}" : $"بيع {word}";
        var service = new HadithSearchService(db, new HadithChainRepository(db), new TaqwiyahService());
        var results = await service.SearchHadithsAsync(new SearchRequestDto { Query = query, Scope = SearchScope.Matn, PageSize = 20 });

        results.Select(r => r.Id).Should().Equal(hit.Id);
    }

    [Fact]
    public async Task Contains_TranslatesToLike_AndTreatsPercentAndUnderscoreAsText()
    {
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var literal = seed.Hadith("كتاب الرموز", 1, "قيمة 50% و 6_0 هنا");
        var lookalike = seed.Hadith("كتاب الرموز", 2, "قيمة 500 و 600 هنا");
        await seed.SaveAsync();
        var ids = new[] { literal.Id, lookalike.Id };

        var query = db.Hadiths.Where(h => ids.Contains(h.Id) && h.NormalizedMatn.Contains("50%"));
        query.ToQueryString().Should().Contain("LIKE");   // trigram-indexable, not strpos()

        (await query.Select(h => h.Id).ToListAsync()).Should().Equal(literal.Id);
        (await db.Hadiths.Where(h => ids.Contains(h.Id) && h.NormalizedMatn.Contains("6_0")).Select(h => h.Id).ToListAsync())
            .Should().Equal(literal.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NarratorSearch_IgnoresTashkeel_AndListsPrefixMatchesFirst_ThenShorterNames(bool queryWithTashkeel)
    {
        var word = Marker.New();
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var longContains = seed.Narrator($"مُحَمَّدُ بْنُ عَبْدِ اللَّهِ بْنِ سَعِيدٍ الْ{word}يُّ");
        var shortContains = seed.Narrator($"عَلِيٌّ الْ{word}يُّ");
        var prefix = seed.Narrator($"{Marker.WithTashkeel(word)} بْنُ عَمْرِو بْنِ مَالِكٍ الْأَنْصَارِيُّ");
        var byKnownAs = seed.Narrator("عُبَيْدُ اللَّهِ بْنُ سَعْدٍ", knownAs: $"{word} الصغير");
        await seed.SaveAsync();

        var results = await new NarratorService(db).SearchNarratorsAsync(queryWithTashkeel ? Marker.WithTashkeel(word) : word);

        results.Select(r => r.Id).Should().Equal(byKnownAs.Id, prefix.Id, shortContains.Id, longContains.Id);
    }

    [Fact]
    public async Task NarratorSearch_UnifiesAlefVariants()
    {
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var word = Marker.New();
        var narrator = seed.Narrator($"أَحْمَدُ بْنُ إِبْرَاهِيمَ الْ{word}");
        await seed.SaveAsync();

        var results = await new NarratorService(db).SearchNarratorsAsync($"احمد بن ابراهيم ال{word}");

        results.Select(r => r.Id).Should().Equal(narrator.Id);
    }
}
