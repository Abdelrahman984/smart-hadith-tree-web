using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>
/// Shared mapping from Itqan grades (grade_en) and tabaqat strings to the numeric
/// T1–T12 scale used by both the Taqwiyah and Ilal engines (lower = stronger).
/// </summary>
public static class NarratorGradeScale
{
    /// <summary>Default tier for missing or unrecognized grades (ضعيف / مجهول).</summary>
    public const int DefaultTier = 7;

    /// <summary>Maps an Itqan grade_en to the T1–T12 tier number.</summary>
    public static int ToTier(string? gradeEn)
    {
        if (string.IsNullOrEmpty(gradeEn)) return DefaultTier;
        var g = gradeEn.ToLowerInvariant();
        if (g.Contains("companion")) return 1;
        if (g.Contains("reliable") && g.Contains("mostly")) return 4;
        if (g.Contains("reliable")) return 3;
        if (g.Contains("weak")) return 7;
        if (g.Contains("abandoned")) return 9;
        if (g.Contains("fabricator")) return 12;
        return DefaultTier;
    }

    /// <summary>Arabic label for an Itqan grade, for use in explanations.</summary>
    public static string ToArabicLabel(string? gradeEn)
    {
        if (string.IsNullOrEmpty(gradeEn)) return "غير محرر";
        var g = gradeEn.ToLowerInvariant();
        if (g.Contains("companion")) return "صحابي";
        if (g.Contains("reliable") && g.Contains("mostly")) return "صدوق";
        if (g.Contains("reliable")) return "ثقة";
        if (g.Contains("weak")) return "ضعيف";
        if (g.Contains("abandoned")) return "متروك";
        if (g.Contains("fabricator")) return "كذاب";
        if (g.Contains("unknown")) return "مجهول";
        return "غير محرر";
    }

    /// <summary>True when the narrator is a Companion (صحابي).</summary>
    public static bool IsCompanion(string? gradeEn, string? generationTier)
    {
        if (!string.IsNullOrEmpty(gradeEn) && gradeEn.Contains("companion", StringComparison.OrdinalIgnoreCase))
            return true;
        return MatnText.NormalizeForComparison(generationTier).Contains("صحاب");
    }

    /// <summary>
    /// True when the narrator is known to be a Successor (تابعي), false when known not to be,
    /// and null when the generation cannot be determined from the data.
    /// </summary>
    public static bool? IsTabii(string? gradeEn, string? generationTier)
    {
        if (IsCompanion(gradeEn, generationTier)) return false;

        var tier = MatnText.NormalizeForComparison(generationTier);
        if (string.IsNullOrEmpty(tier)) return null;

        if (tier.Contains("اتباع") || tier.Contains("تابعي التابعين") || tier.Contains("تبع"))
            return false;
        if (tier.Contains("تابع"))
            return true;

        // Ibn Hajr's tabaqat in Taqrib al-Tahdhib: 2–5 are Successors, 6+ are later.
        string[] tabiiOrdinals = ["الثانيه", "الثالثه", "الرابعه", "الخامسه"];
        string[] laterOrdinals = ["السادسه", "السابعه", "الثامنه", "التاسعه", "العاشره", "الحاديه عشره", "الثانيه عشره"];
        if (laterOrdinals.Any(tier.Contains)) return false;
        if (tabiiOrdinals.Any(tier.Contains)) return true;

        var digits = new string(tier.Where(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var n))
            return n is >= 2 and <= 5;

        return null;
    }
}
