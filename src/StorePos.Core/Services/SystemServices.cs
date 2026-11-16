using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StorePos.Core.Data;
using StorePos.Core.Models;
using StorePos.Core.Utilities;

namespace StorePos.Core.Services;

public class UserService(StoreDbContext db, AuditService audit, ICurrentUser current, IClock clock)
{
    private static readonly PasswordHasher<User> Hasher = new();
    public const int MinPasswordLength = 4;

    public Task<bool> AnyUsersAsync() => db.Users.AnyAsync();

    public Task<List<User>> GetAllAsync() => db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();

    /// <summary>Creates a user. The very first user can be created without being signed in and is always an administrator.</summary>
    public async Task<int> CreateAsync(string username, string displayName, string password, UserRole role)
    {
        bool first = !await db.Users.AnyAsync();
        if (!first && !current.Role.AtLeast(UserRole.Administrator)) throw new DomainException("Solo un administrador puede crear usuarios.");
        var name = TextNorm.Clean(username)?.ToLowerInvariant() ?? throw new DomainException("El usuario es obligatorio.");
        if (name.Any(char.IsWhiteSpace)) throw new DomainException("El usuario no puede tener espacios.");
        if (await db.Users.AnyAsync(u => u.Username == name)) throw new DomainException("Ese usuario ya existe.");
        ValidatePassword(password);

        var u = new User
        {
            Username = name, DisplayName = TextNorm.Clean(displayName) ?? name,
            Role = first ? UserRole.Administrator : role, CreatedAt = clock.Now
        };
        u.PasswordHash = Hasher.HashPassword(u, password);
        db.Users.Add(u);
        await db.SaveChangesAsync();
        audit.Add("Usuario creado", "User", u.Id, $"{u.Username} ({u.Role.Display()})");
        await db.SaveChangesAsync();
        return u.Id;
    }

    private static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new DomainException($"La contraseña debe tener al menos {MinPasswordLength} caracteres.");
    }

    public async Task<User?> ValidateAsync(string username, string password)
    {
        var name = username.Trim().ToLowerInvariant();
        var u = await db.Users.FirstOrDefaultAsync(x => x.Username == name);
        if (u is null || !u.IsActive) return null;
        var result = Hasher.VerifyHashedPassword(u, u.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed) return null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded) u.PasswordHash = Hasher.HashPassword(u, password);
        u.LastLoginAt = clock.Now;
        await db.SaveChangesAsync();
        return u;
    }

    public async Task UpdateAsync(int id, string displayName, UserRole role, bool isActive, string? newPassword)
    {
        if (!current.Role.AtLeast(UserRole.Administrator)) throw new DomainException("Solo un administrador puede modificar usuarios.");
        var u = await db.Users.FindAsync(id) ?? throw new DomainException("Usuario no encontrado.");
        bool losesAdmin = u.Role == UserRole.Administrator && u.IsActive && (role != UserRole.Administrator || !isActive);
        if (losesAdmin && !await db.Users.AnyAsync(x => x.Id != id && x.IsActive && x.Role == UserRole.Administrator))
            throw new DomainException("Debe quedar al menos un administrador activo.");

        var changes = new List<string>();
        if (u.Role != role) changes.Add($"rol {u.Role.Display()} → {role.Display()}");
        if (u.IsActive != isActive) changes.Add(isActive ? "activado" : "desactivado");
        u.DisplayName = TextNorm.Clean(displayName) ?? u.DisplayName;
        u.Role = role;
        u.IsActive = isActive;
        if (!string.IsNullOrEmpty(newPassword))
        {
            ValidatePassword(newPassword);
            u.PasswordHash = Hasher.HashPassword(u, newPassword);
            changes.Add("contraseña cambiada");
        }
        if (changes.Count > 0) audit.Add("Usuario modificado", "User", u.Id, $"{u.Username}: {string.Join(", ", changes)}");
        await db.SaveChangesAsync();
    }

    public async Task ChangeOwnPasswordAsync(string currentPassword, string newPassword)
    {
        var u = await db.Users.FindAsync(current.UserId ?? 0) ?? throw new DomainException("Sesión no válida.");
        if (Hasher.VerifyHashedPassword(u, u.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            throw new DomainException("La contraseña actual no es correcta.");
        ValidatePassword(newPassword);
        u.PasswordHash = Hasher.HashPassword(u, newPassword);
        audit.Add("Contraseña cambiada", "User", u.Id, u.Username);
        await db.SaveChangesAsync();
    }
}

public record BackupFile(string Name, DateTime CreatedAt, long SizeBytes);

/// <summary>
/// Backups use SQLite's online backup API, which produces a consistent copy even while the
/// application keeps reading and writing (unlike copying the .db/-wal files).
/// </summary>
public class BackupService(StoreDbContext db, SettingsService settings, SettingsCache cache, AuditService audit,
                           IClock clock, WriteGate gate, ILogger<BackupService> log)
{
    private string ConnectionString => db.Database.GetConnectionString() ?? throw new InvalidOperationException("Sin cadena de conexión.");

    public string DatabasePath => Path.GetFullPath(new SqliteConnectionStringBuilder(ConnectionString).DataSource);

    public async Task<string> GetDirectoryAsync()
    {
        var configured = await settings.GetAsync(SettingKeys.BackupDirectory);
        var dir = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetDirectoryName(DatabasePath)!, "backups")
            : configured.Trim();
        Directory.CreateDirectory(dir);
        return Path.GetFullPath(dir);
    }

    public async Task<BackupFile> CreateBackupAsync(string kind = "manual")
    {
        var dir = await GetDirectoryAsync();
        var name = $"storepos_{clock.Now:yyyyMMdd_HHmmss}_{kind}.db";
        var path = Path.Combine(dir, name);

        await Task.Run(() =>
        {
            using var source = new SqliteConnection(ConnectionString);
            source.Open();
            using var dest = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
            dest.Open();
            source.BackupDatabase(dest);
        });

        if (kind == "manual") { audit.Add("Respaldo creado", "Backup", null, path); await db.SaveChangesAsync(); }
        await PruneAsync(dir);
        var info = new FileInfo(path);
        log.LogInformation("Respaldo creado: {Path} ({Size} bytes)", path, info.Length);
        return new BackupFile(name, info.LastWriteTime, info.Length);
    }

    private async Task PruneAsync(string dir)
    {
        int keep = Math.Max(3, await settings.GetIntAsync(SettingKeys.BackupKeepCount));
        foreach (var old in new DirectoryInfo(dir).GetFiles("storepos_*_auto.db").OrderByDescending(f => f.Name).Skip(keep))
        {
            try { old.Delete(); } catch (IOException ex) { log.LogWarning(ex, "No se pudo borrar {File}", old.FullName); }
        }
    }

    public async Task<List<BackupFile>> ListAsync()
    {
        var dir = await GetDirectoryAsync();
        return new DirectoryInfo(dir).GetFiles("*.db").OrderByDescending(f => f.LastWriteTime)
            .Select(f => new BackupFile(f.Name, f.LastWriteTime, f.Length)).ToList();
    }

    public async Task<string> ResolveAsync(string fileName)
    {
        var dir = await GetDirectoryAsync();
        var full = Path.GetFullPath(Path.Combine(dir, Path.GetFileName(fileName)));
        if (!full.StartsWith(dir, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            throw new DomainException("Respaldo no encontrado.");
        return full;
    }

    /// <summary>Checks that a file is an intact StorePOS database.</summary>
    public static void Validate(string path)
    {
        try
        {
            using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            c.Open();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var result = cmd.ExecuteScalar() as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("El respaldo está dañado (integrity_check: " + result + ").");
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Products','Sales','InventoryLots','Owners');";
            if (Convert.ToInt32(cmd.ExecuteScalar()) != 4) throw new DomainException("El archivo no es un respaldo de StorePOS.");
        }
        catch (SqliteException ex)
        {
            throw new DomainException("El archivo no es una base de datos SQLite válida: " + ex.Message);
        }
    }

    /// <summary>
    /// Restores a backup into the live database: validate → safety backup of current data →
    /// copy pages with the backup API while writes are blocked → clear connection pools.
    /// </summary>
    public async Task<string> RestoreAsync(string backupPath)
    {
        Validate(backupPath);
        var safety = await CreateBackupAsync("prerestore");

        using (await gate.EnterAsync())
        {
            await Task.Run(() =>
            {
                using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
                source.Open();
                using var dest = new SqliteConnection(ConnectionString);
                dest.Open();
                source.BackupDatabase(dest);
            });
            SqliteConnection.ClearAllPools();
        }
        cache.Invalidate();

        // Bring an older backup up to the current schema, if migrations are present.
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        audit.Add("Respaldo restaurado", "Backup", null, $"{Path.GetFileName(backupPath)} (respaldo de seguridad previo: {safety.Name})");
        await db.SaveChangesAsync();
        return safety.Name;
    }

    public async Task<string> SaveUploadAsync(Stream content)
    {
        var dir = await GetDirectoryAsync();
        var path = Path.Combine(dir, $"storepos_{clock.Now:yyyyMMdd_HHmmss}_subido.db");
        await using (var f = File.Create(path)) await content.CopyToAsync(f);
        try { Validate(path); }
        catch { File.Delete(path); throw; }
        return path;
    }
}

/// <summary>The only background task: once an hour, checks whether an automatic backup is due.</summary>
public class AutoBackupHostedService(IServiceScopeFactory scopes, ILogger<AutoBackupHostedService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); } catch (OperationCanceledException) { return; }
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                if (!await settings.GetBoolAsync(SettingKeys.BackupAutoEnabled)) continue;
                int hours = Math.Max(1, await settings.GetIntAsync(SettingKeys.BackupIntervalHours));
                var lastText = await settings.GetAsync(SettingKeys.BackupLastAutoAt);
                bool due = !DateTime.TryParse(lastText, System.Globalization.CultureInfo.InvariantCulture,
                                              System.Globalization.DateTimeStyles.None, out var last)
                           || clock.Now - last >= TimeSpan.FromHours(hours);
                if (!due) continue;
                await scope.ServiceProvider.GetRequiredService<BackupService>().CreateBackupAsync("auto");
                await settings.SetManyAsync(new Dictionary<string, string>
                {
                    [SettingKeys.BackupLastAutoAt] = clock.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                }, silent: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Falló el respaldo automático");
            }
        } while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
