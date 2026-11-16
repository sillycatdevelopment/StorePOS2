using System.Runtime.InteropServices;

namespace StorePos.Web.Infrastructure;

/// <summary>
/// Windows console tweaks for the black StorePOS window.
/// Clicking inside a console with "Modo de edición rápida" (QuickEdit) enabled puts it in selection mode
/// ("Seleccionar" in the title), which PAUSES the program — the POS would stop responding on every device.
/// We turn QuickEdit off and give the window a title that explains what to do if it ever happens.
/// </summary>
public static class WindowsConsole
{
    public const string Title = "StorePOS - NO CERRAR - Si este titulo empieza con 'Seleccionar', presiona Esc";

    private const int StdInputHandle = -10;
    private const uint EnableQuickEditMode = 0x0040;
    private const uint EnableExtendedFlags = 0x0080;

    // Classic DllImport: needs no unsafe code or source generator.
    [DllImport("kernel32.dll")]
    private static extern nint GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll")]
    private static extern bool GetConsoleMode(nint hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleMode(nint hConsoleHandle, uint dwMode);

    public static void Prepare()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            Console.Title = Title;
            var handle = GetStdHandle(StdInputHandle);
            if (GetConsoleMode(handle, out var mode))
                SetConsoleMode(handle, (mode & ~EnableQuickEditMode) | EnableExtendedFlags);

            Console.WriteLine();
            Console.WriteLine("  ==============================================================");
            Console.WriteLine("   StorePOS esta funcionando. NO CIERRES esta ventana.");
            Console.WriteLine("   Si el sistema deja de responder y el titulo de esta ventana");
            Console.WriteLine("   empieza con 'Seleccionar', haz clic aqui y presiona Esc.");
            Console.WriteLine("  ==============================================================");
            Console.WriteLine();
        }
        catch
        {
            // Not attached to a console (e.g. running as a service): nothing to do.
        }
    }
}
