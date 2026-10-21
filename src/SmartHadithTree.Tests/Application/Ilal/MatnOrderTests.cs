using FluentAssertions;
using SmartHadithTree.Application.DTOs;
using SmartHadithTree.Application.Services.Ilal;
using SmartHadithTree.Application.Services.Ilal.Rules;

namespace SmartHadithTree.Tests.Application.Ilal;

/// <summary>The same turuq give the same matn findings whatever order they come in.</summary>
public class MatnOrderTests
{
    // مسند أحمد #2, مسند أبي يعلى #12 and سنن ابن ماجه #1395 (two chains, tahwil), all from Waki', as stored in v7.
    private const string Ahmad = "حَدَّثَنَا وَكِيعٌ، قَالَ حَدَّثَنَا مِسْعَرٌ وَسُفْيَانُ، عَنْ عُثْمَانَ بْنِ الْمُغِيرَةِ الثَّقَفِيِّ، عَنْ عَلِيِّ بْنِ رَبِيعَةَ الْوَالِبِيِّ، عَنْ أَسْمَاءَ بْنِ الْحَكَمِ الْفَزَارِيِّ عَنْ عَلِيٍّ ﵁، قَالَ: كُنْتُ إِذَا سَمِعْتُ مِنْ رَسُولِ اللهِ ﷺ حَدِيثًا نَفَعَنِي اللهُ بِمَا شَاءَ مِنْهُ، وَإِذَا حَدَّثَنِي عَنْهُ غَيْرِي اسْتَحْلَفْتُهُ، فَإِذَا حَلَفَ لِي صَدَّقْتُهُ، وَإِنَّ أَبَا بَكْرٍ ﵁ حَدَّثَنِي - وَصَدَقَ أَبُو بَكْرٍ - أَنَّهُ سَمِعَ النَّبِيَّ ﷺ، قَالَ: \" مَا مِنْ رَجُلٍ يُذْنِبُ ذَنْبًا فَيَتَوَضَّأُ فَيُحْسِنُ الْوُضُوءَ، قَالَ مِسْعَرٌ: وَيُصَلِّي، وَقَالَ سُفْيَانُ: ثُمَّ يُصَلِّي رَكْعَتَيْنِ، فَيَسْتَغْفِرُ اللهَ ﷿ إِلَّا غُفَرَ لَهُ \".";
    private const string AbuYala = "حَدَّثَنَا أَبُو خَيْثَمَةَ، حَدَّثَنَا وَكِيعُ بْنُ الْجَرَّاحِ، حَدَّثَنَا مِسْعَرٌ، وَسُفْيَانُ، عَنْ عُثْمَانَ بْنِ الْمُغِيرَةِ الثَّقَفِيِّ، عَنْ عَلِيِّ بْنِ رَبِيعَةَ الْوَالِبِيِّ، عَنْ أَسْمَاءِ بْنِ الْحَكَمِ الْفَزَارِيِّ، عَنْ عَلِيٍّ، قَالَ: كُنْتُ إِذَا سَمِعْتُ مِنْ رَسُولِ اللَّهِ ﷺ حَدِيثًا نَفَعَنِي اللَّهُ بِمَا شَاءَ مِنْهُ، وَإِذَا حَدَّثَنِي عَنْهُ غَيْرُهُ اسْتَحْلَفْتُهُ، فَإِذَا حَلَفَ لِي صَدَّقْتُهُ، وَإِنَّ أَبَا بَكْرٍ، حَدَّثَنِي، وَصَدَقَ أَبُو بَكْرٍ، أَنَّهُ ﷺ قَالَ: \" مَا مِنْ أَحَدٍ يُذْنِبُ ذَنْبًا فَيَتَوَضَّأُ فَيُحْسِنُ الْوُضُوءَ، قَالَ مِسْعَرٌ: ثُمَّ يُصَلِّي، قَالَ سُفْيَانُ: يُصَلِّي رَكْعَتَيْنِ، ثُمَّ يَسْتَغْفِرُ إِلَّا غُفِرَ لَهُ \"\n[حكم حسين سليم أسد] : إسناده صحيح";
    private const string IbnMajah = "حَدَّثَنَا أَبُو بَكْرِ بْنُ أَبِي شَيْبَةَ، وَنَصْرُ بْنُ عَلِيٍّ قَالَا: حَدَّثَنَا وَكِيعٌ، حَدَّثَنَا مِسْعَرٌ، وَسُفْيَانُ، عَنْ عُثْمَانَ بْنِ الْمُغِيرَةِ الثَّقَفِيِّ، عَنْ عَلِيِّ بْنِ رَبِيعَةَ الْوَالِبِيِّ، عَنْ أَسْمَاءَ بْنِ الْحَكَمِ الْفَزَارِيِّ\nعَنْ عَلِيِّ بْنِ أَبِي طَالِبٍ، قَالَ: كُنْتُ إِذَا سَمِعْتُ مِنْ رَسُولِ اللَّهِ ﷺ حَدِيثًا، يَنْفَعُنِي اللَّهُ بِمَا شَاءَ مِنْهُ، وَإِذَا حَدَّثَنِي عَنْهُ غَيْرُهُ اسْتَحْلَفْتُهُ، فَإِذَا حَلَفَ صَدَّقْتُهُ، وَإِنَّ أَبَا بَكْرٍ حَدَّثَنِي وَصَدَقَ أَبُو بَكْرٍ، قَالَ: قَالَ رَسُولُ اللَّهِ ﷺ: \"مَا مِنْ رَجُلٍ يُذْنِبُ ذَنْبًا، فَيَتَوَضَّأُ فَيُحْسِنُ الْوُضُوءَ، ثُمَّ يُصَلِّي رَكْعَتَيْنِ -وَقَالَ مِسْعَرٌ: ثُمَّ يُصَلِّي- فيَسْتَغْفِرُ اللَّهَ، إِلَّا غَفَرَ اللَّهُ لَهُ\".";

    private static IlalTestBuilder Narrators() => new IlalTestBuilder()
        .Narrator("أحمد").Narrator("أبو يعلى").Narrator("زهير").Narrator("ابن ماجه").Narrator("ابن أبي شيبة").Narrator("نصر")
        .Narrator("وكيع").Narrator("عثمان").Narrator("علي", "companion");

    [Fact]
    public void SameTuruqInAnyOrder_GiveTheSameMatnFindings()
    {
        var b = Narrators();
        b.Chain(Ahmad, "مسند أحمد", "أحمد", "حدثنا", "وكيع", "عن", "عثمان", "عن", "علي");
        b.Chain(AbuYala, "مسند أبي يعلى الموصلي", "أبو يعلى", "حدثنا", "زهير", "حدثنا", "وكيع", "عن", "عثمان", "عن", "علي");
        b.Chain(IbnMajah, "سنن ابن ماجه", "ابن ماجه", "حدثنا", "ابن أبي شيبة", "حدثنا", "وكيع", "عن", "عثمان", "عن", "علي");
        b.Chain(IbnMajah, "سنن ابن ماجه", "ابن ماجه", "حدثنا", "نصر", "حدثنا", "وكيع", "عن", "عثمان", "عن", "علي");
        var context = b.Build();

        // The two chains of the tahwil record are one hadith.
        var chains = context.Chains.ToList();
        chains[3] = new IlalChain
        {
            HadithId = chains[2].HadithId, BookName = chains[3].BookName, HadithNumber = chains[2].HadithNumber,
            MatnArabic = chains[3].MatnArabic, Links = chains[3].Links
        };

        var results = Permutations(chains)
            .Select(order => Signature(new MatnAtMadarRule().Evaluate(WithChains(context, order))))
            .ToList();

        results.Should().NotBeEmpty();
        results[0].Should().NotBeEmpty("Ahmad's text differs from the other three");
        results.Should().AllBeEquivalentTo(results[0]);
    }

    [Fact]
    public void ABranchCarriedByTwoBooks_IsComparedByTheSameTextInAnyOrder()
    {
        const string full = "عن أبي هريرة أن رسول الله ﷺ قال من صام رمضان إيمانا واحتسابا غفر له ما تقدم من ذنبه";
        const string added = full + " وما تأخر من ذنبه كله";
        var b = new IlalTestBuilder()
            .Narrator("مالك").Narrator("سفيان").Narrator("الزهري").Narrator("أبو هريرة", "companion")
            .Narrator("البخاري").Narrator("مسلم").Narrator("النسائي");
        b.Chain(full, "صحيح البخاري", "البخاري", "حدثنا", "مالك", "عن", "الزهري", "عن", "أبو هريرة");
        // One student (Sufyan) in two books, one with the addition and one without.
        b.Chain(added, "سنن النسائي", "النسائي", "حدثنا", "سفيان", "عن", "الزهري", "عن", "أبو هريرة");
        b.Chain(full, "صحيح مسلم", "مسلم", "حدثنا", "سفيان", "عن", "الزهري", "عن", "أبو هريرة");
        var context = b.Build();

        var results = Permutations(context.Chains.ToList())
            .Select(order => Signature(new MatnAtMadarRule().Evaluate(WithChains(context, order))))
            .ToList();

        results.Should().AllBeEquivalentTo(results[0]);
    }

    private static IlalContext WithChains(IlalContext c, IReadOnlyList<IlalChain> chains) => new()
    {
        Chains = chains,
        Narrators = c.Narrators,
        Relations = c.Relations,
        NarratorsWithRelations = c.NarratorsWithRelations,
        Hearings = c.Hearings,
        HearingEvidence = c.HearingEvidence,
        GroupRules = c.GroupRules
    };

    /// <summary>Everything a reader sees in a finding, with the id lists as sets.</summary>
    private static List<string> Signature(IEnumerable<IlalFindingDto> findings) => findings
        .Select(f => string.Join(" | ", f.Type, f.Severity, f.Confidence, f.TitleAr, f.EvidenceAr,
            string.Join(",", f.HadithIds.Order()), string.Join(",", f.NarratorIds.Order()),
            f.MatnComparison?.ReferenceHadithId, f.MatnComparison?.ComparedHadithId, f.MatnComparison?.Similarity,
            string.Join("/", f.MatnComparison?.Segments.Select(s => $"{s.Kind}:{s.Text}") ?? [])))
        .Order(StringComparer.Ordinal)
        .ToList();

    private static IEnumerable<List<T>> Permutations<T>(List<T> items)
    {
        if (items.Count <= 1)
        {
            yield return [.. items];
            yield break;
        }
        for (var i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, j) => j != i).ToList();
            foreach (var tail in Permutations(rest))
                yield return [items[i], .. tail];
        }
    }
}
