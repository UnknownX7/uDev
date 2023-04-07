using ImGuiNET;

namespace uDev.UI.Modules;

public unsafe class ImGuiMemoryUI : PluginUIModule
{
    public override string MenuLabel => "ImGui Window Memory";
    public override int MenuPriority => 30;
    public override void Draw() => MemoryUI.DrawMemoryEditorChild(ImGuiEx.GetCurrentWindow(), 0x2000, true);
}