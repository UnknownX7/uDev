using Dalamud.Game.Text;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace uDev.UI;

public static class AddressUI
{
    private static readonly Dictionary<string, Type> typeDictionary = new()
    {
        ["void"] = typeof(void),
        ["byte"] = typeof(byte),
        ["short"] = typeof(short),
        ["ushort"] = typeof(ushort),
        ["int"] = typeof(int),
        ["uint"] = typeof(uint),
        ["long"] = typeof(long),
        ["ulong"] = typeof(ulong),
        ["nint"] = typeof(nint),
        ["nuint"] = typeof(nuint),
        ["float"] = typeof(float),
        ["string"] = typeof(string)
    };

    private static nint address = DalamudApi.SigScanner.BaseTextAddress;
    public static string HexAddress
    {
        get => address.ToString("X");
        set => address = nint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var p) ? (p > uint.MaxValue ? p : p + DalamudApi.SigScanner.BaseAddress) : nint.Zero;
    }

    private static string signature = string.Empty;
    private static readonly string[] argTypes = typeDictionary.Select(kv => kv.Key).ToArray();
    private static int ret = 6;
    private static readonly int[] args = new int[20];
    private static object hook;
    private static bool logChat = false;
    private static bool startEnabled = true;

    public static IDisposable Hook => hook as IDisposable;

    static AddressUI() => args[0] = 8;

    public static void Draw()
    {
        DrawSignatureTest();
        ImGui.Spacing();
        ImGui.Spacing();
        DrawHookTest();
        if (!Debug.CanReadMemory(address, 1)) return;
        MemoryUI.DrawMemoryDetailsChild("MemoryDetails", address, 0x2000);
    }

    private static void DrawSignatureTest()
    {
        var _ = HexAddress;
        if (ImGui.InputText("Address", ref _, 16, ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase | ImGuiInputTextFlags.AutoSelectAll))
            HexAddress = _;
        ImGui.InputText("Signature", ref signature, 512, ImGuiInputTextFlags.AutoSelectAll);

        if (ImGui.Button("Module"))
        {
            try
            {
                address = DalamudApi.SigScanner.DalamudSigScanner.ScanModule(signature);
            }
            catch
            {
                address = nint.Zero;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Text"))
        {
            try
            {
                address = DalamudApi.SigScanner.DalamudSigScanner.ScanText(signature);
            }
            catch
            {
                address = nint.Zero;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Data"))
        {
            try
            {
                address = DalamudApi.SigScanner.DalamudSigScanner.ScanData(signature);
            }
            catch
            {
                address = nint.Zero;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Static"))
        {
            try
            {
                address = DalamudApi.SigScanner.DalamudSigScanner.GetStaticAddressFromSig(signature);
            }
            catch
            {
                address = nint.Zero;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Pop Out Memory View") && address != nint.Zero)
            MemoryUI.AddMemoryView(address);
    }

    private static void DrawHookTest()
    {
        TypeCombo("##Return", ref ret);
        ImGui.SameLine();
        ImGui.TextUnformatted("func(");
        ImGui.SameLine();

        var maxArg = 0;
        for (int i = 0; i < args.Length; i++)
        {
            TypeCombo(args[i] != 0 ? $"a{i + 1}" : $"##a{i + 1}", ref args[i]);

            if (i == args.Length - 1 || args[i] == 0)
            {
                maxArg = i;
                break;
            }

            using (ImGuiEx.StyleVarBlock.Begin(ImGuiStyleVar.ItemSpacing, Vector2.Zero))
                ImGui.SameLine();
            ImGui.TextUnformatted(",");
            ImGui.SameLine();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(")    ");

        if (ImGuiEx.IsItemDraggedDelta("ArgLength", ImGuiMouseButton.Left, 62 * ImGuiHelpers.GlobalScale, false, out var dt))
        {
            var amount = (int)dt.X;
            if (amount > 0)
            {
                for (int i = 0; i < amount; i++)
                {
                    if (i + maxArg >= args.Length) break;
                    args[i + maxArg] = 6;
                }
            }
            else
            {
                if (args[^1] != 0)
                    maxArg++;

                for (int i = 0; i < -amount; i++)
                {
                    if (maxArg - i <= 0) break;
                    args[maxArg - i - 1] = 0;
                }
            }
        }

        var valid = address.IsValidHookAddress();
        if (!valid)
            ImGui.BeginDisabled();

        if (ImGui.Button("Create Hook"))
        {
            Hook?.Dispose();
            hook = CreateHook();
            if (startEnabled)
                EnableHook();
        }

        if (!valid)
            ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.Button("Reset Delegate\uE051\uE051");
        if (ImGuiEx.IsItemDoubleClicked())
        {
            ret = 6;
            for (int i = 1; i < args.Length; i++)
                args[i] = 0;
            args[0] = 8;
        }
        ImGui.SameLine();
        ImGui.Checkbox("Log to Chat", ref logChat);
        ImGui.SameLine();
        ImGui.Checkbox("Start Enabled", ref startEnabled);

        if (hook == null) return;

        if (ImGui.Button("Enable Hook"))
            EnableHook();
        ImGui.SameLine();
        if (ImGui.Button("Disable Hook"))
            DisableHook();
        ImGui.SameLine();
        if (ImGui.Button("Dispose Hook") && Hook != null)
        {
            Hook.Dispose();
            hook = null;
        }
    }

    private static void TypeCombo(string label, ref int current)
    {
        using var _ = ImGuiEx.StyleVarBlock.Begin(ImGuiStyleVar.PopupBorderSize, 1);
        var preview = argTypes[current];
        var inputWidth = ImGui.CalcTextSize(preview).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SetNextItemWidth(inputWidth);
        if (!ImGui.BeginCombo(label, preview, ImGuiComboFlags.HeightLargest | ImGuiComboFlags.NoArrowButton)) return;
        for (int i = 0; i < argTypes.Length; i++)
        {
            var typeName = argTypes[i];
            if (!ImGui.Selectable(typeName, i == current)) continue;
            current = i;
            break;
        }
        ImGui.EndCombo();
    }

    // Delegate func(...)
    // {
    //      var ret = hook.Original(...);
    //      Log(ConcatParams(hasReturn, ...));
    //      return ret;
    // }
    private static object CreateHook()
    {
        var types = args.TakeWhile(id => id != 0).Select(id => typeDictionary[argTypes[id]]).ToList();

        var i = 0;
        var paramExpressions = types.Select(t => Expression.Parameter(t, $"a{++i}")).ToList();

        var retType = typeDictionary[argTypes[ret]];
        var hasReturn = retType != typeof(void);
        if (!hasReturn && types.Count == 0) return null;

        types.Add(retType);
        var hookDelegateType = DelegateTypeFactory.CreateDelegateType(types.ToArray());

        var hookType = typeof(Hook<>).MakeGenericType(hookDelegateType);
        var ctor = hookType.GetConstructor(new[] { typeof(nint), hookDelegateType });

        var retVar = hasReturn ? Expression.Variable(retType, "ret") : null;
        var hookField = Expression.Convert(Expression.Field(null, typeof(AddressUI).GetField(nameof(hook), BindingFlags.Static | BindingFlags.NonPublic)!), hookType);
        var getHookOriginal = Expression.Call(hookField, hookType.GetProperty(nameof(Hook<Action>.Original), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!);
        var callHookOriginal = Expression.Invoke(getHookOriginal, paramExpressions);
        var assignRet = hasReturn ? Expression.Assign(retVar, callHookOriginal) : null;

        var objectArray = paramExpressions.Select(p => Expression.Convert(p, typeof(object)));
        if (hasReturn)
            objectArray = objectArray.Append(Expression.Convert(retVar, typeof(object)));
        var concatExpression = Expression.Call(typeof(AddressUI).GetMethod(nameof(ConcatParams), BindingFlags.Static | BindingFlags.NonPublic)!, Expression.Constant(hasReturn), Expression.NewArrayInit(typeof(object), objectArray));
        var printExpression = Expression.Call(typeof(AddressUI).GetMethod(nameof(Log), BindingFlags.Static | BindingFlags.NonPublic)!, concatExpression);

        var block = hasReturn ? Expression.Block(new[] { retVar }, assignRet, printExpression, retVar) : Expression.Block(printExpression, callHookOriginal);
        return ctor?.Invoke(new object[] { address, Expression.Lambda(hookDelegateType, block, paramExpressions).Compile() });
    }
    private static void EnableHook() => hook.GetType().GetMethod(nameof(Hook<Action>.Enable))?.Invoke(hook, null);

    private static void DisableHook() => hook.GetType().GetMethod(nameof(Hook<Action>.Disable))?.Invoke(hook, null);

    private static void Log(string message)
    {
        message = $"[HookTest] {message}";
        if (logChat)
            DalamudApi.ChatGui.PrintChat(new() { Message = message, Type = XivChatType.Notice });
        else
            PluginLog.Warning(message);
    }

    private static string ConcatParams(bool hasReturn, params object[] objects)
    {
        var str = string.Empty;

        for (int i = 0; i < objects.Length; i++)
        {
            var o = objects[i];
            var oStr = o switch
            {
                nint p => p.ToString("X"),
                nuint up => up.ToString("X"),
                _ => o.ToString()
            };
            str += (i != objects.Length - 1) ? $"a{i + 1}: {oStr} | " : (hasReturn ? $"ret: {oStr}" : $"a{i + 1}: {oStr}");
        }

        return str;
    }
}