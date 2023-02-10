using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

    private static int ret;
    private static readonly int[] args = new int[10];
    private static object hook;

    public static IDisposable Hook => hook as IDisposable;

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

        if (ImGui.Button("Create Delegate"))
        {
            Hook?.Dispose();
            hook = CreateHook();
        }
        ImGui.SameLine();
        ImGui.Button("Reset");
        if (ImGuiEx.IsItemDoubleClicked())
        {
            ret = 0;
            for (int i = 0; i < args.Length; i++)
                args[i] = 0;
        }

        if (hook == null) return;

        if (ImGui.Button("Enable Hook"))
            hook.GetType().GetMethod("Enable")?.Invoke(hook, null);
        ImGui.SameLine();
        if (ImGui.Button("Dispose Hook") && Hook != null)
        {
            Hook.Dispose();
            hook = null;
        }
    }

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

        var hookField = Expression.Convert(Expression.Field(null, typeof(HookUI).GetField("hook", BindingFlags.Static | BindingFlags.NonPublic)!), hookType);
        var getHookOriginal = Expression.Call(hookField, hookType.GetProperty("Original", BindingFlags.Instance | BindingFlags.Public)!.GetMethod!);
        var callHookOriginal = Expression.Invoke(getHookOriginal, paramExpressions);
        var retVar = hasReturn ? Expression.Variable(retType, "ret") : null;
        var assignRet = hasReturn ? Expression.Assign(retVar, callHookOriginal) : null; //Expression.Convert(callHookOriginal, retType)

        var objectArray = paramExpressions.Select(p => Expression.Convert(p, typeof(object)));
        if (hasReturn)
            objectArray = objectArray.Append(Expression.Convert(retVar, typeof(object)));

        var concatExpression = Expression.Call(typeof(HookUI).GetMethod("ConcatParams", BindingFlags.Static | BindingFlags.NonPublic)!, Expression.NewArrayInit(typeof(object), objectArray));
        var printExpression = Expression.Call(typeof(HookUI).GetMethod("LogInfo", BindingFlags.Static | BindingFlags.NonPublic)!, concatExpression);

        var block = hasReturn ? Expression.Block(new[] { retVar }, assignRet, printExpression, retVar) : Expression.Block(printExpression, callHookOriginal);
        return ctor?.Invoke(new object[] { SignatureUI.Address, Expression.Lambda(hookDelegateType, block, paramExpressions).Compile() });
    }

    private static void LogInfo(string message) => PluginLog.Information(message);

    private static string ConcatParams(params object[] objects)
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
            if (i != objects.Length - 1)
                str += $"a{i + 1}: {oStr} | ";
            else
                str += $"ret: {oStr}";
        }

        return str;
    }
}