using Dalamud.Game;
using Dalamud.Plugin;
using uDev.UI;

namespace uDev;

#pragma warning disable IDE1006 // Naming Styles

public class uDev : DalamudPlugin<uDev, Configuration>, IDalamudPlugin
{
    public override string Name => "uDev";

    public uDev(DalamudPluginInterface pluginInterface) : base(pluginInterface) { }

    protected override void Initialize() => DalamudApi.SigScanner.Inject(typeof(Common));

    protected override void ToggleConfig() => MainUI.IsVisible ^= true;

    [PluginCommand("/udev", HelpMessage = "Opens / closes the config.")]
    private void ToggleConfig(string command, string argument) => ToggleConfig();

    protected override void Update(Framework framework) { }

    protected override void Draw() => MainUI.Draw();

    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        AddressUI.Hook?.Dispose();
    }
}