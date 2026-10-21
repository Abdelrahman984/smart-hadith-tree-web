using FluentAssertions;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Domain.Entities;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Tests.Integration;

/// <summary><see cref="IlalAnalysisService"/> loading its context from PostgreSQL (orders through navigations).</summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class IlalLoadingTests(PostgresFixture pg)
{
    [Fact]
    public async Task GroupRules_AreQuotedInTheBooksOrder_NotInInsertOrder()
    {
        await using var db = pg.CreateContext();
        var seed = new ChainSeed(db);
        var compiler = seed.Narrator("أحمد بن حنبل");
        var student = seed.Narrator("جرير بن حازم");
        var mukhtalit = seed.Narrator("سعيد بن أبي عروبة", ibnHajarRank: 6);
        mukhtalit.HasMukhtalit = true;
        mukhtalit.IkhtilatSeverity = IkhtilatSeverity.Harmful;
        var companion = seed.Narrator("أنس بن مالك", ibnHajarRank: 1);
        var hadith = seed.Hadith("مسند أحمد", 900001, "متن");
        seed.Link(hadith, compiler, student, 1);
        seed.Link(hadith, student, mukhtalit, 2, "عن");
        seed.Link(hadith, mukhtalit, companion, 3, "عن");
        await seed.SaveAsync();
        // Saved one at a time, last first (one SaveChanges would insert in key order, i.e. randomly): the table then
        // returns them last-first, and only ORDER BY "Ordinal" puts the books' first rules first.
        foreach (var (ordinal, timing, group) in new[]
                 {
                     (3, HearingTiming.After, "من سمع منه بآخرة"),
                     (2, HearingTiming.Before, "من سمع منه بالبصرة"),
                     (1, HearingTiming.After, "من سمع منه بعد الهزيمة"),
                     (0, HearingTiming.Before, "من سمع منه قبل الهزيمة")
                 })
        {
            db.MukhtalitGroupRules.Add(new MukhtalitGroupRule
            {
                Id = Guid.NewGuid(), MukhtalitId = mukhtalit.Id, GroupAr = group, Timing = timing,
                Quote = $"قول في {group}", Ordinal = ordinal
            });
            await seed.SaveAsync();
        }

        var report = await new IlalAnalysisService(db).AnalyzeAsync([hadith.Id]);

        report.Turuq.Should().ContainSingle();
        var evidence = report.Findings.Single(f => f.Type == IllahType.Ikhtilat).EvidenceAr;
        evidence.Should().Contain("(من سمع منه قبل الهزيمة) أن سماعهم قبل الاختلاط")
            .And.Contain("وفي (من سمع منه بعد الهزيمة) أن سماعهم بعده")
            .And.NotContain("بالبصرة").And.NotContain("بآخرة");
    }
}
