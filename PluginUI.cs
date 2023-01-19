using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace uDev;

public static class PluginUI
{
    public static bool isVisible = false;

    public static void Draw()
    {
        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(700, 660) * ImGuiHelpers.GlobalScale, new Vector2(9999));
        ImGui.Begin("uDev Configuration", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        ImGui.End();
    }
}