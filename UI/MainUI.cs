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
    private static bool openingMenu = false;

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
        if (uiModuleListSize.Y > dummySize.Y)
        {
            ImGui.Dummy(dummySize);
        }
        else
        {
            ImGuiEx.FontButton(FontAwesomeIcon.Bars.ToIconString(), UiBuilder.IconFont);
            if (ImGui.IsItemHovered())
                openingMenu = true;
            dummySize = ImGui.GetItemRectSize();
        }

        ImGui.BeginChild("PluginUIModule");
        var selectedModule = uDev.Config.SelectedMenuModule;
        if (selectedModule >= 0 && selectedModule < uiModules.Count && uiModules[selectedModule] is { IsValid: true } m)
            m.Draw();
        ImGui.EndChild();

        ImGui.SetCursorPos(prevCursorPos);

        if (uiModuleListSize.Y > dummySize.Y || openingMenu)
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
                var speed = (float)DalamudApi.Framework.UpdateDelta.TotalSeconds * 1500 * ImGuiHelpers.GlobalScale;
                uiModuleListSize.X = openingMenu || ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                    ? Math.Min(uiModuleListSize.X + speed, 200 * ImGuiHelpers.GlobalScale)
                    : Math.Max(uiModuleListSize.X - speed, dummySize.X);

                if (openingMenu && uiModuleListSize.X == 200 * ImGuiHelpers.GlobalScale)
                    openingMenu = false;
                else if (uiModuleListSize.X == dummySize.X)
                    uiModuleListSize.Y = maxHeight - 1;
            }
            else
            {
                var speed = (float)DalamudApi.Framework.UpdateDelta.TotalSeconds * maxHeight * 8;
                uiModuleListSize.Y = openingMenu || ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
                    ? Math.Min(uiModuleListSize.Y + speed, maxHeight)
                    : Math.Max(uiModuleListSize.Y - speed, dummySize.Y);
            }

            ImGui.EndChild();
        }

        ImGui.End();
    }
}