namespace SmartHadithTree.Domain.Entities;

/// <summary>
/// Represents a Hadith narrator (الراوي) in the chain of transmission.
/// </summary>
public class Narrator
{
    public Guid Id { get; set; }

    /// <summary>الاسم الكامل — Full name with nasab (lineage).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>الشهرة / اللقب — The name the narrator is commonly known by.</summary>
    public string? KnownAs { get; set; }

    /// <summary>الكنية — Honorific (e.g., أبو هريرة).</summary>
    public string? Kunyah { get; set; }

    /// <summary>الطبقة — Generation tier (صحابي، تابعي، تابع التابعين...).</summary>
    public string? GenerationTier { get; set; }

    /// <summary>سنة الولادة بالهجري.</summary>
    public int? BirthYearHijri { get; set; }

    /// <summary>سنة الوفاة بالهجري.</summary>
    public int? DeathYearHijri { get; set; }

    /// <summary>مكان الولادة.</summary>
    public string? BirthPlace { get; set; }

    /// <summary>مكان الوفاة.</summary>
    public string? DeathPlace { get; set; }

    /// <summary>ترجمة الراوي — Biographical notes.</summary>
    public string? Biography { get; set; }

    // ── Navigation Properties ──────────────────────────────────────

    /// <summary>Transmissions where this narrator is the sheikh (teacher).</summary>
    public ICollection<Transmission> TransmissionsAsSheikh { get; set; } = [];

    /// <summary>Transmissions where this narrator is the student (receiver).</summary>
    public ICollection<Transmission> TransmissionsAsStudent { get; set; } = [];

    /// <summary>
    /// ID from the R3GENESI5/Itqan dataset (for mapping).
    /// </summary>
    public int? ItqanId { get; set; }

    public int? GawamiId { get; set; }
    public bool IsMudallis { get; set; }
    public bool HasMukhtalit { get; set; }

    /// <summary>
    /// Standardized English grade from Itqan (reliable, weak, etc.) for UI color mapping.
    /// </summary>
    public string? ItqanGrade { get; set; }

    /// <summary>
    /// classical evaluation records
    /// </summary>
    public ICollection<ScholarEvaluation> ScholarEvaluations { get; set; } = new List<ScholarEvaluation>();
}
