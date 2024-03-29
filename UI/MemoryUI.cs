using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
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
        private long editingPosition = -1;
        private bool setFocus = false;
        private bool typingMode = false;
        private bool displayProcessModuleOffset = false;
        private float windowWidth = 600;

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
            using var __ = ImGuiEx.StyleVarBlock.Begin(ImGuiStyleVar.FramePadding, new Vector2(0));
            using var ___ = ImGuiEx.StyleVarBlock.Begin(ImGuiStyleVar.ItemSpacing, new Vector2(4));

            DrawnThisFrame = true;

            var startingEditingPosition = editingPosition;
            var startingEditingRow = editingPosition / columns;
            if (editingPosition >= 0)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.UpArrow) && editingPosition >= columns)
                {
                    editingPosition -= columns;
                    setFocus = true;
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.DownArrow) && editingPosition < Size - columns)
                {
                    editingPosition += columns;
                    setFocus = true;
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow) && editingPosition > 0)
                {
                    editingPosition--;
                    setFocus = true;
                }
                else if (ImGui.IsKeyPressed(ImGuiKey.RightArrow) && editingPosition < Size - 1)
                {
                    editingPosition++;
                    setFocus = true;
                }

                if (ImGui.IsKeyPressed(ImGuiKey.ModAlt, false))
                    typingMode ^= true;
            }

            var style = ImGui.GetStyle();
            var itemSpacingX = style.ItemSpacing.X;
            var spaceX = ImGui.CalcTextSize(" ").X;
            var bytesX = ImGui.CalcTextSize((Address + Size).ToString("X")).X + spaceX * 2 + itemSpacingX;
            var stringX = bytesX + (Address % 8 == 0 ? 1 : 2) * (spaceX + itemSpacingX) + (spaceX * 2 + itemSpacingX) * columns;

            if (ImGui.IsWindowAppearing())
                windowWidth = stringX + spaceX * columns + style.ScrollbarSize + style.WindowPadding.X * 2;

            var separators = new List<float>();
            HashSet<nint> readable = null;
            using var clipper = new ImGuiEx.ListClipper((int)Size, columns);
            foreach (var i in clipper.Rows)
            {
                if (clipper.IsStepped)
                    readable = Debug.GetReadableMemory(Address + i, Math.Min((clipper.DisplayEnd - clipper.DisplayStart + 1) * columns, Size - i));

                var lineAddr = Address + i;
                ImGuiEx.TextCopyable(lineAddr switch
                {
                    _ when lineAddr >= DalamudApi.SigScanner.BaseRDataAddress => new Vector4(0.5f, 1, 0.5f, 1),
                    _ when lineAddr >= DalamudApi.SigScanner.BaseTextAddress => new Vector4(1, 1, 0.5f, 1),
                    _ => new Vector4(0.6f, 0.6f, 0.7f, 1)
                }, displayProcessModuleOffset && lineAddr >= DalamudApi.SigScanner.BaseAddress ? $"+{lineAddr - DalamudApi.SigScanner.BaseAddress:X11}" : lineAddr.ToString("X"));
                if (ImGuiEx.IsItemReleased(ImGuiMouseButton.Right))
                    displayProcessModuleOffset ^= true;

                ImGui.SameLine(bytesX);

                var str = string.Empty;
                foreach (var j in clipper.Columns)
                {
                    var pos = i + j;
                    var ptrAddr = Address + pos;
                    var ptr = (byte*)ptrAddr;

                    if (readable!.Contains(ptrAddr))
                    {
                        var b = *ptr;

                        if (pos == editingPosition)
                        {
                            var input = b.ToString("X2");
                            var cursorPos = -1;

                            using (ImGuiEx.ItemWidthBlock.Begin(ImGui.CalcTextSize("  ").X))
                            {
                                var flags = ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.CallbackEdit | ImGuiInputTextFlags.NoHorizontalScroll | ImGuiInputTextFlags.AlwaysOverwrite;
                                byte inputByte = 0;

                                if (!typingMode)
                                    flags |= ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase;

                                if (ImGui.InputText($"##{pos}", ref input, 2, flags,
                                    data =>
                                    {
                                        if (typingMode && data->BufTextLen > 0)
                                        {
                                            inputByte = *data->Buf;
                                            *(int*)data->UserData = 2;
                                        }
                                        else if (data->SelectionStart == data->SelectionEnd)
                                        {
                                            *(int*)data->UserData = data->CursorPos;
                                        }
                                        return 0;
                                    }, (nint)(&cursorPos)) || cursorPos >= 2)
                                {
                                    SafeMemory.Write(ptrAddr, typingMode ? inputByte : byte.Parse(input, NumberStyles.HexNumber));
                                    editingPosition++;
                                    setFocus = true;
                                }
                                else if (cursorPos == 0 && ImGui.IsKeyPressed(ImGuiKey.Backspace))
                                {
                                    SafeMemory.Write<byte>(ptrAddr, 0);
                                    editingPosition--;
                                    setFocus = true;
                                }
                                else if (!setFocus && !ImGui.IsItemActive())
                                {
                                    editingPosition = -1;
                                    typingMode = false;
                                }
                            }

                            if (typingMode)
                                ImGui.GetWindowDrawList().AddRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), 0xFF00FF00);

                            if (setFocus && pos == editingPosition)
                            {
                                ImGui.SetKeyboardFocusHere(-1);
                                setFocus = false;
                            }
                        }
                        else
                        {
                            // It works I guess...
                            var maxLength = ptrAddr switch
                            {
                                _ when readable.Contains(ptrAddr + 8) => 8,
                                _ when readable.Contains(ptrAddr + 4) => 4,
                                _ when readable.Contains(ptrAddr + 2) => 2,
                                _ => 1
                            };

                            ImGui.TextColored(b != 0 ? Vector4.One : new Vector4(0.5f, 0.5f, 0.5f, 1), b.ToString("X2"));

                            if (ImGui.IsItemHovered())
                            {
                                ImGui.SetTooltip($"0x{pos:X}\n{GetPointerTooltip(ptr, maxLength)}");

                                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                                {
                                    editingPosition = pos;
                                    setFocus = true;
                                }
                                else if (maxLength >= 8 && ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                                {
                                    var a = *(nint*)ptr;
                                    if (Debug.CanReadMemory(a))
                                        AddPopoutMemoryEditor(a);
                                }
                            }
                        }

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

                    if ((ptrAddr + 1) % 8 != 0 || j == columns - 1 || pos == Size - 1) continue;

                    ImGui.TextUnformatted(" ");
                    ImGui.SameLine();
                    if (clipper.DisplayStart == 0)
                        separators.Add(ImGui.GetCursorPosX() - itemSpacingX - spaceX / 2);
                }

                ImGui.SameLine(stringX);
                ImGui.TextUnformatted($" {str}");
            }

            foreach (var separatorX in separators)
            {
                var windowPos = ImGui.GetWindowPos();
                var x = separatorX - itemSpacingX / 2 + 1;
                ImGui.GetWindowDrawList().AddLine(new Vector2(x, 0) + windowPos, new Vector2(x, ImGui.GetWindowHeight()) + windowPos, 0xFF505050, 2);
            }

            if ((editingPosition >= Size && editingPosition == startingEditingPosition) || !ImGui.IsWindowFocused())
            {
                editingPosition = -1;
                setFocus = false;
                typingMode = false;
            }
            else if (editingPosition >= 0 && startingEditingRow >= 0)
            {
                var editingRow = editingPosition / columns;
                var delta = editingRow - startingEditingRow;
                if ((delta < 0 && editingRow < clipper.FirstRow) || (delta > 0 && editingRow > clipper.LastRow))
                    ImGui.SetScrollY(ImGui.GetScrollY() + delta * ImGui.GetFrameHeightWithSpacing());
            }

            if (allowExpanding && (ImGui.GetScrollY() == ImGui.GetScrollMaxY() || editingPosition >= Size - columns))
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
            ImGui.SetNextWindowSize(ImGuiHelpers.ScaledVector2(windowWidth, 500));
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
        if (inlineEditors.TryGetValue(address, out var editor))
        {
            if (!allowExpanding)
                editor.Size = length;
            return editor;
        }

        editor = new MemoryEditor(address, length, allowExpanding);
        inlineEditors[address] = editor;
        return editor;
    }

    public static void DrawMemoryEditor(nint address, long length, bool allowExpanding = false) => GetMemoryEditor(address, length, allowExpanding).Draw();
    public static void DrawMemoryEditor(void* ptr, long length, bool allowExpanding = false) => DrawMemoryEditor((nint)ptr, length, allowExpanding);
    public static void DrawMemoryEditorChild(nint address, long length, bool allowExpanding = false) => GetMemoryEditor(address, length, allowExpanding).DrawAsChild();
    public static void DrawMemoryEditorChild(void* ptr, long length, bool allowExpanding = false) => DrawMemoryEditorChild((nint)ptr, length, allowExpanding);
}