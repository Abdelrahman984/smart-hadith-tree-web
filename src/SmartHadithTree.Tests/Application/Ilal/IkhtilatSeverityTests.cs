using FluentAssertions;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>The ikhtilat rule weighs the books' severity, the student's timing and what the books say about the group.</summary>
public class IkhtilatSeverityTests
{
    private static IlalTestBuilder Fixture(IkhtilatSeverity? severity, bool noHearingAfter = false, string book = "مسند أحمد", int? rank = null)
    {
        var b = new IlalTestBuilder().Narrator("أحمد").Narrator("جرير").Narrator("أبوه")
            .Narrator("ابن عيينة", mukhtalit: true, severity: severity, noHearingAfter: noHearingAfter, rank: rank);
        b.Chain("x", book, "أحمد", "حدثنا", "جرير", "عن", "ابن عيينة", "عن", "أبوه");
        return b;
    }

    [Theory]
    [InlineData(IkhtilatSeverity.Harmful, IllahSeverity.Qadihah)]
    [InlineData(IkhtilatSeverity.Disputed, IllahSeverity.GhayrQadihah)]
    [InlineData(IkhtilatSeverity.Light, IllahSeverity.Tanbih)]
    public void HeardAfter_IsWeighedBySeverity(IkhtilatSeverity severity, IllahSeverity expected)
    {
        var b = Fixture(severity).Hearing("ابن عيينة", "جرير", HearingTiming.After);

        new IkhtilatRule().Evaluate(b.Build()).Single().Severity.Should().Be(expected);
    }

    [Theory]
    [InlineData(IkhtilatSeverity.Harmful, 2, 0.45)]     // harmful: a note whatever the grade
    [InlineData(IkhtilatSeverity.Harmful, 5, 0.45)]
    [InlineData(IkhtilatSeverity.Disputed, 5, 0.3)]     // disputed: only for صدوق يهم or weaker
    [InlineData(IkhtilatSeverity.Disputed, 8, 0.3)]
    public void UnknownTiming_IsANote_ForHarmful_AndForDisputedOfAWeakerNarrator(IkhtilatSeverity severity, int rank, double confidence)
    {
        var finding = new IkhtilatRule().Evaluate(Fixture(severity, rank: rank).Build()).Single();

        finding.Severity.Should().Be(IllahSeverity.Tanbih);
        finding.Confidence.Should().Be(confidence);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void UnknownTiming_OfADisputedIkhtilat_OfAThiqahOrSaduq_IsNotFlagged(int rank) =>
        new IkhtilatRule().Evaluate(Fixture(IkhtilatSeverity.Disputed, rank: rank).Build()).Should().BeEmpty();

    [Fact]
    public void UnknownTiming_OfALightIkhtilat_IsNotFlagged() =>
        new IkhtilatRule().Evaluate(Fixture(IkhtilatSeverity.Light).Build()).Should().BeEmpty();

    [Fact]
    public void UnknownTiming_WhenTheBooksSayNobodyHeardAfter_IsNotFlagged() =>
        new IkhtilatRule().Evaluate(Fixture(IkhtilatSeverity.Harmful, noHearingAfter: true).Build()).Should().BeEmpty();

    [Fact]
    public void WithoutASeverity_TheNarratorIsTreatedAsHarmful_AsBefore() =>
        new IkhtilatRule().Evaluate(Fixture(null).Hearing("ابن عيينة", "جرير", HearingTiming.After).Build())
            .Single().Severity.Should().Be(IllahSeverity.Qadihah);

    [Theory]
    [InlineData(HearingTiming.Both)]
    [InlineData(HearingTiming.Conflict)]
    public void BothOrConflicting_IsANote_UnlessLightOrInTheSahihs(HearingTiming timing)
    {
        new IkhtilatRule().Evaluate(Fixture(IkhtilatSeverity.Harmful).Hearing("ابن عيينة", "جرير", timing).Build())
            .Single().Severity.Should().Be(IllahSeverity.Tanbih);
        new IkhtilatRule().Evaluate(Fixture(IkhtilatSeverity.Light).Hearing("ابن عيينة", "جرير", timing).Build()).Should().BeEmpty();
        new IkhtilatRule().Evaluate(Fixture(IkhtilatSeverity.Harmful, book: "صحيح البخاري").Hearing("ابن عيينة", "جرير", timing).Build())
            .Should().BeEmpty();
    }

    [Fact]
    public void TheCriticsWordsAndTheGroupRule_AreQuotedInTheEvidence()
    {
        var after = Fixture(IkhtilatSeverity.Harmful)
            .Hearing("ابن عيينة", "جرير", HearingTiming.After, "سمع منه بعد ما اختلط (الكواكب النيرات)");
        new IkhtilatRule().Evaluate(after.Build()).Single().EvidenceAr.Should().Contain("سمع منه بعد ما اختلط");

        var unknown = Fixture(IkhtilatSeverity.Harmful)
            .GroupRule("ابن عيينة", "من سمع منه قبل التغير", HearingTiming.Before, "من سمع منه قبل التغير فروايته صحيحة");
        new IkhtilatRule().Evaluate(unknown.Build()).Single().EvidenceAr.Should().Contain("من سمع منه قبل التغير فروايته صحيحة");
    }

    [Fact]
    public void GroupRulesGoingBothWays_QuoteTheFirstBeforeAndTheFirstAfterRule_InTheBooksOrder()
    {
        // شريك النخعي in ilal.json: two «before» and two «after» rules, interleaved.
        var b = Fixture(IkhtilatSeverity.Harmful)
            .GroupRule("ابن عيينة", "الذين سمعوا منه بواسط", HearingTiming.Before, "ليس فيهم تخليط")
            .GroupRule("ابن عيينة", "المتأخرون عنه بالكوفة", HearingTiming.After, "فيه أوهام")
            .GroupRule("ابن عيينة", "من سمع منه قديما", HearingTiming.Before, "فحديثه صحيح")
            .GroupRule("ابن عيينة", "من سمع منه بعدما ولي القضاء", HearingTiming.After, "ففي سماعه بعض الاختلاط");

        var evidence = new IkhtilatRule().Evaluate(b.Build()).Single().EvidenceAr;

        evidence.Should().Contain("(الذين سمعوا منه بواسط) أن سماعهم قبل الاختلاط: «ليس فيهم تخليط»")
            .And.Contain("وفي (المتأخرون عنه بالكوفة) أن سماعهم بعده: «فيه أوهام»")
            .And.NotContain("من سمع منه قديما").And.NotContain("ولي القضاء");
    }

    [Fact]
    public void OnlyAfterRules_AreQuotedAsAfter()
    {
        var b = Fixture(IkhtilatSeverity.Harmful)
            .GroupRule("ابن عيينة", "من سمع منه سنة سبع وتسعين", HearingTiming.After, "فسماعه لا شيء");

        new IkhtilatRule().Evaluate(b.Build()).Single().EvidenceAr
            .Should().Contain("(من سمع منه سنة سبع وتسعين) أن سماعهم بعد الاختلاط: «فسماعه لا شيء»");
    }

    [Fact]
    public void RulesThatStateNoTiming_AreNotQuotedAsBefore()
    {
        // «other» in ilal.json (حفص بن غياث: who wrote from his book); it was quoted as «قبل الاختلاط» before.
        var b = Fixture(IkhtilatSeverity.Harmful)
            .GroupRule("ابن عيينة", "من كتب عنه من كتابه", HearingTiming.Unknown, "فمن كتب عنه من كتابه فهو صالح");

        var evidence = new IkhtilatRule().Evaluate(b.Build()).Single().EvidenceAr;

        evidence.Should().NotContain("من كتب عنه من كتابه").And.NotContain("أن سماعهم");
    }
}
