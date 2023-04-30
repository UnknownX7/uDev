using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using ImGuiNET;
using Hypostasis.Debug;

namespace uDev.UI.Modules;

public class HypostasisPluginsUI : PluginUIModule
{
    public override string MenuLabel => "Browse Hypostasis Plugins";
    public override int MenuPriority => 0;

    private HypostasisMemberDebugInfo selectedDebugInfo = null;
    private Debug.PluginIPC selectedPlugin = null;
    private readonly Dictionary<string, Debug.PluginIPC> plugins = new();

    public override void Draw()
    {
        ImGui.BeginChild("PluginList", new Vector2(150 * ImGuiHelpers.GlobalScale, 0), true);
        DrawPluginList();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("PluginView");
        DrawPluginView();
        ImGui.EndChild();
    }

    private void DrawPluginList()
    {
        if (!DalamudApi.PluginInterface.TryGetData<HashSet<string>>(DebugIPC.HypostasisTag, out var pluginNames)) return;

        lock (pluginNames)
        {
            foreach (var name in pluginNames.Where(name => ImGui.Selectable(name, name == selectedPlugin?.Name)))
            {
                if (!plugins.TryGetValue(name, out var ipc))
                    plugins.Add(name, ipc = new(name));

                selectedPlugin = ipc;
                selectedDebugInfo = null;
            }
        }
    }

    private void DrawPluginView()
    {
        if (selectedPlugin == null) return;

        if (selectedDebugInfo != null)
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

    private void DrawSignatureList()
    {
        if (!ImGui.BeginTable("SignatureInfoTable", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Info", ImGuiTableColumnFlags.None, 0.5f);
        ImGui.TableSetupColumn("Signature", ImGuiTableColumnFlags.None, 1);
        ImGui.TableSetupColumn("Address", ImGuiTableColumnFlags.None, 0.3f);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.None, 0.25f);
        ImGui.TableHeadersRow();

        var debugInfos = selectedPlugin.DebugInfos;
        if (debugInfos != null)
        {
            foreach (var debugInfo in debugInfos)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextUnformatted($"{debugInfo.AssignableInfo?.Name}");
                if (ImGui.IsItemClicked())
                    selectedDebugInfo = debugInfo;

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{debugInfo.Signature}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{debugInfo.Address:X}");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{debugInfo.DebugType}");
            }
        }

        ImGui.EndTable();
    }

    private void DrawSelectedSigInfo()
    {
        ImGui.TextUnformatted($"Name: {selectedDebugInfo.AssignableInfo?.Name}");
        ImGui.TextUnformatted($"Signature: {selectedDebugInfo.Signature}");
        ImGui.TextUnformatted("Address:");
        ImGui.SameLine();
        ImGuiEx.TextCopyable($"{selectedDebugInfo.Address:X}");
        ImGui.TextUnformatted($"Type: {selectedDebugInfo.DebugType}");

        if (selectedDebugInfo.AssignableInfo is not { } assignableInfo) return;
        var memberInfo = assignableInfo.MemberInfo;
        var memberDetails = new Debug.MemberDetails(memberInfo, assignableInfo.Object);

        ImGui.Spacing();
        ImGui.Spacing();
        ImGui.TextUnformatted("Member Info");
        ImGui.TextUnformatted($"{memberInfo.MemberType}: {memberInfo.DeclaringType}.{assignableInfo.Name}");
        ImGui.TextUnformatted($"{memberDetails.Type}: {memberDetails.ValueString}");
        if (memberDetails.IsPointer)
            ImGui.TextUnformatted($"Can Read Memory: {memberDetails.CanReadMemory}");

        if (selectedDebugInfo.SignatureInjectionAttribute != null)
        {
            var attribute = selectedDebugInfo.SignatureInjectionAttribute;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Attribute Info");
            ImGui.TextUnformatted($"Scan Type: {(attribute.Static ? "Static" : "Text")}");
            ImGui.TextUnformatted($"Offset: {attribute.Offset}");
            ImGui.TextUnformatted($"Required: {attribute.Required}");

            if (selectedDebugInfo.DebugType == HypostasisMemberDebugInfo.MemberDebugType.Hook)
            {
                ImGui.TextUnformatted($"Detour: {attribute.DetourName}");
                ImGui.TextUnformatted($"Enable: {attribute.EnableHook}");
                ImGui.TextUnformatted($"Dispose: {attribute.DisposeHook}");
            }
        }
        else if (selectedDebugInfo.CSInjectionAttribute != null)
        {
            var attribute = selectedDebugInfo.CSInjectionAttribute;

            ImGui.Spacing();
            ImGui.Spacing();
            ImGui.TextUnformatted("Attribute Info");
            ImGui.TextUnformatted($"{attribute.ClientStructsType}.{attribute.MemberName}");
        }

        if ((!memberDetails.ContainsMembers && memberDetails.Length == 0) || !ImGui.BeginTabBar("DetailsTabBar")) return;

        if (memberDetails.ContainsMembers && ImGui.BeginTabItem("Object Details"))
        {
            ImGui.BeginChild("ObjectDetails", Vector2.Zero, true);
            ReflectionUI.DrawObjectDetails(memberDetails);
            ImGui.EndChild();
            ImGui.EndTabItem();
        }

        if (memberDetails.Length > 0 && ImGui.BeginTabItem("Memory Details"))
        {
            MemoryUI.DrawMemoryEditorChild(memberDetails.Address, memberDetails.Length);
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }

    private bool DrawBackButton()
    {
        if (!ImGuiEx.FontButton(FontAwesomeIcon.ArrowLeft.ToIconString(), UiBuilder.IconFont)) return false;
        selectedDebugInfo = null;
        return true;
    }
}