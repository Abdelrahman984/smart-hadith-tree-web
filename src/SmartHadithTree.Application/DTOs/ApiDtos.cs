namespace SmartHadithTree.Application.DTOs;

public class HadithSearchResultDto
{
    public Guid Id { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string? Chapter { get; set; }
    public string MatnSnippet { get; set; } = string.Empty;
}

public class IsnadNodeDto
{
    public Guid Id { get; set; } // The Transmission Id or unique node id
    public Guid NarratorId { get; set; }
    public string NarratorName { get; set; } = string.Empty;
    public string? KnownAs { get; set; }
    public string? GenerationTier { get; set; }
    public int StepOrder { get; set; } // 1 = Compiler (Bukhari), higher = earlier (Sahabi)
    public Guid? ParentNodeId { get; set; } // Points to the student (who received it from this sheikh)
    public string? TransmissionTerm { get; set; } // حدثنا, عن
    public string? GradeEn { get; set; }
    
    public bool IsAnomaly { get; set; }
    public string? AnomalyReason { get; set; }
}

public class IsnadTreeResponseDto
{
    public Guid HadithId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string MatnArabic { get; set; } = string.Empty;
    public List<IsnadNodeDto> Nodes { get; set; } = [];
}

public class NarratorSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? GenerationTier { get; set; }
    public string GradeSummary { get; set; } = string.Empty; // e.g. "ثقة", "ضعيف"
    public string? GradeEn { get; set; }
}

public class NarratorDetailDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? KnownAs { get; set; }
    public string? Kunyah { get; set; }
    public string? GenerationTier { get; set; }
    public int? BirthYearHijri { get; set; }
    public int? DeathYearHijri { get; set; }
    public string? Biography { get; set; }
    public string? GradeEn { get; set; }
    public List<ScholarEvaluationDto> Evaluations { get; set; } = [];
}

public class ScholarEvaluationDto
{
    public string ScholarName { get; set; } = string.Empty;
    public string EvaluationText { get; set; } = string.Empty;
    public string? SourceBook { get; set; }
    public string? VerdictRating { get; set; }
}

/// <summary>
/// Represents a source Hadith in the comparative (Takhreej) view.
/// </summary>
public class ComparativeHadithSourceDto
{
    public Guid HadithId { get; set; }
    public string BookName { get; set; } = string.Empty;
    public int HadithNumber { get; set; }
    public string MatnSnippet { get; set; } = string.Empty;
}

/// <summary>
/// Extended node DTO that tracks which books/sources a narrator appears in
/// across multiple merged Isnad chains.
/// </summary>
public class ComparativeIsnadNodeDto : IsnadNodeDto
{
    /// <summary>Which source Hadith(s) this transmission belongs to.</summary>
    public List<Guid> SourceHadithIds { get; set; } = [];

    /// <summary>Which book(s) this narrator appears in for this cluster.</summary>
    public List<string> SourceBooks { get; set; } = [];
}

/// <summary>
/// The unified tree response wrapping multiple sources into a single merged DAG.
/// </summary>
public class ComparativeTreeResponseDto
{
    public List<ComparativeHadithSourceDto> Sources { get; set; } = [];
    public List<ComparativeIsnadNodeDto> Nodes { get; set; } = [];
}
