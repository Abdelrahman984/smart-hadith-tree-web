using FluentAssertions;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Tests.Application.Ilal;

public class MatnAlignerTests
{
    [Fact]
    public void Align_IdenticalTexts_HasFullSimilarityAndNoDifferences()
    {
        var result = MatnAligner.Align("إنما الأعمال بالنيات", "انما الاعمال بالنيات");

        result.Similarity.Should().Be(1.0);
        result.AddedCount.Should().Be(0);
        result.RemovedCount.Should().Be(0);
    }

    [Fact]
    public void Align_DetectsAddedRun()
    {
        var result = MatnAligner.Align(
            "انما الاعمال بالنيات وانما لكل امرئ ما نوى",
            "انما الاعمال بالنيات وانما لكل امرئ ما نوى فمن كانت هجرته الى الله");

        result.AddedCount.Should().Be(5);
        result.RemovedCount.Should().Be(0);
        result.Segments.Last().Kind.Should().Be(AlignmentKind.Added);
        result.Segments.Last().Text.Should().Be("فمن كانت هجرته الي الله");
    }

    [Fact]
    public void Align_DetectsSubstitution()
    {
        var result = MatnAligner.Align("صلاة الجماعة تفضل صلاة الفذ بسبع وعشرين درجة",
                                       "صلاة الجماعة تفضل صلاة الفذ بخمس وعشرين جزءا");

        result.SubstitutedCount.Should().Be(2);
    }

    [Fact]
    public void Align_IgnoresDiacriticsAndHonorifics()
    {
        var result = MatnAligner.Align("قَالَ رَسُولُ اللَّهِ صَلَّى اللَّهُ عَلَيْهِ وَسَلَّمَ الدِّينُ النَّصِيحَةُ",
                                       "قال رسول الله ﷺ: الدين النصيحة.");

        result.Similarity.Should().Be(1.0);
    }

    [Fact]
    public void ExtractBody_StripsIsnadAndTrailingCommentary()
    {
        var body = MatnText.ExtractBody(
            "حدثنا قتيبة حدثنا الليث عن نافع عن ابن عمر أن رسول الله صلى الله عليه وسلم قال لا يبع بعضكم على بيع بعض قال أبو عيسى هذا حديث حسن صحيح");

        body.Should().Be("ان رسول الله قال لا يبع بعضكم علي بيع بعض");
    }

    [Fact]
    public void ExtractBody_MawqufText_SkipsPastIsnad()
    {
        var body = MatnText.ExtractBody("حدثنا وكيع عن سفيان عن منصور عن إبراهيم قال كانوا يكرهون ذلك");

        body.Should().Be("قال كانوا يكرهون ذلك");
    }

    [Theory]
    [InlineData("عن أبي هريرة قال قال رسول الله صلى الله عليه وسلم", true)]
    [InlineData("عن النبي صلى الله عليه وسلم قال", true)]
    [InlineData("عن ابن عمر قال من السنة كذا", false)]
    public void IsMarfu_DetectsAttributionToTheProphet(string text, bool expected)
    {
        MatnText.IsMarfu(text).Should().Be(expected);
    }
}
