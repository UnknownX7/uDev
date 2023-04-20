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
        ImGui.BeginChild("AxisInputIDs", new Vector2(200 * ImGuiHelpers.GlobalScale, 0), true);
        ImGui.TextUnformatted("Axis Input IDs");
        ImGui.Separator();

        ImGui.TextUnformatted($"0 & 1 (Mouse): {Common.InputData->GetAxisInput(0)}, {Common.InputData->GetAxisInput(1)}");
        ImGui.TextUnformatted($"2 (???): {Common.InputData->GetAxisInput(2)}");

        var drawList = ImGui.GetWindowDrawList();

        var axis3 = Common.InputData->GetAxisInput(3);
        var axis4 = Common.InputData->GetAxisInput(4);
        var boxSize = ImGuiHelpers.ScaledVector2(180);
        ImGui.TextUnformatted($"3 & 4 (Movement): {axis3}, {axis4}");
        ImGui.Dummy(boxSize);
        drawList.AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0xFFFFFFFF);
        drawList.AddCircleFilled(ImGui.GetItemRectMin() + boxSize / 2 + new Vector2(axis3, -axis4) / 100 * boxSize / 2, 2, 0xFFFFFFFF);

        var axis5 = Common.InputData->GetAxisInput(5);
        var axis6 = Common.InputData->GetAxisInput(6);
        ImGui.TextUnformatted($"5 & 6 (Camera): {axis5}, {axis6}");
        ImGui.Dummy(boxSize);
        drawList.AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0xFFFFFFFF);
        drawList.AddCircleFilled(ImGui.GetItemRectMin() + boxSize / 2 + new Vector2(axis5, -axis6) / 100 * boxSize / 2, 2, 0xFFFFFFFF);

        ImGui.EndChild();

        ImGui.SameLine();

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