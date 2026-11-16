using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;

namespace StorePos.Core.Services;

/// <summary>Singleton in-memory copy of AppSettings so pages don't query settings on every request.</summary>
public sealed class SettingsCache
{
    internal readonly ConcurrentDictionary<string, string> Values = new();
    internal volatile bool Loaded;
    public void Invalidate() => Loaded = false;
}

public static class SettingKeys
{
    public const string StoreName = "Store.Name";
    public const string ReceiptFooter = "Store.ReceiptFooter";
    public const string AllocationMode = "Inventory.AllocationMode";
    public const string LowStockMarginPercent = "Inventory.LowStockMarginPercent";
    public const string SafetyDays = "Stocking.SafetyDays";
    public const string DefaultCycleDays = "Stocking.DefaultCycleDays";
    public const string HistoryDays = "Stocking.HistoryDays";
    public const string BackupDirectory = "Backup.Directory";
    public const string BackupAutoEnabled = "Backup.AutoEnabled";
    public const string BackupIntervalHours = "Backup.IntervalHours";
    public const string BackupKeepCount = "Backup.KeepCount";
    public const string BackupLastAutoAt = "Backup.LastAutoAt";
}

public class SettingsService(StoreDbContext db, SettingsCache cache, AuditService audit)
{
    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        [SettingKeys.StoreName] = "Mi Tienda",
        [SettingKeys.ReceiptFooter] = "¡Gracias por su compra!",
        [SettingKeys.AllocationMode] = nameof(Models.AllocationMode.Manual),
        [SettingKeys.LowStockMarginPercent] = "50",
        [SettingKeys.SafetyDays] = "2",
        [SettingKeys.DefaultCycleDays] = "7",
        [SettingKeys.HistoryDays] = "28",
        [SettingKeys.BackupDirectory] = "",
        [SettingKeys.BackupAutoEnabled] = "true",
        [SettingKeys.BackupIntervalHours] = "24",
        [SettingKeys.BackupKeepCount] = "30",
        [SettingKeys.BackupLastAutoAt] = "",
    };

    private async Task EnsureLoadedAsync()
    {
        if (cache.Loaded) return;
        var all = await db.AppSettings.AsNoTracking().ToListAsync();
        cache.Values.Clear();
        foreach (var s in all) cache.Values[s.Key] = s.Value;
        cache.Loaded = true;
    }

    public async Task<string> GetAsync(string key)
    {
        await EnsureLoadedAsync();
        return cache.Values.TryGetValue(key, out var v) ? v : Defaults.GetValueOrDefault(key, "");
    }

    public async Task<int> GetIntAsync(string key) =>
        int.TryParse(await GetAsync(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v : int.Parse(Defaults[key], CultureInfo.InvariantCulture);

    public async Task<bool> GetBoolAsync(string key) => bool.TryParse(await GetAsync(key), out var v) && v;

    public async Task<AllocationMode> GetAllocationModeAsync() =>
        Enum.TryParse<AllocationMode>(await GetAsync(SettingKeys.AllocationMode), out var m) ? m : AllocationMode.Manual;

    /// <summary>Saves several settings at once. Audited unless <paramref name="silent"/>.</summary>
    public async Task SetManyAsync(IDictionary<string, string> values, bool silent = false)
    {
        var keys = values.Keys.ToList();
        var existing = await db.AppSettings.Where(s => keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key);
        var changes = new List<string>();
        foreach (var (key, raw) in values)
        {
            var value = raw?.Trim() ?? "";
            if (existing.TryGetValue(key, out var row))
            {
                if (row.Value == value) continue;
                changes.Add($"{key}: '{row.Value}' → '{value}'");
                row.Value = value;
            }
            else
            {
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });
                changes.Add($"{key}: '{value}'");
            }
        }
        if (changes.Count == 0) return;
        if (!silent) audit.Add("Configuración modificada", "AppSetting", null, string.Join("; ", changes));
        await db.SaveChangesAsync();
        cache.Invalidate();
    }
}
