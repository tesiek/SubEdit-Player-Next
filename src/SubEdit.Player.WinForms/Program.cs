using SubEdit.Player.Core.Configuration;
using SubEdit.Player.WinForms.Playback;

namespace SubEdit.Player.WinForms;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var settingsStore = new PortableSettingsStore(AppContext.BaseDirectory);
        var settings = settingsStore.Load();
        var startupVideoPath = args.FirstOrDefault(File.Exists);
        var engineFactory = new FlyleafEngineFactory();

        Application.Run(new Form1(settings, settingsStore, engineFactory, startupVideoPath));
    }
}
