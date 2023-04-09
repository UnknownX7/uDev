using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace uDev.UI;

public static class MainUI
{
    private static bool isVisible = uDev.Config.OpenOnStartup;
    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    private static readonly List<PluginUIModule> uiModules = PluginModuleManager.PluginModules.OfType<PluginUIModule>().OrderBy(module => module.MenuPriority).ToList();
    private static Vector2 dummySize = ImGuiHelpers.ScaledVector2(21);
    private static Vector2 uiModuleListSize = dummySize;

    public static void Draw()
    {
        MemoryUI.DrawMemoryEditors();

        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(ImGuiHelpers.ScaledVector2(1000, 650), new Vector2(9999));
        ImGui.Begin("uDev", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

        if (ImGuiEx.AddHeaderIcon("OpenOnStartup", FontAwesomeIcon.MapPin, new ImGuiEx.HeaderIconOptions { Tooltip = "Open on Startup", Color = uDev.Config.OpenOnStartup ? 0xFFFFFFFF : 0x30FFFFFF }))
        {
            uDev.Config.OpenOnStartup ^= true;
            uDev.Config.Save();
        }

        ImGuiEx.AddDonationHeader();

        var prevCursorPos = ImGui.GetCursorPos();
        var openMenu = false;
        if (uiModuleListSize.Y > dummySize.Y)
        {
            ImGui.Dummy(dummySize);
        }
        else
        {
            ImGuiEx.FontButton(FontAwesomeIcon.Bars.ToIconString(), UiBuilder.IconFont);
            if (ImGui.IsItemHovered())
                openMenu = true;
            dummySize = ImGui.GetItemRectSize();
        }

        ImGui.BeginChild("PluginUIModule");
        var selectedModule = uDev.Config.SelectedMenuModule;
        if (selectedModule >= 0 && selectedModule < uiModules.Count && uiModules[selectedModule] is { IsValid: true } m)
            m.Draw();
        ImGui.EndChild();

        ImGui.SetCursorPos(prevCursorPos);

        if (uiModuleListSize.Y > dummySize.Y || openMenu)
        {
            var style = ImGui.GetStyle();
            ImGui.BeginChild("UIModuleList", uiModuleListSize, true);
            ImGuiEx.PushClipRectFullScreen();
            ImGui.GetWindowDrawList().AddRectFilled(ImGui.GetWindowPos() + Vector2.One, ImGui.GetWindowPos() + ImGui.GetWindowSize() - Vector2.One, ImGui.GetColorU32(ImGuiCol.WindowBg));
            ImGui.PopClipRect();

            for (int i = 0; i < uiModules.Count; i++)
            {
                var module = uiModules[i];
                using var _ = ImGuiEx.DisabledBlock.Begin(!module.IsValid);
                if (ImGui.Selectable(module.MenuLabel, i == uDev.Config.SelectedMenuModule))
                    uDev.Config.SelectedMenuModule = i;
            }

            var maxHeight = ImGui.GetCursorPosY() + style.ItemSpacing.Y;
            if (uiModuleListSize.Y == maxHeight)
            {
                uiModuleListSize.X = ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                    ? Math.Min(uiModuleListSize.X + (float)DalamudApi.Framework.UpdateDelta.TotalSeconds * 1000 * ImGuiHelpers.GlobalScale, 200 * ImGuiHelpers.GlobalScale)
                    : Math.Max(uiModuleListSize.X - (float)DalamudApi.Framework.UpdateDelta.TotalSeconds * 1000 * ImGuiHelpers.GlobalScale, dummySize.X);

                if (uiModuleListSize.X == dummySize.X)
                    uiModuleListSize.Y = maxHeight - 1;
            }
            else
            {
                uiModuleListSize.Y = openMenu || ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                    ? Math.Min(uiModuleListSize.Y + (float)DalamudApi.Framework.UpdateDelta.TotalSeconds * maxHeight * 8 * ImGuiHelpers.GlobalScale, maxHeight)
                    : Math.Max(uiModuleListSize.Y - (float)DalamudApi.Framework.UpdateDelta.TotalSeconds * maxHeight * 8 * ImGuiHelpers.GlobalScale, dummySize.Y);
            }

            ImGui.EndChild();
        }

        ImGui.End();
    }
}