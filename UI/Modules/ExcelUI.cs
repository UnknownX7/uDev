using System.Collections.Generic;
using System.Reflection;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using ImGuiNET;
using Lumina.Excel;

namespace uDev.UI.Modules;

public class ExcelSheetUI : PluginUIModule
{
    public override string MenuLabel => "Excel Sheets";
    public override int MenuPriority => 10;

    private static readonly Type[] luminaTypes = Assembly.Load("Lumina.Excel").GetTypes<ExcelRow>().ToArray();
    private Type selectedLuminaType;
    private Type[] luminaTypeSearchCache = null;
    private string sheetSearch = string.Empty;
    private bool allSheetSearch = false;
    private bool setSearchFocus = false;

    public override void Draw()
    {
        var width = 200 * ImGuiHelpers.GlobalScale;
        ImGui.BeginGroup();
        ImGui.SetNextItemWidth(width);

        if (allSheetSearch)
        {
            if (ImGui.InputTextWithHint("##AllSearch", "\uE052 Contains", ref sheetSearch, 128, ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.EnterReturnsTrue))
                luminaTypeSearchCache = null;
            ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.GetColorU32(ImGuiCol.TabActive), ImGui.GetStyle().FrameRounding);
        }
        else
        {
            if (ImGui.InputTextWithHint("##Search", "\uE052 Search", ref sheetSearch, 128, ImGuiInputTextFlags.AutoSelectAll))
                luminaTypeSearchCache = null;
        }

        if (setSearchFocus)
        {
            ImGui.SetKeyboardFocusHere(-1);
            setSearchFocus = false;
        }

        if (ImGuiEx.IsItemReleased(ImGuiMouseButton.Right))
        {
            allSheetSearch ^= true;
            luminaTypeSearchCache = luminaTypes;
            sheetSearch = string.Empty;
            setSearchFocus = true;
        }

        luminaTypeSearchCache ??= allSheetSearch ? SearchAllSheets(sheetSearch) : luminaTypes.Where(t => t.Name.Contains(sheetSearch, StringComparison.CurrentCultureIgnoreCase)).ToArray();

        ImGui.BeginChild("SheetList", new Vector2(width, 0), true);
        foreach (var t in luminaTypeSearchCache.Where(t => ImGui.Selectable(t.Name, t == selectedLuminaType)))
            selectedLuminaType = t;
        ImGui.EndChild();
        ImGui.EndGroup();

        if (selectedLuminaType == null) return;
        ImGui.SameLine();
        var methodInfo = typeof(ImGuiEx).GetMethod(nameof(ImGuiEx.ExcelSheetTable))?.MakeGenericMethod(selectedLuminaType);
        methodInfo?.Invoke(null, new object[] { "ExcelSheetBrowser" });
    }

    private static Type[] SearchAllSheets(string search)
    {
        static string GetObjectAsString(object o) => o switch
        {
            ILazyRow lazyRow => $"{lazyRow.GetType().GenericTypeArguments[0].Name}#{lazyRow.Row}",
            //Lumina.Text.SeString seString => seString.ToDalamudString().ToString(),
            _ => o?.ToString() ?? string.Empty
        };

        static IEnumerable<string> GetPropertiesAsStrings(object o)
        {
            foreach (var propertyInfo in o.GetType().GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
                yield return GetObjectAsString(propertyInfo.GetValue(o));
        }

        static IEnumerable<object> GetSheet(Type t)
        {
            var methodInfo = typeof(IDataManager).GetMethod(nameof(IDataManager.GetExcelSheet), BindingFlags.Instance | BindingFlags.Public, Array.Empty<Type>())?.MakeGenericMethod(t);
            return (IEnumerable<object>)methodInfo?.Invoke(DalamudApi.DataManager, null);
        }

        return luminaTypes.Where(t => GetSheet(t) is { } e && e.Any(o => GetPropertiesAsStrings(o).Any(str => str.Contains(search)))).ToArray();
    }
}