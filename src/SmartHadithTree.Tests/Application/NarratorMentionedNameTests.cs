using FluentAssertions;
using SmartHadithTree.Application.Services;

namespace SmartHadithTree.Tests.Application;

public class NarratorMentionedNameTests
{
    private const string Sufyan = "سفيان بن عيينة بن أبي عمران الهلالي";

    [Fact]
    public void Choose_PrefersTheFormTheIsnadsUseMost()
    {
        var texts = new[]
        {
            "حدثنا الحميدي قال حدثنا سفيان قال حدثنا الزهري",
            "حدثنا قتيبة حدثنا سفيان عن الزهري",
            "حدثنا مسدد حدثنا ابن عيينة عن الزهري",
        };

        NarratorMentionedName.Choose(Sufyan, null, texts).Should().Be("سفيان");
    }

    [Fact]
    public void Choose_BareFirstNameFollowedByPatronymic_CountsForTheLongerForm()
    {
        var texts = new[] { "حدثنا محمد بن يحيى قال حدثنا محمد بن يحيى", "حدثنا محمد بن بشار" };

        NarratorMentionedName.Choose("محمد بن يحيى بن عبد الله الذهلي", null, texts).Should().Be("محمد بن يحيى");
    }

    [Fact]
    public void Choose_UsesKnownAsAndIgnoresDiacriticsAndCaseEndings()
    {
        var texts = new[] { "عَنِ الزُّهْرِيِّ، عَنْ أَبِي هُرَيْرَةَ" };

        NarratorMentionedName.Choose("محمد بن مسلم بن عبيد الله القرشي", "الزهري", texts).Should().Be("الزهري");
        NarratorMentionedName.Choose("عبد الرحمن بن صخر الدوسي", "أبو هريرة", texts).Should().Be("أبو هريرة");
    }

    [Fact]
    public void Choose_NobodyNamesHim_ReturnsNull()
    {
        NarratorMentionedName.Choose(Sufyan, null, ["حدثنا مالك عن نافع"]).Should().BeNull();
        NarratorMentionedName.Choose(Sufyan, null, [null, " "]).Should().BeNull();
    }

    [Fact]
    public void Choose_UsesKunyah_WhenFullNameDoesNotCarryIt()
    {
        NarratorMentionedName.Choose("عبد الرحمن بن صخر الدوسي", null, ["عن أبي هريرة قال"], "أبو هريرة")
            .Should().Be("أبو هريرة");
    }

    [Fact]
    public void Choose_TreatsSpacedAndJoinedAbdAsTheSameName()
    {
        NarratorMentionedName.Choose("عبد الله بن عمر بن الخطاب", null, ["عن عبدالله بن عمر عن النبي"])
            .Should().Be("عبد الله بن عمر");
    }
}
