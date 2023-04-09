using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Hypostasis.Game.Structures;
using ImGuiNET;

namespace uDev.UI.Modules;

public unsafe class InputDataUI : PluginUIModule
{
    public override string MenuLabel => "Game Input Data";
    public override int MenuPriority => 25;

    private int inputID = 0;

    protected override bool Validate() => Common.InputData != null && InputData.getInputBinding.IsValid;

    public override void Draw()
    {
        var maxInputID = *(int*)((nint)Common.InputData + 0x9AC);
        ImGui.BeginChild("HeldInputIDs", new Vector2(50 * ImGuiHelpers.GlobalScale, 0), true);
        for (uint i = 0; i < maxInputID; i++)
        {
            if (Common.InputData->IsInputIDHeld(i) && ImGui.Selectable(i.ToString()))
                inputID = (int)i;
        }
        ImGui.EndChild();

        ImGui.SameLine();

        using var _ = ImGuiEx.GroupBlock.Begin();
        ImGui.BeginChild("InputData", new Vector2(0, ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() * 2.5f), true);
        MemoryUI.DrawMemoryEditor(Common.InputData, Marshal.SizeOf<InputData>());
        ImGui.EndChild();

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        if (ImGui.InputInt("Input ID", ref inputID))
            inputID = Math.Min(Math.Max(inputID, 0), maxInputID);

        MemoryUI.DrawMemoryEditorChild(Common.InputData->GetInputBinding((uint)inputID), 11);
    }
}