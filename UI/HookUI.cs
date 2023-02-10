using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Dalamud.Game.Text;
using Dalamud.Hooking;
using Dalamud.Interface;
using Dalamud.Logging;
using ImGuiNET;

namespace uDev.UI;

public static class HookUI
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
        ["float"] = typeof(float)
    };

    private static readonly string[] argTypes = typeDictionary.Select(kv => kv.Key).ToArray();
    private static int ret = 1;
    private static readonly int[] args = new int[10];
    private static object hook;
    private static bool logChat = false;
    private static bool startEnabled = true;

    public static IDisposable Hook => hook as IDisposable;

    static HookUI() => args[0] = 8;

    public static void Draw()
    {
        SignatureUI.DrawAddressInput();
        var inputWidth = 75 * ImGuiHelpers.GlobalScale;
        ImGui.SetNextItemWidth(inputWidth);
        ImGui.Combo("##Return", ref ret, argTypes, argTypes.Length);
        ImGui.SameLine();
        ImGui.TextUnformatted("func(");
        ImGui.SameLine();

        for (int i = 0; i < args.Length; i++)
        {
            ImGui.SetNextItemWidth(inputWidth);
            ImGui.Combo($"##Arg{i}", ref args[i], argTypes, argTypes.Length);
            ImGui.SameLine();
        }

        ImGui.TextUnformatted(")");

        var valid = ValidateAddress();

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
        ImGui.Button("Reset");
        if (ImGuiEx.IsItemDoubleClicked())
        {
            ret = 0;
            for (int i = 0; i < args.Length; i++)
                args[i] = 0;
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

    private static unsafe bool ValidateAddress() => SignatureUI.Address >= DalamudApi.SigScanner.BaseTextAddress && SignatureUI.Address < DalamudApi.SigScanner.BaseRDataAddress
        && *(byte*)SignatureUI.Address != 0xCC && *(byte*)(SignatureUI.Address - 1) == 0xCC;

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
        var hookField = Expression.Convert(Expression.Field(null, typeof(HookUI).GetField(nameof(hook), BindingFlags.Static | BindingFlags.NonPublic)!), hookType);
        var getHookOriginal = Expression.Call(hookField, hookType.GetProperty(nameof(Hook<Action>.Original), BindingFlags.Instance | BindingFlags.Public)!.GetMethod!);
        var callHookOriginal = Expression.Invoke(getHookOriginal, paramExpressions);
        var assignRet = hasReturn ? Expression.Assign(retVar, callHookOriginal) : null;

        var objectArray = paramExpressions.Select(p => Expression.Convert(p, typeof(object)));
        if (hasReturn)
            objectArray = objectArray.Append(Expression.Convert(retVar, typeof(object)));
        var concatExpression = Expression.Call(typeof(HookUI).GetMethod(nameof(ConcatParams), BindingFlags.Static | BindingFlags.NonPublic)!, Expression.Constant(hasReturn), Expression.NewArrayInit(typeof(object), objectArray));
        var printExpression = Expression.Call(typeof(HookUI).GetMethod(nameof(Log), BindingFlags.Static | BindingFlags.NonPublic)!, concatExpression);

        var block = hasReturn ? Expression.Block(new[] { retVar }, assignRet, printExpression, retVar) : Expression.Block(printExpression, callHookOriginal);
        return ctor?.Invoke(new object[] { SignatureUI.Address, Expression.Lambda(hookDelegateType, block, paramExpressions).Compile() });
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