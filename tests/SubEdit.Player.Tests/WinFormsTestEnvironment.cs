using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace SubEdit.Player.Tests;

internal static class WinFormsTestEnvironment
{
    // Match the production ApplicationConfiguration before any test creates a HWND.
    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
    }
}
