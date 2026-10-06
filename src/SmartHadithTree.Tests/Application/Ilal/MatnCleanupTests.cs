using FluentAssertions;
using SmartHadithTree.Application.Services;
using SmartHadithTree.Application.Services.Ilal.Rules;
using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>
/// What is not part of the matn must not reach the comparison: the compiler's notes, the editor's
/// brackets, pointers to another text, and a second isnad pasted into the same record.
/// </summary>
public class MatnCleanupTests
{
    private const string Isnad = "حدثنا عفان قال حدثنا أبان عن يحيى عن أبي مالك الأشعري أن رسول الله ﷺ قال: ";

    [Theory]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان. أخرجه مسلم في الصحيح عن إسحاق بن منصور")]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان. رواه البخاري في الصحيح عن محمد بن كثير")]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان. وكذلك رواه معاذ بن معاذ")]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان [حكم حسين سليم أسد]: إسناده صحيح")]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان [٦٦١]")]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان ب د ع ف م تحفه اتحاف")]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان لم يرو هذا الحديث عن الأوزاعي إلا سلمة")]
    [InlineData("الطهور شطر الإيمان والحمد لله تملأ الميزان تفرد به عثمان عن الدراوردي")]
    public void NotesAndEditorSymbols_AreCutFromTheBody(string matn)
    {
        MatnText.ExtractBody(Isnad + matn).Should().EndWith("تملا الميزان");
    }

    [Theory]
    [InlineData("فذكر مثله إلا أنه قال: الصلاة برهان")]
    [InlineData("وذكر الحديث وقال فيه كذا")]
    [InlineData("فذكر بمثل حديث زكرياء عن الشعبي إلى قوله")]
    public void PointerToAnotherText_IsNotTreatedAsMatn(string pointer)
    {
        MatnText.ExtractBody(Isnad + "الطهور شطر الإيمان " + pointer).Should().EndWith("الطهور شطر الايمان");
    }

    [Fact]
    public void RemarkOnTheHadith_IsCutSoTheShortTextStaysAnAbridgement()
    {
        MatnText.ExtractBody(Isnad + "الدين النصيحة وهذا الحديث صحيح").Should().EndWith("الدين النصيحه");
    }

    [Fact]
    public void SpellingVariantsScatteredThroughTheText_AreNotAContradiction()
    {
        const string a = "قضى النبي أن للجار أن يضع خشبه على جدار جاره وإن كره والطريق الميتاء سبع أذرع ولا ضرر ولا ضرار";
        const string b = "قضى النبي أن للجار أن يضع خشبته على جدار جاره وإن كره والطريق الميتاء سبع أذرع ولا ضرر ولا إضرار";

        var diff = MatnAligner.Align(MatnText.ExtractBody(a), MatnText.ExtractBody(b));

        MatnAtMadarRule.Classify(diff)
            .Should().Be(MatnAtMadarRule.DiffKind.None);
    }

    [Fact]
    public void SecondIsnadPastedIntoTheRecord_IsCut()
    {
        var text = "سمعت النبي ﷺ يقول: الحلال بين والحرام بين. حدثنا علي بن عبد الله حدثنا ابن عيينة عن أبي فروة قال سمعت النعمان";

        MatnText.ExtractBody(text).Should().Be("سمعت النبي يقول الحلال بين والحرام بين");
    }

    [Fact]
    public void CompanionSayingHaddathana_RasulAllah_IsNotAnIsnad()
    {
        var text = "قال عبد الله: حدثنا رسول الله ﷺ وهو الصادق المصدوق: إن أحدكم يجمع خلقه في بطن أمه أربعين يوما";

        MatnText.ExtractBody(text).Should().Contain("يجمع خلقه");
    }

    [Fact]
    public void MawqufText_WithoutAMatnStart_KeepsItsWholeBody()
    {
        // The isnad is still in the body here, so «حدثنا» must not cut it.
        var text = "حدثنا عفان حدثنا أبان عن يحيى عن أبي مالك قال: الطهور شطر الإيمان والحمد لله تملأ الميزان";

        MatnText.ExtractBody(text).Should().Contain("تملا الميزان");
    }
}
