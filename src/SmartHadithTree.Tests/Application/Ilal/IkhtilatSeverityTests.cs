using FluentAssertions;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Enums;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>The ikhtilat rule weighs the books' severity, the student's timing and what the books say about the group.</summary>
public class IkhtilatSeverityTests
{
    private static IlalTestBuilder Fixture(IkhtilatSeverity? severity, bool noHearingAfter = false, string book = "مسند أحمد")
    {
        var b = new IlalTestBuilder().Narrator("أحمد").Narrator("جرير").Narrator("أبوه")
            .Narrator("ابن عيينة", mukhtalit: true, severity: severity, noHearingAfter: noHearingAfter);
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
    [InlineData(IkhtilatSeverity.Harmful, 0.45)]
    [InlineData(IkhtilatSeverity.Disputed, 0.3)]
    public void UnknownTiming_IsANote_OnlyForHarmfulOrDisputed(IkhtilatSeverity severity, double confidence)
    {
        var finding = new IkhtilatRule().Evaluate(Fixture(severity).Build()).Single();

        finding.Severity.Should().Be(IllahSeverity.Tanbih);
        finding.Confidence.Should().Be(confidence);
    }

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
}
