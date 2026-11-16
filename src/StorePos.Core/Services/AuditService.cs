using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;
using StorePos.Core.ViewModels;

namespace StorePos.Core.Services;

/// <summary>
/// Adds audit rows to the current DbContext without saving, so the audit entry is committed
/// in the same transaction as the change it describes (or not at all).
/// </summary>
public class AuditService(StoreDbContext db, ICurrentUser user, IClock clock)
{
    public void Add(string action, string entityType, object? entityId, string? details)
    {
        db.AuditLogs.Add(new AuditLog
        {
            CreatedAt = clock.Now,
            UserId = user.UserId,
            UserName = user.UserName,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Details = details is { Length: > 2000 } ? details[..2000] : details
        });
    }

    public async Task<PagedResult<AuditLog>> SearchAsync(DateOnly? from, DateOnly? to, string? text, int page, int pageSize = 50)
    {
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (from is { } f) { var d = f.ToDateTime(TimeOnly.MinValue); q = q.Where(a => a.CreatedAt >= d); }
        if (to is { } t) { var d = t.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(a => a.CreatedAt < d); }
        if (!string.IsNullOrWhiteSpace(text))
        {
            var like = "%" + TextNorm.EscapeLike(text.Trim()) + "%";
            q = q.Where(a => EF.Functions.Like(a.Action, like, "\\") || EF.Functions.Like(a.Details!, like, "\\")
                          || EF.Functions.Like(a.UserName, like, "\\") || EF.Functions.Like(a.EntityType, like, "\\"));
        }
        return await PagedResult<AuditLog>.CreateAsync(q.OrderByDescending(a => a.Id), page, pageSize);
    }
}
