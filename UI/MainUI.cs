using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Dalamud.Interface;
using ImGuiNET;
using Lumina.Excel;

namespace uDev.UI;

public static class MainUI
{
    private static bool isVisible = true;
    private static HypostasisMemberDebugInfo selectedDebugInfo = null;
    private static Debug.PluginIPC selectedPlugin = null;
    private static readonly Dictionary<string, Debug.PluginIPC> plugins = new();
    private static readonly Type[] luminaTypes = Assembly.Load("Lumina.Excel").GetTypes<ExcelRow>().ToArray();
    private static Type selectedLuminaType;

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
        ImGuiEx.AddDonationHeader();

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

            if (ImGui.BeginTabItem("Sig / Hook Test"))
            {
                AddressUI.Draw();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("ImGui Window Memory"))
            {
                unsafe
                {
                    var ptr = ImGuiEx.GetCurrentWindow();
                    ImGui.BeginChild("ImGuiMemoryView");
                    MemoryUI.DrawMemoryDetails((nint)ptr, 0x5000);
                    ImGui.EndChild();
                    ImGui.EndTabItem();
                }
            }

            if (ImGui.BeginTabItem("Excel Sheets"))
            {
                ImGui.BeginChild("SheetList", new Vector2(200 * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y), true);
                foreach (var t in luminaTypes.Where(t => ImGui.Selectable(t.Name, t.Name == selectedPlugin?.Name)))
                    selectedLuminaType = t;
                ImGui.EndChild();

                if (selectedLuminaType != null)
                {
                    ImGui.SameLine();
                    var methodInfo = typeof(ImGuiEx).GetMethod(nameof(ImGuiEx.ExcelSheetTable))?.MakeGenericMethod(selectedLuminaType);
                    methodInfo?.Invoke(null, new object[] { "ExcelSheetBrowser" });
                }

                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private static void DrawPluginList()
    {
        if (!DalamudApi.PluginInterface.TryGetData<HashSet<string>>(Hypostasis.Debug.HypostasisTag, out var pluginNames)) return;

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

    private static void DrawPluginView()
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

    private static void DrawSignatureList()
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

    private static void DrawSelectedSigInfo()
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
        selectedDebugInfo = null;
        return true;
    }
}