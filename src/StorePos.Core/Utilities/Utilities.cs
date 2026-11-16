using System.Globalization;
using System.Text;
using StorePos.Core.Models;

namespace StorePos.Core.Utilities;

/// <summary>Business-rule violation with a message safe to show the user (in Spanish).</summary>
public class DomainException(string message) : Exception(message);

/// <summary>Money is stored as integer centavos. These helpers convert and format.</summary>
public static class Money
{
    public static readonly CultureInfo MX = CultureInfo.GetCultureInfo("es-MX");

    public static long ToCents(decimal pesos) =>
        (long)Math.Round(pesos * 100m, MidpointRounding.AwayFromZero);

    public static decimal ToPesos(long cents) => cents / 100m;

    public static string Format(long cents) => ToPesos(cents).ToString("C2", MX);

    /// <summary>Parses "18", "18.5", "$1,234.50". Returns false on invalid or negative input.</summary>
    public static bool TryParse(string? text, out long cents)
    {
        cents = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = text.Replace("$", "").Replace(",", "").Trim();
        if (!decimal.TryParse(clean, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) || d < 0)
            return false;
        cents = ToCents(d);
        return true;
    }

    /// <summary>price (per piece or per kg) × quantity (pieces or grams), rounded to the centavo.</summary>
    public static long LineAmount(long unitCents, long qty, UnitType unit) =>
        unit == UnitType.Piece
            ? unitCents * qty
            : (long)Math.Round(unitCents * (decimal)qty / 1000m, MidpointRounding.AwayFromZero);
}

/// <summary>Quantities are integers in base units: pieces, or grams for kilogram products.</summary>
public static class Qty
{
    public static long Scale(UnitType unit) => unit == UnitType.Kilogram ? 1000 : 1;

    public static decimal ToDisplay(long qty, UnitType unit) => qty / (decimal)Scale(unit);

    public static string Format(long qty, UnitType unit) =>
        unit == UnitType.Piece
            ? qty.ToString("N0", Money.MX) + " pza"
            : (qty / 1000m).ToString("0.###", Money.MX) + " kg";

    public static string UnitName(UnitType unit) => unit == UnitType.Piece ? "pza" : "kg";

    /// <summary>Converts a user-entered amount (pieces, or kg with decimals) to base units.</summary>
    public static long FromDisplay(decimal value, UnitType unit)
    {
        if (unit == UnitType.Piece)
        {
            if (value != Math.Floor(value))
                throw new DomainException("Este producto se vende por pieza (número entero).");
            return (long)value;
        }
        return (long)Math.Round(value * 1000m, MidpointRounding.AwayFromZero);
    }

    public static bool TryParse(string? text, UnitType unit, out long qty)
    {
        qty = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!decimal.TryParse(text.Trim().Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return false;
        try { qty = FromDisplay(d, unit); } catch (DomainException) { return false; }
        return true;
    }
}

public static class TextNorm
{
    /// <summary>Lowercase, trimmed, accents removed, inner whitespace collapsed. "Café  Ñora" → "cafe nora".</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        bool lastSpace = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!lastSpace) sb.Append(' ');
                lastSpace = true;
                continue;
            }
            sb.Append(c);
            lastSpace = false;
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>Escapes LIKE wildcards so user input is matched literally (used with ESCAPE '\').</summary>
    public static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

/// <summary>Store clock. Everything is stored in local store time (Mexico City).</summary>
public interface IClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
}

public class StoreClock : IClock
{
    private static readonly TimeZoneInfo Zone = FindZone();

    private static TimeZoneInfo FindZone()
    {
        // .NET 6+ accepts IANA ids on Windows too; fall back to the machine's zone.
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Mexico_City"); }
        catch { return TimeZoneInfo.Local; }
    }

    public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone), DateTimeKind.Unspecified);
    public DateOnly Today => DateOnly.FromDateTime(Now);
}

/// <summary>Who is performing the current operation (for audit trails and permissions).</summary>
public interface ICurrentUser
{
    int? UserId { get; }
    string UserName { get; }
    UserRole Role { get; }
    bool IsAuthenticated { get; }
}

public static class RoleExtensions
{
    public static bool AtLeast(this UserRole role, UserRole required) => (int)role >= (int)required;

    public static string Display(this UserRole role) => role switch
    {
        UserRole.Administrator => "Administrador",
        UserRole.Manager => "Encargado",
        _ => "Cajero"
    };
}

/// <summary>
/// Process-wide gate for operations that change stock or cash. SQLite allows one writer at a time;
/// serializing these in-process avoids SQLITE_BUSY retries and makes check-then-write logic safe.
/// </summary>
public sealed class WriteGate
{
    private readonly SemaphoreSlim _sem = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken ct = default)
    {
        await _sem.WaitAsync(ct);
        return new Releaser(_sem);
    }

    private sealed class Releaser(SemaphoreSlim sem) : IDisposable
    {
        private int _done;
        public void Dispose() { if (Interlocked.Exchange(ref _done, 1) == 0) sem.Release(); }
    }
}

public static class DisplayNames
{
    public static string Of(MovementType t) => t switch
    {
        MovementType.InitialStock => "Inventario inicial",
        MovementType.Restock => "Resurtido",
        MovementType.Sale => "Venta",
        MovementType.SaleCancellation => "Cancelación de venta",
        MovementType.Return => "Devolución",
        MovementType.Damaged => "Merma / dañado",
        MovementType.Correction => "Corrección manual",
        MovementType.TransferOut => "Traspaso (salida)",
        MovementType.TransferIn => "Traspaso (entrada)",
        MovementType.PackOpened => "Caja abierta",
        MovementType.UnitsFromPack => "Sueltos de caja abierta",
        _ => t.ToString()
    };

    public static string Of(CashMovementType t) => t switch
    {
        CashMovementType.Deposit => "Depósito a caja",
        CashMovementType.Withdrawal => "Retiro de caja",
        CashMovementType.Expense => "Gasto",
        CashMovementType.Refund => "Reembolso",
        CashMovementType.OtherIn => "Otra entrada",
        CashMovementType.OtherOut => "Otra salida",
        _ => t.ToString()
    };

    public static string Of(RequestStatus s) => s switch
    {
        RequestStatus.Pending => "Pendiente",
        RequestStatus.Ordered => "Pedido",
        RequestStatus.Stocked => "En existencia",
        RequestStatus.NotPurchasing => "No se comprará",
        _ => s.ToString()
    };

    public static string Of(StockingListStatus s) => s switch
    {
        StockingListStatus.Draft => "Borrador",
        StockingListStatus.Purchased => "Comprada",
        StockingListStatus.Received => "Recibida",
        StockingListStatus.Cancelled => "Cancelada",
        _ => s.ToString()
    };

    public static string Of(AllocationMode m) => m switch
    {
        AllocationMode.Manual => "Selección manual del dueño",
        AllocationMode.OldestFirst => "Lote más antiguo primero (FIFO global)",
        AllocationMode.Automatic => "Automático (dueño prioritario, luego mayor existencia)",
        _ => m.ToString()
    };

    private static readonly string[] Days = { "Domingo", "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado" };
    public static string Of(DayOfWeek d) => Days[(int)d];
}
