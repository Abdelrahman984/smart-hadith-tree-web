using SmartHadithTree.Domain.Utilities;

namespace SmartHadithTree.Application.Services.Ilal;

/// <summary>Classification of transmission formulas (صيغ الأداء).</summary>
public static class TransmissionTerms
{
    // Normalized forms (no hamza on alef, ta marbuta → ha).
    private static readonly string[] ExplicitHearing =
        ["حدثنا", "حدثني", "اخبرنا", "اخبرني", "انبانا", "انباني", "سمعت", "سمع", "انه سمع", "انها سمعت", "قرات علي", "قرات على"];

    private static readonly string[] Ambiguous = ["عن", "ان", "قال", "ذكر"];

    /// <summary>True for formulas that explicitly state hearing (تصريح بالسماع), e.g. حدثنا / سمعت.</summary>
    public static bool IsExplicitHearing(string? term)
    {
        var t = MatnText.NormalizeForComparison(term);
        return t.Length > 0 && ExplicitHearing.Contains(t);
    }

    /// <summary>True for formulas that do not prove hearing (عن / أن / قال), which a mudallis can exploit.</summary>
    public static bool IsAmbiguous(string? term)
    {
        var t = MatnText.NormalizeForComparison(term);
        return t.Length > 0 && Ambiguous.Contains(t);
    }

    /// <summary>True when the book is one of the two Sahihs, whose mudallis 'an'ana is treated as connected.</summary>
    public static bool IsSahihayn(string? bookName)
    {
        var b = MatnText.NormalizeForComparison(bookName);
        return b.Contains("صحيح البخاري") || b.Contains("صحيح مسلم");
    }
}
