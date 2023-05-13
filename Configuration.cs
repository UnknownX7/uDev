using Dalamud.Configuration;

namespace uDev;

public partial class Configuration : PluginConfiguration, IPluginConfiguration
{
    public int Version { get; set; }
    public bool OpenOnStartup = false;
    public int SelectedMenuModule = 0;
}