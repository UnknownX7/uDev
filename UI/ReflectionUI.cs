using System.Numerics;
using ImGuiNET;
using System.Reflection;
using System.Runtime.InteropServices;

namespace uDev.UI;

public static class ReflectionUI
{
    public const BindingFlags defaultBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const MemberTypes whitelistedMemberTypes = MemberTypes.Field | MemberTypes.Property;

    public static void DrawObjectDetails(Debug.MemberDetails objectDetails)
    {
        var type = objectDetails.Value.GetType();
        var isClass = type.IsClass;

        foreach (var memberInfo in type.GetMembers(defaultBindingFlags))
        {
            if ((memberInfo.MemberType & whitelistedMemberTypes) == 0) continue;
            var memberDetails = new Debug.MemberDetails(memberInfo, objectDetails.Value);

            var open = false;
            var indent = 0;
            if (memberDetails.ContainsMembers)
            {
                open = ImGui.TreeNodeEx($"##{memberInfo.Name}", ImGuiTreeNodeFlags.AllowItemOverlap | ImGuiTreeNodeFlags.SpanAvailWidth);
                ImGui.SameLine();
            }
            else
            {
                indent = (int)(ImGui.GetFontSize() + ImGui.GetStyle().ItemSpacing.X);
                ImGui.Indent(indent);
            }

            if (isClass)
                DrawMemberDetails(memberDetails);
            else
                DrawStructureMemberDetails(memberDetails);

            if (indent > 0)
                ImGui.Unindent(indent);

            if (!open) continue;
            DrawObjectDetails(memberDetails);
            ImGui.TreePop();
        }
    }

    private static void DrawMemberDetails(Debug.MemberDetails memberDetails)
    {
        ImGui.TextColored(new Vector4(0.25f, 1, 0.5f, 1), memberDetails.MemberInfo.MemberType.ToString());
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.25f, 0.5f, 1, 1), memberDetails.Type?.Name ?? string.Empty);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(1, 1, 0.5f, 1), !memberDetails.IsArray ? $"{memberDetails.MemberInfo.Name}:" : $"{memberDetails.MemberInfo.Name}[{memberDetails.ArrayLength}]");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(1, 1, 1, 1), memberDetails.ValueString);
        if (memberDetails.Type == typeof(long))
            ImGuiEx.SetItemTooltip($"{memberDetails.Value:X}");
    }

    private static void DrawStructureMemberDetails(Debug.MemberDetails memberDetails)
    {
        var offsetAttribute = memberDetails.MemberInfo.GetCustomAttribute<FieldOffsetAttribute>();
        if (offsetAttribute != null)
        {
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), $"[0x{offsetAttribute.Value:X}]");
            ImGui.SameLine();
        }
        DrawMemberDetails(memberDetails);
    }
}