using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Plugin.Ipc;
using ImGuiNET;
using static Hypostasis.Util;

namespace uDev;

public static unsafe class PluginUI
{
    private const BindingFlags defaultBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const MemberTypes whitelistedMemberTypes = MemberTypes.Field | MemberTypes.Property;

    private class PluginIPC //: IDisposable
    {
        public string Name { get; }
        private ICallGateSubscriber<List<SigScannerWrapper.SigInfo>> GetSigInfosSubscriber { get; }
        private ICallGateSubscriber<Dictionary<int, (object, MemberInfo)>> GetMemberInfosSubscriber { get; }
        public List<SigScannerWrapper.SigInfo> SigInfos
        {
            get
            {
                var sigInfos = GetSigInfosSubscriber.InvokeFunc();
                var memberInfos = GetMemberInfosSubscriber.InvokeFunc();
                for (int i = 0; i < sigInfos.Count; i++)
                {
                    if (!memberInfos.TryGetValue(i, out var memberInfo)) continue;
                    sigInfos[i].assignableInfo = new(memberInfo.Item1, memberInfo.Item2);
                }
                return sigInfos;
            }
        }

        public PluginIPC(string name)
        {
            Name = name;
            GetSigInfosSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<List<SigScannerWrapper.SigInfo>>($"{name}.Hypostasis.GetSigInfos");
            GetMemberInfosSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<Dictionary<int, (object, MemberInfo)>>($"{name}.Hypostasis.GetMemberInfos");
        }
        //public void Dispose() { }
    }

    private class MemberDetails
    {
        public object Value { get; }
        public Type Type { get; }
        public bool IsPointer { get; }
        public Type BoxedType { get; }
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

            IsPointer = Type?.IsPointer ?? false;
            BoxedType = Type?.GetElementType();

            if (IsPointer && BoxedType != null)
            {
                var address = (nint)Pointer.Unbox((Pointer)Value!);
                CanReadMemory = Debug.CanReadMemory(address, Marshal.SizeOf(BoxedType));

                // Thanks void and void* and void** and so on...
                try
                {
                    Struct = Marshal.PtrToStructure(address, BoxedType);
                }
                catch { }
            }
            else
            {
                Struct = Value;
            }

            ShouldDrawStruct = Struct?.GetType() is { IsValueType: true, IsEnum: false } && Struct is not IComparable;
        }
    }

    private class MemoryView
    {
        public nint Address { get; set; }
        public long Size { get; set; }
        public MemoryView(nint address, long size)
        {
            Address = address;
            Size = size;
        }
    }

    private static readonly List<MemoryView> displayedMemoryViews = new();
    private static bool isVisible = true;
    private static SigScannerWrapper.SigInfo selectedSigInfo = null;
    private static PluginIPC selectedPlugin = null;
    private static readonly Dictionary<string, PluginIPC> plugins = new()
    {
        ["ReAction"] = new("ReAction"),
        ["uDev"] = new("uDev")
    };

    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    public static void Draw()
    {
        for (int i = 0; i < displayedMemoryViews.Count; i++)
            DrawMemoryDetailsWindow(displayedMemoryViews[i]);

        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(ImGuiHelpers.ScaledVector2(1000, 650), new Vector2(9999));
        ImGui.Begin("uDev", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGuiEx.AddDonationHeader(2);

        ImGui.BeginChild("PluginList", new Vector2(150 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
        DrawPluginList();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("SignatureInfo");
        if (selectedSigInfo != null)
            DrawSelectedSigInfo();
        else if (selectedPlugin != null)
            DrawSignatureList();
        ImGui.EndChild();


        ImGui.End();
    }

    private static void DrawPluginList()
    {
        foreach (var (name, ipc) in plugins)
        {
            if (!ImGui.Selectable(name, name == selectedPlugin?.Name)) continue;
            selectedPlugin = ipc;
            selectedSigInfo = null;
        }
    }

    private static void DrawSignatureList()
    {
        if (!ImGui.BeginTable("SignatureInfoTable", 4, ImGuiTableFlags.Borders)) return;

        ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.None, 0.5f);
        ImGui.TableSetupColumn("Signature", ImGuiTableColumnFlags.None, 1);
        ImGui.TableSetupColumn("Address", ImGuiTableColumnFlags.None, 0.3f);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.2f);
        ImGui.TableHeadersRow();

        foreach (var sigInfo in selectedPlugin.SigInfos)
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
        if (ImGuiEx.FontButton(FontAwesomeIcon.ArrowLeft.ToIconString(), UiBuilder.IconFont))
        {
            selectedSigInfo = null;
            return;
        }

        ImGui.TextUnformatted($"Signature: {selectedSigInfo.signature}");
        ImGui.TextUnformatted("Address:");
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
            var indent = 0;
            if (memberDetails.ShouldDrawStruct)
            {
                open = ImGui.TreeNodeEx($"##{memberInfo.Name}", ImGuiTreeNodeFlags.AllowItemOverlap | ImGuiTreeNodeFlags.SpanAvailWidth);
                ImGui.SameLine();
            }
            else
            {
                indent = (int)(ImGui.GetFontSize() + ImGui.GetStyle().ItemSpacing.X);
                ImGui.Indent(indent);
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

            if (indent > 0)
                ImGui.Unindent(indent);

            if (!open) continue;
            DrawStructureDetails(memberDetails.Struct, memberDetails.IsArray);
            ImGui.TreePop();
        }
    }

    private static void DrawMemoryDetails(nint address, long length)
    {
        ImGui.PushFont(UiBuilder.MonoFont);

        const int columns = 16;

        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin((int)MathF.Ceiling(length / (float)columns), ImGui.GetFontSize() + ImGui.GetStyle().ItemSpacing.Y);

        var readable = Debug.GetReadableMemory(address, length);
        while (clipper.Step())
        {
            for (int row = clipper.DisplayStart; row < clipper.DisplayEnd; row++)
            {
                var i = row * columns;
                ImGuiEx.TextCopyable(new Vector4(0.5f, 0.5f, 0.5f, 1), (address + i).ToString("X"));
                ImGui.SameLine();

                var str = string.Empty;
                for (int j = 0; j < columns; j++)
                {
                    var pos = i + j;
                    if (pos >= length) break;
                    var ptrAddr = address + pos;
                    var ptr = (byte*)ptrAddr;

                    if (readable.Contains(ptrAddr))
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

                        ImGui.TextUnformatted(b.ToString("X2"));

                        if (maxLength >= 8 && ImGuiEx.IsItemReleased(ImGuiMouseButton.Right))
                        {
                            var a = *(nint*)ptr;
                            if (Debug.CanReadMemory(a, 1) && displayedMemoryViews.All(v => v.Address != a))
                                displayedMemoryViews.Add(new MemoryView(a, 0x200));
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

        ImGui.PopFont();
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