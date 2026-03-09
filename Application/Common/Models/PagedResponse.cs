namespace Application.Common.Models;

/// <summary>
/// Paginated API response envelope.
/// Matches spec §7 pagination format.
/// </summary>
public class PagedResponse<T>
{
    public bool Success { get; init; } = true;
    public required IReadOnlyList<T> Data { get; init; }
    public required PaginationMeta Pagination { get; init; }
}

public class PaginationMeta
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalItems / PageSize) : 0;
}
