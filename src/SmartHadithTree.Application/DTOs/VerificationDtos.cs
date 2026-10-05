namespace SmartHadithTree.Application.DTOs;

/// <summary>Request to check whether a pasted text is a hadith that exists in the corpus.</summary>
public class VerifyHadithRequestDto
{
    public string Text { get; set; } = string.Empty;
}

/// <summary>A corpus hadith that matches the checked text, with how closely it matches.</summary>
public class VerifiedHadithMatchDto
{
    public Guid Id { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string? Chapter { get; set; }
    public string MatnArabic { get; set; } = string.Empty;

    /// <summary>Share (0..1) of the checked text's word pairs found, in order, in this hadith.</summary>
    public double Similarity { get; set; }
}

/// <summary>Outcome of checking a text against the corpus. Only a non-empty <see cref="Matches"/> names a source.</summary>
public class HadithVerificationResultDto
{
    public string Status { get; set; } = HadithVerificationStatus.NotFound;

    /// <summary>"lexical" (text matching only) or "ai+lexical" (the model also reviewed the candidates).</summary>
    public string Method { get; set; } = "lexical";

    public string Explanation { get; set; } = string.Empty;
    public List<VerifiedHadithMatchDto> Matches { get; set; } = [];
}

/// <summary>
/// <c>exact</c>: the text is quoted from the matn of the listed hadith(s).
/// <c>variant</c>: close wording; compare with the listed matn.
/// <c>not-found</c>: nothing in the corpus supports it; the system does not claim it is fabricated.
/// <c>invalid</c>: the input is too short or too long to check.
/// </summary>
public static class HadithVerificationStatus
{
    public const string Exact = "exact";
    public const string Variant = "variant";
    public const string NotFound = "not-found";
    public const string Invalid = "invalid";
}
