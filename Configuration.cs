using Dalamud.Configuration;

namespace uDev;

public class Configuration : PluginConfiguration<Configuration>, IPluginConfiguration
{
    public bool OpenOnStartup = false;
    public int SelectedMenuModule = 0;
}