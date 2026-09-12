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

public class SearchRequestDto
{
    public string Query { get; set; } = string.Empty;
    public SearchScope Scope { get; set; } = SearchScope.All;
    public SearchMatchType Match { get; set; } = SearchMatchType.AllWords;
}
