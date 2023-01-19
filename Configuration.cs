using Dalamud.Configuration;

namespace uDev;

public class Configuration : PluginConfiguration<Configuration>, IPluginConfiguration
{
    public override int Version { get; set; }

    public override void Initialize() { }
}