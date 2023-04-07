namespace uDev.UI;

public abstract class PluginUIModule : PluginModule
{
    public abstract string MenuLabel { get; }
    public abstract int MenuPriority { get; }
    public abstract void Draw();
}