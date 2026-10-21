using FluentAssertions;
using SmartHadithTree.Infrastructure.Data.Repositories;

namespace SmartHadithTree.Tests.Integration;

/// <summary>The two raw queries of <see cref="HadithChainRepository"/> on a real PostgreSQL database.</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class IsnadQueryTests(PostgresFixture pg)
{
    [Fact]
    public async Task IsnadTree_OfATahwilIsnad_FollowsBothBranchesToTheSharedCompanion()
    {
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var compiler = seed.Narrator("المصنف");
        var a = seed.Narrator("شيخ أول", sourceKey: "it-a", ibnHajarRank: 3);
        var b = seed.Narrator("شيخ ثان", sourceKey: "it-b");
        var companion = seed.Narrator("الصحابي", ibnHajarRank: 1);
        a.BirthYearHijri = 60;          // born after the companion died: a temporal break on the A branch only
        companion.DeathYearHijri = 50;
        var hadith = seed.Hadith("كتاب الاختبار", 1, "متن");
        var toA = seed.Link(hadith, compiler, a, 1, "حدثنا");
        var toB = seed.Link(hadith, compiler, b, 1, "أخبرنا");
        seed.Link(hadith, a, companion, 2, "عن");
        seed.Link(hadith, b, companion, 2, "عن");
        await seed.SaveAsync();

        var nodes = await new HadithChainRepository(db).GetIsnadTreeAsync(hadith.Id);

        nodes.Should().HaveCount(5);
        var root = nodes.Single(n => n.StepOrder == 0);
        root.Id.Should().Be(Guid.Empty);
        root.NarratorId.Should().Be(compiler.Id);

        nodes.Where(n => n.StepOrder == 1).Select(n => (n.NarratorId, n.Id, n.ParentNodeId, n.TransmissionTerm))
            .Should().Equal((a.Id, toA.Id, Guid.Empty, "حدثنا"), (b.Id, toB.Id, Guid.Empty, "أخبرنا"));  // SourceKey order
        nodes.Single(n => n.NarratorId == a.Id).GradeEn.Should().Be("reliable");

        var companions = nodes.Where(n => n.StepOrder == 2).ToList();
        companions.Should().OnlyContain(n => n.NarratorId == companion.Id && n.GradeEn == "companion" && n.TransmissionTerm == "عن");
        companions.Select(n => n.ParentNodeId).Should().BeEquivalentTo(new Guid?[] { toA.Id, toB.Id });
        companions.Single(n => n.ParentNodeId == toA.Id).IsAnomaly.Should().BeTrue();
        companions.Single(n => n.ParentNodeId == toB.Id).IsAnomaly.Should().BeFalse();
    }

    [Fact]
    public async Task IsnadTree_WithTwoCompilers_GivesBothTheZeroId_InRegistryKeyOrder()
    {
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var second = seed.Narrator("المصنف الثاني", sourceKey: "ic-2");
        var first = seed.Narrator("المصنف الأول", sourceKey: "ic-1");
        var sheikh = seed.Narrator("الشيخ");
        var hadith = seed.Hadith("كتاب الاختبار", 2, "متن");
        seed.Link(hadith, second, sheikh, 1);
        seed.Link(hadith, first, sheikh, 1);
        await seed.SaveAsync();

        var nodes = await new HadithChainRepository(db).GetIsnadTreeAsync(hadith.Id);

        nodes.Where(n => n.StepOrder == 0).Select(n => (n.Id, n.NarratorId))
            .Should().Equal((Guid.Empty, first.Id), (Guid.Empty, second.Id));
    }

    [Fact]
    public async Task ComparativeTree_MergesTheSharedNarrators_WithTheirSources()
    {
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var bukhari = seed.Narrator("البخاري");
        var muslim = seed.Narrator("مسلم");
        var sheikh = seed.Narrator("الشيخ المشترك");
        var companion = seed.Narrator("الصحابي", ibnHajarRank: 1);
        var h1 = seed.Hadith("صحيح الاختبار", 10, "متن");
        var h2 = seed.Hadith("سنن الاختبار", 20, "متن");
        seed.Link(h1, bukhari, sheikh, 1);
        seed.Link(h1, sheikh, companion, 2);
        seed.Link(h2, muslim, sheikh, 1, "أخبرنا");
        seed.Link(h2, sheikh, companion, 2);
        var noChain = seed.Hadith("كتاب بلا إسناد", 30, "متن");
        await seed.SaveAsync();

        var repo = new HadithChainRepository(db);
        var nodes = await repo.GetComparativeIsnadTreeAsync([h1.Id, h2.Id, noChain.Id]);

        nodes.Select(n => n.NarratorId).Distinct().Should().BeEquivalentTo(new[] { bukhari.Id, muslim.Id, sheikh.Id, companion.Id });
        var shared = nodes.Where(n => n.NarratorId == sheikh.Id).ToList();
        shared.SelectMany(n => n.SourceHadithIds).Distinct().Should().BeEquivalentTo(new[] { h1.Id, h2.Id });
        shared.SelectMany(n => n.SourceBooks).Distinct().Should().BeEquivalentTo(new[] { "صحيح الاختبار", "سنن الاختبار" });
        nodes.Single(n => n.NarratorId == companion.Id && n.ParentNodeId != null).SourceHadithIds
            .Should().BeEquivalentTo(new[] { h1.Id, h2.Id });

        // One id goes through the same uuid[] parameter.
        (await repo.GetComparativeIsnadTreeAsync([h1.Id])).Select(n => n.NarratorId).Distinct()
            .Should().BeEquivalentTo(new[] { bukhari.Id, sheikh.Id, companion.Id });
    }
}
