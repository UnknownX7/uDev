using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace uDev;

public static class DelegateTypeFactory
{
    private static readonly ModuleBuilder module;

    static DelegateTypeFactory()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DelegateTypeFactory"), AssemblyBuilderAccess.RunAndCollect);
        module = assembly.DefineDynamicModule("DelegateTypeFactory");
    }

    public static Type CreateDelegateType(Type[] types)
    {
        var builder = module.DefineType(GetUniqueName(), TypeAttributes.Sealed | TypeAttributes.Public, typeof(MulticastDelegate));

        var ctor = builder.DefineConstructor(MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
            CallingConventions.Standard, new[] { typeof(object), typeof(nint) });
        ctor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        var paramTypes = types.SkipLast(1).ToArray();

        var invokeMethod = builder.DefineMethod("Invoke", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public, types.Last(), paramTypes);
        invokeMethod.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        for (int i = 1; i <= paramTypes.Length; i++)
            invokeMethod.DefineParameter(i, ParameterAttributes.None, $"a{i}");

        return builder.CreateType();
    }

    private static string GetUniqueName()
    {
        var number = 2;
        var name = "CustomDelegate";
        while (module.GetType(name) != null)
            name = $"CustomDelegate{number++}";
        return name;
    }
}