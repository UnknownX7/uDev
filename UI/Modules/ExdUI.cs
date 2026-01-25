using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Component.Excel;
using FFXIVClientStructs.FFXIV.Component.Exd;

namespace uDev.UI.Modules;

public unsafe class ExdUI : PluginUIModule
{
    [StructLayout(LayoutKind.Explicit)]
    private struct ExcelSheet
    {
        [FieldOffset(0x0)] public FFXIVClientStructs.FFXIV.Common.Component.Excel.ExcelSheet CS;
        [FieldOffset(0x38)] public uint stringOffset;
        [FieldOffset(0x3C)] public uint rowSize;
        [FieldOffset(0xC8)] private byte hasSubrows;
        [FieldOffset(0xD4)] private byte usesIDs;

        public string Name => ((nint)CS.SheetName.Value).ReadCString();
        public bool HasSubrows => hasSubrows != 0;
        public bool UsesIDs => usesIDs != 0;
    }

    public override string MenuLabel => "Exd Module";
    public override int MenuPriority => 11;

    private static readonly uint maxSheetID = 1000u;

    private uint selectedSheet;
    private uint selectedRow;
    private string search = string.Empty;

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

    public override void Draw()
    {
        var width = 250 * ImGuiHelpers.GlobalScale;
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##Search", "Search", ref search, 128, ImGuiInputTextFlags.AutoSelectAll);
        ImGui.BeginChild("SheetList", new Vector2(width, 0), true);
        for (uint i = 0; i < maxSheetID; i++)
        {
            using var _ = ImGuiEx.IDBlock.Begin(i);
            var sheet = GetSheetPointer(i);
            if (sheet == null) continue;

            var name = $"[#{i}] {sheet->Name}";
            if (sheet->HasSubrows || GetSheetRowPointer(i, 0, sheet->UsesIDs) == null)
                name += '*';
            if (!name.Contains(search, StringComparison.CurrentCultureIgnoreCase) || !ImGui.Selectable($"{name}###Sheet", i == selectedSheet)) continue;
            selectedSheet = i;
            selectedRow = 0;
        }
        ImGui.EndChild();
        ImGui.EndGroup();

        ImGui.SameLine();

        var sheetPtr = GetSheetPointer(selectedSheet);
        var maxRow = sheetPtr->CS.RowCount;
        var stringOffset = sheetPtr->stringOffset;
        var rowSize = sheetPtr->rowSize;

        using var __ = ImGuiEx.GroupBlock.Begin();
        ImGui.BeginChild("SheetMemoryDetails", new Vector2(0, ImGui.GetContentRegionAvail().Y / 2), true);
        MemoryUI.DrawMemoryEditor(sheetPtr, sizeof(ExcelSheet));
        ImGui.EndChild();

        ImGui.SetNextItemWidth(250 * ImGuiHelpers.GlobalScale);
        var rowID = (int)selectedRow;
        if (ImGui.InputInt($"Row (Count: {maxRow})", ref rowID, 1, (int)(maxRow - 1) / 10))
            selectedRow = (uint)Math.Max(rowID, 0);

        var rowPtr = !sheetPtr->HasSubrows ? GetSheetRowPointer(selectedSheet, selectedRow, sheetPtr->UsesIDs) : null;
        if (rowPtr == null)
        {
            ImGui.TextUnformatted("Row not found!");
            return;
        }

        var row = *(nint*)rowPtr;
        if (stringOffset < rowSize)
        {
            rowSize = stringOffset;
            while (*(byte*)(row + rowSize++) != 0) ;
        }

        MemoryUI.DrawMemoryEditorChild(row, rowSize);
    }

    private static ExcelSheet* GetSheetPointer(uint sheetID) => (ExcelSheet*)ExcelModule->GetSheetByIndex(sheetID);
    private static void* GetSheetRowPointer(uint sheetID, uint row, bool useId) => useId ? ExdModule->GetRowBySheetIndexAndRowId(sheetID, row) : ExdModule->GetRowBySheetIndexAndRowIndex(sheetID, row);
}