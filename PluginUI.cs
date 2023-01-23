using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.Interop;
using ImGuiNET;
using Newtonsoft.Json.Linq;
using static Hypostasis.Util;

namespace uDev;

public static unsafe class PluginUI
{
    private const BindingFlags defaultBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const MemberTypes whitelistedMemberTypes = MemberTypes.Field | MemberTypes.Property;

    private class MemberDetails
    {
        public object Value { get; }
        public Type Type { get; }
        public Type BoxedType { get; }
        public bool IsPointer { get; }
        public bool CanReadMemory { get; }
        public object Struct { get; }
        public bool ShouldDrawStruct { get; }
        public bool IsArray { get; }
        public int ArrayLength { get; }
        public string ValueString => Value switch
        {
            nint p => p.ToString("X"),
            _ when !IsPointer => Value?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        public MemberDetails(MemberInfo memberInfo, object o)
        {
            switch (memberInfo)
            {
                case FieldInfo f:
                    Value = f.GetValue(o);
                    Type = f.FieldType;
                    break;
                case PropertyInfo p:
                    IsArray = p.GetIndexParameters().Length > 0;
                    if (IsArray)
                    {
                        var array = new List<object>();
                        try
                        {
                            var oType = o.GetType();
                            var countMember = oType.GetMember("Count", defaultBindingFlags | BindingFlags.IgnoreCase)
                                .Concat(oType.GetMember("Length", defaultBindingFlags | BindingFlags.IgnoreCase))
                                .First();
                            var countInfo = new AssignableInfo(o, countMember);
                            var count = (int)countInfo.GetValue();
                            ArrayLength = count;
                            for (int i = 0; i < count; i++)
                                array.Add(p.GetValue(o, new object[] { i }));
                        }
                        catch { }

                        Value = array;
                    }
                    else
                    {
                        Value = p.GetValue(o);
                    }
                    Type = p.PropertyType;
                    break;
                case MethodInfo m:
                    Value = m;
                    Type = m.ReturnType;
                    return;
                default:
                    break;
            }

            BoxedType = Type?.GetElementType();
            IsPointer = Type?.IsPointer ?? false;
            CanReadMemory = IsPointer && BoxedType != null && Debug.CanReadMemory(selectedSigInfo.address, Marshal.SizeOf(BoxedType));
            Struct = CanReadMemory ? Marshal.PtrToStructure(selectedSigInfo.address, BoxedType!) : Value;
            ShouldDrawStruct = Struct?.GetType() is { IsValueType: true, IsEnum: false } && Struct is not IComparable;
        }
    }

    private static bool isVisible = true;
    private static SigScannerWrapper.SigInfo selectedSigInfo = null;

    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    public static void Draw()
    {
        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(new Vector2(1000, 500) * ImGuiHelpers.GlobalScale, new Vector2(9999));
        ImGui.Begin("uDev Configuration", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGuiEx.AddDonationHeader(2);

        ImGui.BeginChild("PluginList", new Vector2(150 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
        DrawPluginList();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("SignatureInfo");
        if (selectedSigInfo == null)
            DrawSignatureList();
        else
            DrawSelectedSigInfo();
        ImGui.EndChild();


        ImGui.End();
    }

    private static void DrawPluginList()
    {
        ImGui.Selectable("Fix Me", true);
    }

    private static void DrawSignatureList()
    {
        if (!ImGui.BeginTable("SignatureInfoTable", 4, ImGuiTableFlags.Borders)) return;

        ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.None, 0.5f);
        ImGui.TableSetupColumn("Signature", ImGuiTableColumnFlags.None, 1);
        ImGui.TableSetupColumn("Address", ImGuiTableColumnFlags.None, 0.3f);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.2f);
        ImGui.TableHeadersRow();

        foreach (var sigInfo in DalamudApi.SigScanner.SigInfos)
        {
            var info = sigInfo.assignableInfo?.Name ?? string.Empty;
            var offset = sigInfo.offset != 0 ? $"({sigInfo.offset})" : string.Empty;

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            ImGui.TextUnformatted(info);
            if (ImGui.IsItemClicked())
                selectedSigInfo = sigInfo;

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{sigInfo.signature} {offset}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{sigInfo.address:X}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{sigInfo.sigType}");
        }

        ImGui.EndTable();
    }

    private static void DrawSelectedSigInfo()
    {
        if (ImGui.Button("<"))
        {
            selectedSigInfo = null;
            return;
        }

        ImGui.TextUnformatted($"Signature: {selectedSigInfo.signature}");
        ImGui.TextUnformatted($"Address:");
        ImGui.SameLine();
        ImGuiEx.TextCopyable($"{selectedSigInfo.address:X}");
        ImGui.TextUnformatted($"Type: {selectedSigInfo.sigType}");

        if (selectedSigInfo.assignableInfo is not { } assignableInfo) return;
        var memberInfo = assignableInfo.memberInfo;
        var memberDetails = new MemberDetails(memberInfo, assignableInfo.obj);

        if (selectedSigInfo.sigAttribute != null)
        {
            var attribute = selectedSigInfo.sigAttribute;
            var ex = selectedSigInfo.exAttribute;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Member Info");
            ImGui.TextUnformatted($"{memberInfo.MemberType}: {memberInfo.DeclaringType}.{assignableInfo.Name}");
            ImGui.TextUnformatted($"{memberDetails.Type}: {memberDetails.ValueString}");
            if (memberDetails.IsPointer)
                ImGui.TextUnformatted($"Can Read Memory: {memberDetails.CanReadMemory}");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Attribute Info");
            ImGui.TextUnformatted($"Scan Type: {attribute.ScanType}");
            ImGui.TextUnformatted($"Offset: {attribute.Offset}");
            ImGui.TextUnformatted($"Fallibility: {attribute.Fallibility}");

            if (selectedSigInfo.sigType == SigScannerWrapper.SigInfo.SigType.Hook)
            {
                ImGui.TextUnformatted($"Detour: {attribute.DetourName}");
                ImGui.TextUnformatted($"Enable: {ex.EnableHook}");
                ImGui.TextUnformatted($"Dispose: {ex.DisposeHook}");
            }

        }

        if (!memberDetails.ShouldDrawStruct || !ImGui.BeginTabBar("DetailsTabBar")) return;
        if (ImGui.BeginTabItem("Structure Details"))
        {
            ImGui.BeginChild("StructureDetails", ImGui.GetContentRegionAvail(), true);
            DrawStructureDetails(memberDetails.Struct);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }

        if (memberDetails.IsPointer && ImGui.BeginTabItem("Memory Details"))
        {
            ImGui.BeginChild("MemoryDetails", ImGui.GetContentRegionAvail(), true);
            DrawMemoryDetails(selectedSigInfo.address, Marshal.SizeOf(memberDetails.BoxedType));
            ImGui.EndChild();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private static void DrawStructureDetails(object s, bool isArray = false)
    {
        foreach (var memberInfo in s.GetType().GetMembers(defaultBindingFlags))
        {
            if ((memberInfo.MemberType & whitelistedMemberTypes) == 0) continue;
            var memberDetails = new MemberDetails(memberInfo, s);

            var open = false;
            if (memberDetails.ShouldDrawStruct)
            {
                open = ImGui.TreeNodeEx($"##{memberInfo.Name}", ImGuiTreeNodeFlags.AllowItemOverlap | ImGuiTreeNodeFlags.SpanAvailWidth);
                ImGui.SameLine();
            }

            var offsetAttribute = memberInfo.GetCustomAttribute<FieldOffsetAttribute>();
            if (offsetAttribute != null)
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), $"[0x{offsetAttribute.Value:X}]");
                ImGui.SameLine();
            }
            ImGui.TextColored(new Vector4(0.25f, 1, 0.5f, 1), memberInfo.MemberType.ToString());
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.25f, 0.5f, 1, 1), memberDetails.Type?.Name ?? string.Empty);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), !memberDetails.IsArray ? $"{memberInfo.Name}:" : $"{memberInfo.Name}[{memberDetails.ArrayLength}]");
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(1, 1, 1, 1), memberDetails.ValueString);
            if (memberDetails.Type == typeof(long))
                ImGuiEx.SetItemTooltip($"{memberDetails.Value:X}");

            if (!open) continue;
            DrawStructureDetails(memberDetails.Struct, memberDetails.IsArray);
            ImGui.TreePop();
        }
    }

    private static void DrawMemoryDetails(nint address, long length)
    {
        ImGui.PushFont(UiBuilder.MonoFont);

        const int columns = 16;
        for (int i = 0; i < length; i += columns)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), (address + i).ToString("X"));
            ImGui.SameLine();

            var str = string.Empty;
            for (int j = 0; j < columns; j++)
            {
                if (i + j >= length) break;
                var ptr = (byte*)address + i + j;
                var b = *ptr;
                ImGui.TextUnformatted(b.ToString("X2"));
                ImGuiEx.SetItemTooltip(GetPointerTooltip(ptr, length - i - j));
                ImGui.SameLine();

                if (b > 31)
                    str += (char)b;
                else
                    str += ".";

                if (j == columns - 1 || (j + 1) % 8 != 0) continue;
                ImGui.TextUnformatted("|");
                ImGui.SameLine();
            }

            ImGui.TextUnformatted($" {str}");
        }

        ImGui.PopFont();
    }

    private static string GetPointerTooltip(byte* ptr, long maxLength)
    {
        var tooltip = $"Byte: {*ptr} | {*(sbyte*)ptr}";
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