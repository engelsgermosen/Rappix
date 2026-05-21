namespace Rappix.BuildingBlocks.Core.Pagination;

public sealed record PagedRequest(int Page = 1, int PageSize = 20)
{
    public int Skip => (Math.Max(1, Page) - 1) * PageSize;
    public int Take => Math.Clamp(PageSize, 1, 100);
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new([], page, pageSize, 0);
}
