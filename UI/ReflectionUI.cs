using System.Numerics;
using ImGuiNET;
using System.Reflection;
using System.Runtime.InteropServices;

namespace uDev.UI;

public static class ReflectionUI
{
    public const BindingFlags defaultBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const MemberTypes whitelistedMemberTypes = MemberTypes.Field | MemberTypes.Property;

    public static void DrawStructureDetails(object s, bool isArray = false)
    {
        foreach (var memberInfo in s.GetType().GetMembers(defaultBindingFlags))
        {
            if ((memberInfo.MemberType & whitelistedMemberTypes) == 0) continue;
            var memberDetails = new Debug.MemberDetails(memberInfo, s);

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
}