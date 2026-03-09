using Microsoft.EntityFrameworkCore;

namespace Application.Common.Models;

/// <summary>
/// Query parameters for paginated endpoints.
/// </summary>
public class PagedQuery
{
    private int _page = 1;
    private int _pageSize = 20;

    public int Page
    {
        get => _page;
        set => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value switch
        {
            < 1 => 20,
            > 100 => 100,
            _ => value
        };
    }

    public string Sort { get; set; } = "createdAt:desc";
}

/// <summary>
/// Result of a paginated query.
/// </summary>
public class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
}

public static class PagedQueryExtensions
{
    /// <summary>
    /// Applies pagination to an IQueryable and returns a PagedResult.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagedQuery paging,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = paging.Page,
            PageSize = paging.PageSize
        };
    }

    /// <summary>
    /// Converts a PagedResult into a PagedResponse envelope.
    /// </summary>
    public static PagedResponse<T> ToPagedResponse<T>(this PagedResult<T> result) => new()
    {
        Data = result.Items,
        Pagination = new PaginationMeta
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalItems = result.TotalCount
        }
    };
}
