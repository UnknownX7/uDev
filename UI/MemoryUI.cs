using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace uDev.UI;

public static unsafe class MemoryUI
{
    private class MemoryView
    {
        public nint Address { get; }
        public long Size { get; set; }
        public MemoryView(nint address, long size)
        {
            Address = address;
            Size = size;
        }
    }

    private static readonly List<MemoryView> displayedMemoryViews = new();

    public static void DrawMemoryViews()
    {
        for (int i = 0; i < displayedMemoryViews.Count; i++)
            DrawMemoryDetailsWindow(displayedMemoryViews[i]);
    }

    public static void AddMemoryView(nint address)
    {
        if (displayedMemoryViews.All(v => v.Address != address))
            displayedMemoryViews.Add(new MemoryView(address, 0x200));
    }

    public static void DrawMemoryDetails(nint address, long length)
    {
        const int columns = 16;
        using var _ = ImGuiEx.FontBlock.Begin(UiBuilder.MonoFont);

        HashSet<nint> readable = null;
        using var clipper = new ImGuiEx.ListClipper((int)length, columns);
        foreach (var i in clipper.Rows)
        {
            if (clipper.IsStepped)
                readable = Debug.GetReadableMemory(address + i, Math.Min((clipper.DisplayEnd - clipper.DisplayStart + 1) * columns, length - i));

            ImGuiEx.TextCopyable(new Vector4(0.5f, 0.5f, 0.5f, 1), (address + i).ToString("X"));
            ImGui.SameLine();

            var str = string.Empty;
            foreach (var j in clipper.Columns)
            {
                var pos = i + j;
                var ptrAddr = address + pos;
                var ptr = (byte*)ptrAddr;

                if (readable!.Contains(ptrAddr))
                {
                    var b = *ptr;

                    // It works I guess...
                    var maxLength = ptrAddr switch
                    {
                        _ when readable.Contains(ptrAddr + 8) => 8,
                        _ when readable.Contains(ptrAddr + 4) => 4,
                        _ when readable.Contains(ptrAddr + 2) => 2,
                        _ => 1
                    };

                    var color = ptrAddr switch
                    {
                        _ when ptrAddr >= DalamudApi.SigScanner.BaseRDataAddress => new Vector4(0.5f, 1, 0.5f, 1),
                        _ when ptrAddr >= DalamudApi.SigScanner.BaseTextAddress => new Vector4(1, 1, 0.5f, 1),
                        _ => Vector4.One
                    };

                    ImGui.TextColored(color, b.ToString("X2"));

                    if (maxLength >= 8 && ImGuiEx.IsItemReleased(ImGuiMouseButton.Right))
                    {
                        var a = *(nint*)ptr;
                        if (Debug.CanReadMemory(a, 1))
                            AddMemoryView(a);
                    }

                    ImGuiEx.SetItemTooltip($"0x{pos:X}\n{GetPointerTooltip(ptr, maxLength)}");

                    if (b > 31)
                        str += (char)b;
                    else
                        str += ".";
                }
                else
                {
                    ImGui.TextUnformatted("??");
                    str += " ";
                }

                ImGui.SameLine();

                if (j == columns - 1 || (j + 1) % 8 != 0) continue;
                ImGui.TextUnformatted("|");
                ImGui.SameLine();
            }

            ImGui.TextUnformatted($" {str}");
        }
    }

    private static void DrawMemoryDetailsWindow(MemoryView view)
    {
        var visible = true;
        ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(700, 500));
        ImGui.Begin($"Memory Details {view.Address:X}", ref visible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings);
        DrawMemoryDetails(view.Address, view.Size);
        if (ImGui.GetScrollY() == ImGui.GetScrollMaxY())
            view.Size += 0x200;
        ImGui.End();
        if (!visible)
            displayedMemoryViews.Remove(view);
    }

    private static string GetPointerTooltip(byte* ptr, long maxLength)
    {
        var tooltip = $"Byte: {*(sbyte*)ptr} | {*ptr}";
        if (maxLength < 2) return tooltip;
        tooltip += $"\nShort: {*(short*)ptr} | {*(ushort*)ptr}";
        if (maxLength < 4) return tooltip;
        tooltip += $"\nInt: {*(int*)ptr} | {*(uint*)ptr}";
        tooltip += $"\nFloat: {*(float*)ptr}";
        if (maxLength < 8) return tooltip;
        tooltip += $"\nLong: {*(long*)ptr} | {*(ulong*)ptr}";
        tooltip += $"\nIntPtr: {*(nint*)ptr:X} | {*(nuint*)ptr:X}";
        tooltip += $"\nDouble: {*(double*)ptr}";
        return tooltip;
    }
}