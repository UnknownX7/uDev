using System;
using System.Numerics;
using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Component.Excel;
using FFXIVClientStructs.FFXIV.Component.Exd;
using ImGuiNET;

namespace uDev.UI;

public static unsafe class ExdUI
{
    private static readonly uint maxSheetID = 1000u;
    private static uint selectedSheet;
    private static uint selectedRow;
    private static string search = string.Empty;

    private static ExdModule* ExdModule => Framework.Instance()->ExdModule;
    private static ExcelModule* ExcelModule => ExdModule->ExcelModule;

    static ExdUI()
    {
        while (true)
        {
            var sheet = ExcelModule->GetSheetByIndex(maxSheetID);
            if (sheet != null && (!Debug.CanReadMemory(sheet) || !Debug.CanReadMemory(sheet->SheetName))) break;
            maxSheetID++;
        }
    }

    public static void Draw()
    {
        var sheetPtr = GetSheetPointer(selectedSheet);
        var rowSize = *(uint*)((nint)sheetPtr + 0x3C);
        var maxRow = sheetPtr->RowCount;

        var width = 250 * ImGuiHelpers.GlobalScale;
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##Search", "Search", ref search, 128, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.BeginChild("SheetList", new Vector2(width, 0), true);
        for (uint i = 0; i < maxSheetID; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var sheet = ExcelModule->GetSheetByIndex(i);
            if (sheet == null) continue;

            var name = $"[#{i}] {((nint)sheet->SheetName).ReadCString()}";
            if (!name.Contains(search, StringComparison.CurrentCultureIgnoreCase) || !ImGui.Selectable(name, i == selectedSheet)) continue;
            selectedSheet = i;
            selectedRow = 0;
        }
        ImGui.EndChild();
        ImGui.EndGroup();

        ImGui.SameLine();

        using var __ = ImGuiEx.GroupBlock.Begin();
        ImGui.BeginChild("SheetMemoryDetails", new Vector2(0, ImGui.GetContentRegionAvail().Y / 2), true);
        MemoryUI.DrawMemoryEditor(sheetPtr, sizeof(ExcelSheet));
        ImGui.EndChild();

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        var row = (int)selectedRow;
        if (ImGui.InputInt($"Row (Max: {maxRow - 1})", ref row, 1, (int)(maxRow - 1) / 10))
            selectedRow = (uint)Math.Min(Math.Max(row, 0), maxRow - 1);

        var rowPtr = GetSheetRowPointer(selectedSheet, selectedRow);
        if (rowPtr == null)
        {
            ImGui.TextUnformatted("Sheet not found!");
            return;
        }

        MemoryUI.DrawMemoryEditorChild(*(void**)rowPtr, rowSize);
    }

    private static ExcelSheet* GetSheetPointer(uint sheetID) => ExcelModule->GetSheetByIndex(sheetID);

    private static void* GetSheetRowPointer(uint sheetID, uint row) => ExdModule->GetEntryByIndex(sheetID, row);
}