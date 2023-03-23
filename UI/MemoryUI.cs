using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace uDev.UI;

public static unsafe class MemoryUI
{
    private class MemoryEditor
    {
        public nint Address { get; }
        public long Size { get; set; }
        public bool DrawnThisFrame { get; set; }

        private readonly bool allowExpanding;
        private long editingPosition;

        public MemoryEditor(nint address, long size, bool expand)
        {
            Address = address;
            Size = size;
            allowExpanding = expand;
        }

        public void Draw()
        {
            const int columns = 16;
            using var _ = ImGuiEx.FontBlock.Begin(UiBuilder.MonoFont);

            DrawnThisFrame = true;

            HashSet<nint> readable = null;
            using var clipper = new ImGuiEx.ListClipper((int)Size, columns);
            foreach (var i in clipper.Rows)
            {
                if (clipper.IsStepped)
                    readable = Debug.GetReadableMemory(Address + i, Math.Min((clipper.DisplayEnd - clipper.DisplayStart + 1) * columns, Size - i));

                var lineAddr = Address + i;
                var color = lineAddr switch
                {
                    _ when lineAddr >= DalamudApi.SigScanner.BaseRDataAddress => new Vector4(0.5f, 1, 0.5f, 1),
                    _ when lineAddr >= DalamudApi.SigScanner.BaseTextAddress => new Vector4(1, 1, 0.5f, 1),
                    _ => new Vector4(0.6f, 0.6f, 0.7f, 1)
                };

                ImGuiEx.TextCopyable(color, lineAddr.ToString("X"));
                ImGui.SameLine();

                var str = string.Empty;
                foreach (var j in clipper.Columns)
                {
                    var pos = i + j;
                    var ptrAddr = Address + pos;
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

                        ImGui.TextColored(b != 0 ? Vector4.One : new Vector4(0.5f, 0.5f, 0.5f, 1), b.ToString("X2"));

                        if (maxLength >= 8 && ImGuiEx.IsItemReleased(ImGuiMouseButton.Right))
                        {
                            var a = *(nint*)ptr;
                            if (Debug.CanReadMemory(a))
                                AddPopoutMemoryEditor(a);
                        }

                        if (ImGui.IsItemHovered())
                            ImGui.SetTooltip($"0x{pos:X}\n{GetPointerTooltip(ptr, maxLength)}");

                        if (b > 31)
                            str += (char)b;
                        else
                            str += ".";
                    }
                    else
                    {
                        ImGui.TextColored(new Vector4(0.75f, 0.5f, 0.5f, 1), "??");
                        str += " ";
                    }

                    ImGui.SameLine();

                    if (j == columns - 1 || (j + 1) % 8 != 0) continue;
                    ImGui.TextUnformatted("|");
                    ImGui.SameLine();
                }

                ImGui.TextUnformatted($" {str}");
            }

            if (allowExpanding && ImGui.GetScrollY() == ImGui.GetScrollMaxY())
                Size += 0x200;
        }

        public void DrawAsChild()
        {
            ImGui.BeginChild($"Memory Details {Address:X}##Child", Vector2.Zero, true);
            Draw();
            ImGui.EndChild();
        }

        public void DrawAsWindow()
        {
            var visible = true;
            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(700, 500));
            ImGui.Begin($"Memory Details {Address:X}##Window", ref visible, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings);
            Draw();
            ImGui.End();
            if (!visible)
                DrawnThisFrame = false;
        }

        private static string GetPointerTooltip(byte* ptr, long maxLength)
        {
            var b = *ptr;
            var tooltip = $"Byte: {*(sbyte*)ptr} | {b}";

            if (b > 1)
            {
                tooltip += " (0b";
                for (int i = 7; i >= 0; i--)
                    tooltip += (b >> i) & 1;
                tooltip += ')';
            }

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

    private static readonly List<MemoryEditor> popoutEditors = new();
    private static readonly Dictionary<nint, MemoryEditor> inlineEditors = new();

    public static void DrawMemoryEditors()
    {
        for (int i = popoutEditors.Count - 1; i >= 0; i--)
        {
            var editor = popoutEditors[i];
            editor.DrawAsWindow();
            if (!editor.DrawnThisFrame)
                popoutEditors.RemoveAt(i);
        }

        HashSet<nint> remove = null;
        foreach (var (address, memoryEditor) in inlineEditors)
        {
            if (memoryEditor.DrawnThisFrame)
                memoryEditor.DrawnThisFrame = false;
            else
                (remove ??= new()).Add(address);
        }

        if (remove == null) return;
        foreach (var address in remove)
            inlineEditors.Remove(address);
    }

    public static void AddPopoutMemoryEditor(nint address)
    {
        if (popoutEditors.All(v => v.Address != address))
            popoutEditors.Add(new MemoryEditor(address, 0x200, true));
    }

    private static MemoryEditor GetMemoryEditor(nint address, long length, bool allowExpanding)
    {
        if (inlineEditors.TryGetValue(address, out var editor)) return editor;
        editor = new MemoryEditor(address, length, allowExpanding);
        inlineEditors[address] = editor;
        return editor;
    }

    public static void DrawMemoryEditor(nint address, long length, bool allowExpanding = false) => GetMemoryEditor(address, length, allowExpanding).Draw();
    public static void DrawMemoryEditor(void* ptr, long length, bool allowExpanding = false) => DrawMemoryEditor((nint)ptr, length, allowExpanding);
    public static void DrawMemoryEditorChild(nint address, long length, bool allowExpanding = false) => GetMemoryEditor(address, length, allowExpanding).DrawAsChild();
    public static void DrawMemoryEditorChild(void* ptr, long length, bool allowExpanding = false) => DrawMemoryEditorChild((nint)ptr, length, allowExpanding);
}