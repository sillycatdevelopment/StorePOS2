using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StorePos.Core.Services;
using StorePos.Core.Utilities;
using StorePos.Web.Infrastructure;

namespace StorePos.Web.Pages.Settings;

[Authorize(Policy = Policies.Admin)]
[RequestSizeLimit(500_000_000)]
[RequestFormLimits(MultipartBodyLengthLimit = 500_000_000)]
public class BackupsModel(BackupService backups, SettingsService settings) : AppPageModel
{
    public List<BackupFile> Files { get; private set; } = new();
    public string Directory { get; private set; } = "";
    public string DatabasePath { get; private set; } = "";
    public bool AutoEnabled { get; private set; }
    public int IntervalHours { get; private set; }
    public int KeepCount { get; private set; }
    public string LastAuto { get; private set; } = "";
    public string ConfiguredDirectory { get; private set; } = "";

    public async Task OnGetAsync()
    {
        Directory = await backups.GetDirectoryAsync();
        DatabasePath = backups.DatabasePath;
        Files = await backups.ListAsync();
        AutoEnabled = await settings.GetBoolAsync(SettingKeys.BackupAutoEnabled);
        IntervalHours = await settings.GetIntAsync(SettingKeys.BackupIntervalHours);
        KeepCount = await settings.GetIntAsync(SettingKeys.BackupKeepCount);
        LastAuto = await settings.GetAsync(SettingKeys.BackupLastAutoAt);
        ConfiguredDirectory = await settings.GetAsync(SettingKeys.BackupDirectory);
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var f = await backups.CreateBackupAsync("manual");
        Ok($"Respaldo creado: {f.Name}");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostConfigAsync(string? directory, bool autoEnabled, int intervalHours, int keepCount)
    {
        await TryAsync(async () =>
        {
            var dir = TextNorm.Clean(directory);
            if (dir is not null)
            {
                if (!Path.IsPathRooted(dir)) throw new DomainException("Escribe una ruta completa, por ejemplo D:\\Respaldos\\StorePOS.");
                try { System.IO.Directory.CreateDirectory(dir); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new DomainException("No se puede usar esa carpeta: " + ex.Message); }
            }
            await settings.SetManyAsync(new Dictionary<string, string>
            {
                [SettingKeys.BackupDirectory] = dir ?? "",
                [SettingKeys.BackupAutoEnabled] = autoEnabled ? "true" : "false",
                [SettingKeys.BackupIntervalHours] = Math.Clamp(intervalHours, 1, 24 * 30).ToString(),
                [SettingKeys.BackupKeepCount] = Math.Clamp(keepCount, 3, 365).ToString(),
            });
        }, "Configuración de respaldos guardada.", redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadAsync(string name)
    {
        try
        {
            var path = await backups.ResolveAsync(name);
            return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path));
        }
        catch (DomainException ex) { Fail(ex.Message); return RedirectToPage(); }
    }

    public async Task<IActionResult> OnPostRestoreAsync(string name)
    {
        await TryAsync(async () =>
        {
            var safety = await backups.RestoreAsync(await backups.ResolveAsync(name));
            Ok($"Respaldo {name} restaurado. Antes se guardó un respaldo de seguridad: {safety}.");
        }, null, redirecting: true);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0) { Fail("Selecciona un archivo de respaldo (.db)."); return RedirectToPage(); }
        await TryAsync(async () =>
        {
            await using var stream = file.OpenReadStream();
            var path = await backups.SaveUploadAsync(stream);
            var safety = await backups.RestoreAsync(path);
            Ok($"Respaldo subido y restaurado. Respaldo de seguridad previo: {safety}.");
        }, null, redirecting: true);
        return RedirectToPage();
    }
}
