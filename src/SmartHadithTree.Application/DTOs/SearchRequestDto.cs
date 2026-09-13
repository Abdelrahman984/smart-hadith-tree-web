namespace SmartHadithTree.Application.DTOs;

public enum SearchScope
{
    All = 0,
    Matn = 1,
    Isnad = 2
}

public enum SearchMatchType
{
    AllWords = 0,
    AnyWord = 1,
    Exact = 2
}

public enum SearchLogicalOperator
{
    And = 0,
    Or = 1
}

public class SearchRequestDto
{
    public string Query { get; set; } = string.Empty;
    public SearchScope Scope { get; set; } = SearchScope.All;
    public SearchMatchType Match { get; set; } = SearchMatchType.AllWords;

    // ── Shamela Advanced Search Properties ──────────────────────────
    public List<string> Phrases { get; set; } = [];
    public SearchLogicalOperator Operator { get; set; } = SearchLogicalOperator.And;
    public List<string> ExcludePhrases { get; set; } = [];
    public bool IsOrdered { get; set; } = false;
    public bool IsProximity { get; set; } = false;
    public int ProximityWords { get; set; } = 15;
}
