namespace DrugSearcher.Models;

/// <summary>
/// 分页搜索条件
/// </summary>
public class PaginatedDrugSearchCriteria : DrugSearchCriteria
{
    public int PageIndex { get; set; } = 0;

    public int PageSize { get; set; } = 30;
}

/// <summary>
/// 分页搜索结果
/// </summary>
public class PaginatedSearchResult
{
    public List<UnifiedDrugSearchResult> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 0;
    public bool HasNextPage => PageIndex + 1 < TotalPages;
}