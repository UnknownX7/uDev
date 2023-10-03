using System;
using System.Numerics;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace uDev.UI.Modules;

public unsafe class AgentUI : PluginUIModule
{
    public override string MenuLabel => "Addon Agents";
    public override int MenuPriority => 15;

    private ushort selectedAddonID;
    private uint? selectedAgentID;
    private string addonSearch = string.Empty;
    private string agentSearch = string.Empty;

    private static RaptureAtkUnitManager* RaptureAtkUnitManager => &Common.UIModule->GetRaptureAtkModule()->RaptureAtkUnitManager;
    private static AtkUnitList* LoadedAddons => &RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;

    public override void Draw()
    {
        if (!ImGui.BeginTabBar("AgentUITabBar")) return;

        if (ImGui.BeginTabItem("Addons"))
        {
            DrawAddonTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Agents"))
        {
            DrawAgentTab();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private void DrawAddonTab()
    {
        var width = 250 * ImGuiHelpers.GlobalScale;
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##Search", "Search", ref addonSearch, 128, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.BeginChild("AddonList", new Vector2(width, 0), true);
        for (int i = 0; i < LoadedAddons->Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var addon = LoadedAddons->EntriesSpan[i];
            using var __ = ImGuiEx.DisabledBlock.Begin(DalamudApi.GameGui.FindAgentInterface(addon) == nint.Zero);
            var name = ((nint)addon.Value).ReadCString();
            if (!name.Contains(addonSearch, StringComparison.CurrentCultureIgnoreCase) || !ImGui.Selectable(name, addon.Value->ID == selectedAddonID)) continue;
            selectedAddonID = addon.Value->ID;
        }
        ImGui.EndChild();
        ImGui.EndGroup();

        if (selectedAddonID == 0) return;

        ImGui.SameLine();
        ImGui.BeginChild("AddonDetails");

        var selectedAddon = RaptureAtkUnitManager->GetAddonById(selectedAddonID);
        var agent = DalamudApi.GameGui.FindAgentInterface(selectedAddon);
        if (agent != nint.Zero)
        {
            var id = GetAgentID(agent);
            ImGui.TextUnformatted($"[#{id}] {(AgentId)id}");
            MemoryUI.DrawMemoryEditorChild(agent, 0x400, true);
        }
        else
        {
            ImGui.TextUnformatted("No agent found.");
        }

        ImGui.EndChild();
    }

    private void DrawAgentTab()
    {
        var width = 250 * ImGuiHelpers.GlobalScale;
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##Search", "Search", ref agentSearch, 128, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.BeginChild("AgentList", new Vector2(width, 0), true);
        var i = 0u;
        while (true)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var agent = Common.UIModule->GetAgentModule()->GetAgentByInternalID(i);
            if (agent == null) break;
            var name = $"[#{i}] {(AgentId)i}";
            if (name.Contains(agentSearch, StringComparison.CurrentCultureIgnoreCase) && ImGui.Selectable(name, i == selectedAgentID))
                selectedAgentID = i;
            i++;
        }
        ImGui.EndChild();
        ImGui.EndGroup();

        if (selectedAgentID == null) return;

        ImGui.SameLine();

        var selectedAgent = Common.UIModule->GetAgentModule()->GetAgentByInternalID(selectedAgentID.Value);
        MemoryUI.DrawMemoryEditorChild(selectedAgent, 0x400, true);
    }

    private static uint GetAgentID(nint address)
    {
        var i = 0u;
        while (true)
        {
            var agent = Common.UIModule->GetAgentModule()->GetAgentByInternalID(i);
            if (agent == null) break;
            if ((nint)agent == address) return i;
            i++;
        }

        return 0;
    }
}