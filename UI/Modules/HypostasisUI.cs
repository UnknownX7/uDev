using System.Linq;
using ImGuiNET;

namespace uDev.UI.Modules;

public class HypostasisUI : PluginUIModule
{
    public override string MenuLabel => "Hypostasis Debug";
    public override int MenuPriority => 0;

    public override void Draw()
    {
        if (!ImGui.BeginTable("HypostasisStructureStatus", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Structure");
        ImGui.TableSetupColumn("Address");
        ImGui.TableSetupColumn("Valid");
        ImGui.TableHeadersRow();

        foreach (var propertyInfo in typeof(Common).GetAllProperties().Where(propertyInfo => propertyInfo.PropertyType.IsPointer && propertyInfo.PropertyType.GetElementType()!.IsAssignableTo(typeof(IHypostasisStructure))))
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var value = propertyInfo.GetValue(null);
            ImGui.TextUnformatted($"{propertyInfo.Name}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{Util.ConvertObjectToIntPtr(value):X}");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{typeof(Common).GetMethod(nameof(Common.IsValid))!.MakeGenericMethod(propertyInfo.PropertyType.GetElementType()!).Invoke(null, new[] { value })}");
        }

        ImGui.EndTable();
    }
}