namespace DrugSearcher.Models;

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];

    public int TotalCount { get; set; }

    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 0;
    public bool HasNextPage => PageIndex + 1 < TotalPages;
}