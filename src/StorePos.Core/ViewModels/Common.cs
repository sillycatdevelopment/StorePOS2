using Microsoft.EntityFrameworkCore;

namespace StorePos.Core.ViewModels;

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    public static async Task<PagedResult<T>> CreateAsync(IQueryable<T> query, int page, int pageSize)
    {
        if (page < 1) page = 1;
        int total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new PagedResult<T> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public static PagedResult<T> FromList(IReadOnlyList<T> all, int page, int pageSize)
    {
        if (page < 1) page = 1;
        return new PagedResult<T>
        {
            Items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page, PageSize = pageSize, TotalCount = all.Count
        };
    }
}

public record Option(int Id, string Name);
