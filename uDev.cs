using Dalamud.Plugin;
using Hypostasis.Debug;
using uDev.UI;

namespace uDev;

#pragma warning disable IDE1006 // Naming Styles

public class uDev(IDalamudPluginInterface pluginInterface) : DalamudPlugin<Configuration>(pluginInterface), IDalamudPlugin
{
    public string Name => "uDev";

    protected override void Initialize()
    {
        DalamudApi.SigScanner.Inject(typeof(Common));
        DebugIPC.DebugHypostasis = true;
    }

    protected override void ToggleConfig() => MainUI.IsVisible ^= true;

    [PluginCommand("/udev", HelpMessage = "Opens / closes the config.")]
    private void ToggleConfig(string command, string argument) => ToggleConfig();

    protected override void Draw() => MainUI.Draw();
}