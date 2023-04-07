using ImGuiNET;

namespace uDev.UI.Modules;

public class GameObjectUI : PluginUIModule
{
    public override string MenuLabel => "Game Object Editor";
    public override int MenuPriority => 20;

    public override void Draw()
    {
        if (ImGui.Button("Initialize"))
            ImGuiEx.FloatingText("fuck you ariel");
    }
}