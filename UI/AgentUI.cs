using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace uDev.UI;

public static unsafe class AgentUI
{
    private static ushort selectedAddonID;
    private static uint selectedAgentID;

    private static RaptureAtkUnitManager* RaptureAtkUnitManager => &Common.UIModule->GetRaptureAtkModule()->RaptureAtkUnitManager;
    private static AtkUnitList* LoadedAddons => &RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;

    public static void Draw()
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

    private static void DrawAddonTab()
    {
        ImGui.BeginChild("AddonList", new Vector2(250 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
        for (int i = 0; i < LoadedAddons->Count; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var addon = (&LoadedAddons->AtkUnitEntries)[i];
            var name = ((nint)addon->Name).ReadCString();
            if (!ImGui.Selectable(name, addon->ID == selectedAddonID)) continue;
            selectedAddonID = addon->ID;
        }
        ImGui.EndChild();

        if (selectedAddonID == 0) return;

        ImGui.SameLine();
        ImGui.BeginChild("AddonDetails");

        var selectedAddon = RaptureAtkUnitManager->GetAddonById(selectedAddonID);
        var agent = DalamudApi.GameGui.FindAgentInterface(selectedAddon);
        if (agent != nint.Zero)
        {
            var id = GetAgentID(agent);
            ImGui.TextUnformatted($"[#{id}] {(AgentId)id}");
            ImGui.BeginChild("AgentMemoryDetails", ImGui.GetContentRegionAvail(), true);
            MemoryUI.DrawMemoryDetails(agent, 0x500);
            ImGui.EndChild();
        }
        else
        {
            ImGui.TextUnformatted("No agent found.");
        }

        ImGui.EndChild();
    }

    private static void DrawAgentTab()
    {
        ImGui.BeginChild("AgentList", new Vector2(250 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
        var i = 0u;
        while (true)
        {
            using var _ = ImGuiEx.IDBlock.Begin((int)i);
            var agent = Common.UIModule->GetAgentModule()->GetAgentByInternalID(i);
            if (agent == null) break;
            if (ImGui.Selectable($"[#{i}] {(AgentId)i}", i == selectedAgentID))
                selectedAgentID = i;
            i++;
        }
        ImGui.EndChild();

        if (selectedAgentID == 0) return;

        ImGui.SameLine();

        ImGui.BeginChild("AgentMemoryDetails", ImGui.GetContentRegionAvail(), true);
        var selectedAgent = Common.UIModule->GetAgentModule()->GetAgentByInternalID(selectedAgentID);
        MemoryUI.DrawMemoryDetails(selectedAgent, 0x500);
        ImGui.EndChild();
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