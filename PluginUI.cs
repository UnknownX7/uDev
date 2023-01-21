using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Utility.Signatures;
using ImGuiNET;

namespace uDev;

public static unsafe class PluginUI
{
    [Signature("E9 38 29 38")]
    private static Vector2* DefinitelyACheatPointer { get; set; }

    private static bool once = true;

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

        if (once)
        {
            DalamudApi.SigScanner.Inject(typeof(PluginUI));
            once = false;
        }

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

    public static void DrawPluginList()
    {
        ImGui.Selectable("uDev", true);
    }

    public static void DrawSignatureList()
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

            ImGui.Text(info);
            if (ImGui.IsItemClicked())
                selectedSigInfo = sigInfo;

            ImGui.TableNextColumn();
            ImGui.Text($"{sigInfo.signature} {offset}");
            ImGui.TableNextColumn();
            ImGui.Text($"{sigInfo.address:X}");
            ImGui.TableNextColumn();
            ImGui.Text($"{sigInfo.sigType}");
        }

        ImGui.EndTable();
    }

    public static void DrawSelectedSigInfo()
    {
        if (ImGui.Button("<"))
        {
            selectedSigInfo = null;
            return;
        }

        ImGui.Text($"Signature: {selectedSigInfo.signature}");
        ImGui.Text($"Address:");
        ImGui.SameLine();
        ImGuiEx.TextCopyable($"{selectedSigInfo.address:X}");
        ImGui.Text($"Type: {selectedSigInfo.sigType}");
        ImGui.Text($"Plugin: uDev");

        if (selectedSigInfo.assignableInfo is { } assignableInfo)
        {
            var memberInfo = assignableInfo.memberInfo;

            var value = assignableInfo.GetValue();
            var valueType = assignableInfo.Type;
            var valueElementType = valueType.GetElementType();
            var isPointer = valueType.IsPointer;
            var canReadMemory = isPointer && Debug.CanReadMemory(selectedSigInfo.address, Marshal.SizeOf(valueElementType!));
            var valueString = value switch
            {
                nint p => p.ToString("X"),
                _ when canReadMemory => Marshal.PtrToStructure(selectedSigInfo.address, valueElementType),
                _ => !isPointer ? value.ToString() : null
            };
            var attribute = selectedSigInfo.attribute;
            var ex = selectedSigInfo.exAttribute;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Text("Member Info");
            ImGui.Text($"{memberInfo.MemberType}: {memberInfo.DeclaringType}.{assignableInfo.Name}");
            ImGui.Text($"{valueType}: {valueString}");
            if (isPointer)
                ImGui.Text($"Can Read Memory: {canReadMemory}");

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.Text("Attribute Info");
            ImGui.Text($"Scan Type: {attribute.ScanType}");
            ImGui.Text($"Offset: {attribute.Offset}");
            ImGui.Text($"Fallibility: {attribute.Fallibility}");

            if (selectedSigInfo.sigType == SigScannerWrapper.SigInfo.SigType.Hook)
            {
                ImGui.Text($"Detour: {attribute.DetourName}");
                ImGui.Text($"Enable: {ex.EnableHook}");
                ImGui.Text($"Dispose: {ex.DisposeHook}");
            }
        }
    }
}