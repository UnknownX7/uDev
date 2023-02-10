using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;

namespace uDev.UI;

public static class MainUI
{
    private static bool isVisible = true;
    private static SigScannerWrapper.SignatureInfo selectedSigInfo = null;
    private static Debug.PluginIPC selectedPlugin = null;
    private static readonly Dictionary<string, Debug.PluginIPC> plugins = new();

    public static bool IsVisible
    {
        get => isVisible;
        set => isVisible = value;
    }

    public static void Draw()
    {
        MemoryUI.DrawMemoryViews();

        if (!isVisible) return;

        ImGui.SetNextWindowSizeConstraints(ImGuiHelpers.ScaledVector2(1000, 650), new Vector2(9999));
        ImGui.Begin("uDev", ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGuiEx.AddDonationHeader(2);

        if (ImGui.BeginTabBar("MainViewTabBar"))
        {
            if (ImGui.BeginTabItem("Browse Plugins"))
            {
                ImGui.BeginChild("PluginList", new Vector2(150 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
                DrawPluginList();
                ImGui.EndChild();

                ImGui.SameLine();

                ImGui.BeginChild("PluginView");
                DrawPluginView();
                ImGui.EndChild();

                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Signature Test"))
            {
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Hook Test"))
            {
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawPluginList()
    {
        var pluginNames = DalamudApi.PluginInterface.GetData<HashSet<string>>(Hypostasis.Debug.HypostasisTag);
        if (pluginNames == null) return;

        lock (pluginNames)
        {
            foreach (var name in pluginNames.Where(name => ImGui.Selectable(name, name == selectedPlugin?.Name)))
            {
                if (!plugins.TryGetValue(name, out var ipc))
                    plugins.Add(name, ipc = new(name));

                selectedPlugin = ipc;
                selectedSigInfo = null;
            }
        }
    }

    private static void DrawPluginView()
    {
        if (selectedPlugin == null) return;

        if (selectedSigInfo != null)
        {
            if (!DrawBackButton())
                DrawSelectedSigInfo();
            return;
        }

        /*ImGui.TextUnformatted("Assembly");
        ImGui.BeginChild("AssemblyTypes");
        ReflectionUI.DrawAssemblyDetails(selectedPlugin.Assembly);
        ImGui.EndChild();

        ImGui.TextUnformatted("Plugin");
        ImGui.BeginChild("PluginDetails");
        ReflectionUI.DrawObjectMembersDetails(selectedPlugin.Plugin, selectedPlugin.Plugin.GetType().GetMembers(ReflectionUI.defaultBindingFlags), true);
        ImGui.EndChild();*/

        ImGui.TextUnformatted("Signatures");
        ImGui.BeginChild("SignatureList");
        DrawSignatureList();
        ImGui.EndChild();
    }

    private static void DrawSignatureList()
    {
        if (!ImGui.BeginTable("SignatureInfoTable", 4, ImGuiTableFlags.Borders)) return;

        ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.None, 0.5f);
        ImGui.TableSetupColumn("Signature", ImGuiTableColumnFlags.None, 1);
        ImGui.TableSetupColumn("Address", ImGuiTableColumnFlags.None, 0.3f);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.2f);
        ImGui.TableHeadersRow();

        var sigInfos = selectedPlugin.SigInfos;
        if (sigInfos != null)
        {
            foreach (var sigInfo in sigInfos)
            {
                var offset = sigInfo.Offset != 0 ? $"({sigInfo.Offset})" : string.Empty;

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextUnformatted($"{sigInfo.AssignableInfo?.Name}");
                if (ImGui.IsItemClicked())
                    selectedSigInfo = sigInfo;

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sigInfo.Signature} {offset}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sigInfo.Address:X}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sigInfo.SigType}");
            }
        }
        else
        {
            selectedPlugin = null;
        }

        ImGui.EndTable();
    }

    private static void DrawSelectedSigInfo()
    {
        ImGui.TextUnformatted($"Name: {selectedSigInfo.AssignableInfo?.Name}");
        ImGui.TextUnformatted($"Signature: {selectedSigInfo.Signature}");
        ImGui.TextUnformatted("Address:");
        ImGui.SameLine();
        ImGuiEx.TextCopyable($"{selectedSigInfo.Address:X}");
        ImGui.TextUnformatted($"Type: {selectedSigInfo.SigType}");

        if (selectedSigInfo.AssignableInfo is not { } assignableInfo) return;
        var memberInfo = assignableInfo.MemberInfo;
        var memberDetails = new Debug.MemberDetails(memberInfo, assignableInfo.Object);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("Member Info");
        ImGui.TextUnformatted($"{memberInfo.MemberType}: {memberInfo.DeclaringType}.{assignableInfo.Name}");
        ImGui.TextUnformatted($"{memberDetails.Type}: {memberDetails.ValueString}");
        if (memberDetails.IsPointer)
            ImGui.TextUnformatted($"Can Read Memory: {memberDetails.CanReadMemory}");

        if (selectedSigInfo.SigAttribute != null)
        {
            var attribute = selectedSigInfo.SigAttribute;
            var ex = selectedSigInfo.ExAttribute;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Attribute Info");
            ImGui.TextUnformatted($"Scan Type: {attribute.ScanType}");
            ImGui.TextUnformatted($"Offset: {attribute.Offset}");
            ImGui.TextUnformatted($"Fallibility: {attribute.Fallibility}");

            if (selectedSigInfo.SigType == SigScannerWrapper.SignatureInfo.SignatureType.Hook)
            {
                ImGui.TextUnformatted($"Detour: {attribute.DetourName}");
                ImGui.TextUnformatted($"Enable: {ex.EnableHook}");
                ImGui.TextUnformatted($"Dispose: {ex.DisposeHook}");
            }
        }
        else if (selectedSigInfo.CSAttribute != null)
        {
            var attribute = selectedSigInfo.CSAttribute;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Attribute Info");
            ImGui.TextUnformatted($"{attribute.ClientStructsType}.{attribute.MemberName}");
        }

        if ((!memberDetails.ContainsMembers && memberDetails.Length == 0) || !ImGui.BeginTabBar("DetailsTabBar")) return;

        if (memberDetails.ContainsMembers && ImGui.BeginTabItem("Object Details"))
        {
            ImGui.BeginChild("ObjectDetails", ImGui.GetContentRegionAvail(), true);
            ReflectionUI.DrawObjectDetails(memberDetails);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }

        if (memberDetails.Length > 0 && ImGui.BeginTabItem("Memory Details"))
        {
            ImGui.BeginChild("MemoryDetails", ImGui.GetContentRegionAvail(), true);
            MemoryUI.DrawMemoryDetails(memberDetails.Address, memberDetails.Length);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private static bool DrawBackButton()
    {
        if (!ImGuiEx.FontButton(FontAwesomeIcon.ArrowLeft.ToIconString(), UiBuilder.IconFont)) return false;
        selectedSigInfo = null;
        return true;
    }
}