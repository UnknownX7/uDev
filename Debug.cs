using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Hypostasis.Debug;
using static Hypostasis.Util;

namespace uDev;

public static partial class Debug
{
    public class MemberDetails
    {
        public MemberInfo MemberInfo { get; }
        public object Object { get; }
        public object Value { get; }
        public Type Type { get; }
        public bool IsPointer { get; }
        public Type BoxedType { get; }
        public nint Address { get; }
        public long Length { get; }
        public bool CanReadMemory { get; }
        public bool ContainsMembers { get; }
        public bool IsArray { get; }
        public IEnumerable Enumerable { get; }
        public int ArrayLength { get; }
        public string ValueString => Value switch
        {
            nint p => p.ToString("X"),
            _ when !IsPointer => Value?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        public MemberDetails(MemberInfo memberInfo, object o)
        {
            MemberInfo = memberInfo;
            Object = o;

            switch (memberInfo)
            {
                case FieldInfo f:
                    if (!f.IsStatic && o == null) return;

                    Value = f.GetValue(o);
                    Type = f.FieldType;
                    break;
                case PropertyInfo p:
                    if (p.GetMethod is not { IsStatic: true } && o == null) return;

                    IsArray = p.GetIndexParameters().Length > 0;
                    if (IsArray)
                    {
                        var array = new List<object>();
                        try
                        {
                            var oType = o.GetType();
                            var countMember = oType.GetMember("Count", AllMembersBindingFlags | BindingFlags.IgnoreCase)
                                .Concat(oType.GetMember("Length", AllMembersBindingFlags | BindingFlags.IgnoreCase))
                                .First();
                            var countInfo = new AssignableInfo(o, countMember);
                            var count = (int)countInfo.GetValue();
                            ArrayLength = count;
                            for (int i = 0; i < count; i++)
                                array.Add(p.GetValue(o, new object[] { i }));
                        }
                        catch { }

                        Value = array;
                    }
                    else
                    {
                        Value = p.GetValue(o);
                    }
                    Type = p.PropertyType;
                    break;
                case MethodInfo m:
                    if (!m.IsStatic && o == null) return;

                    Value = m;
                    Type = m.ReturnType;
                    return;
            }

            IsPointer = Type?.IsPointer ?? false;
            BoxedType = Type?.GetElementType();

            if (IsPointer && BoxedType != null)
            {
                Address = ConvertObjectToIntPtr(Value);
                Length = Marshal.SizeOf(BoxedType);
                CanReadMemory = Debug.CanReadMemory(Address, Length);
                Value = null;

                // Thanks void and void* and void** and so on...
                try
                {
                    Value = Marshal.PtrToStructure(Address, BoxedType);
                }
                catch { }
            }
            else if (Value?.GetType().Name == nameof(AsmPatch))
            {
                try
                {
                    Address = (nint)Value.GetType().GetProperty("Address")!.GetValue(Value)!;
                    Length = ((byte[])Value.GetType().GetProperty("OldBytes")!.GetValue(Value)!).Length;
                    CanReadMemory = Debug.CanReadMemory(Address, Length);
                }
                catch { }
            }

            Enumerable = Value as IEnumerable;
            ContainsMembers = Value?.GetType() is { IsPrimitive: false } && Value?.GetType() != typeof(string);
        }
    }

    public class PluginIPC //: IDisposable
    {
        public string Name { get; }
        private ICallGateSubscriber<IDalamudPlugin> GetPluginSubscriber { get; }
        private ICallGateSubscriber<Hypostasis.Hypostasis.PluginState> GetPluginStateSubscriber { get; }
        private ICallGateSubscriber<List<HypostasisMemberDebugInfo>> GetDebugInfosSubscriber { get; }
        private ICallGateSubscriber<Dictionary<int, (object, MemberInfo)>> GetMemberInfosSubscriber { get; }

        public Assembly Assembly => Plugin is { } p ? Assembly.GetAssembly(p.GetType()) : null;

        public IDalamudPlugin Plugin
        {
            get
            {
                try
                {
                    return GetPluginSubscriber.InvokeFunc();
                }
                catch
                {
                    return null;
                }
            }
        }

        public List<HypostasisMemberDebugInfo> DebugInfos
        {
            get
            {
                try
                {
                    var sigInfos = GetDebugInfosSubscriber.InvokeFunc();
                    var memberInfos = GetMemberInfosSubscriber.InvokeFunc();
                    for (int i = 0; i < sigInfos.Count; i++)
                    {
                        if (!memberInfos.TryGetValue(i, out var memberInfo)) continue;
                        sigInfos[i].AssignableInfo = new(memberInfo.Item1, memberInfo.Item2);
                    }
                    return sigInfos;
                }
                catch
                {
                    return null;
                }
            }
        }

        public PluginIPC(string name)
        {
            Name = name;
            GetPluginSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<IDalamudPlugin>($"{name}.{nameof(Hypostasis.Hypostasis)}.GetPlugin");
            GetPluginStateSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<Hypostasis.Hypostasis.PluginState>($"{name}.{nameof(Hypostasis.Hypostasis)}.GetPluginState");
            GetDebugInfosSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<List<HypostasisMemberDebugInfo>>($"{name}.{nameof(Hypostasis.Hypostasis)}.GetDebugInfos");
            GetMemberInfosSubscriber = DalamudApi.PluginInterface.GetIpcSubscriber<Dictionary<int, (object, MemberInfo)>>($"{name}.{nameof(Hypostasis.Hypostasis)}.GetMemberInfos");
        }
        //public void Dispose() { }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public nint BaseAddress;
        public nint AllocationBase;
        public uint AllocationProtect;
        public nint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [LibraryImport("kernel32.dll")]
    private static partial int VirtualQuery(nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

    public static unsafe bool CanReadMemory(nint address, long size = 1)
    {
        if (address == 0 || size <= 0)
            return false;

        var endAddress = address + size;
        do
        {
            if (VirtualQuery(address, out var mbi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
                return false;

            if ((mbi.State & 0x1000) == 0 || (mbi.Protect & 0x101) != 0 || (mbi.Protect & 0xEE) == 0)
                return false;

            address = mbi.BaseAddress + mbi.RegionSize;
        } while (address < endAddress);

        return true;
    }

    public static unsafe bool CanReadMemory(void* ptr, long size = 1) => CanReadMemory((nint)ptr, size);

    public static unsafe nint GetMaxReadableMemory(nint address, long size)
    {
        var max = nint.Zero;
        if (address == 0 || size <= 0)
            return max;

        var endAddress = address + size;
        do
        {
            if (VirtualQuery(address, out var mbi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
                break;

            if ((mbi.State & 0x1000) == 0 || (mbi.Protect & 0x101) != 0 || (mbi.Protect & 0xEE) == 0)
                break;

            address = mbi.BaseAddress + mbi.RegionSize;
            max = address - 1;
        } while (address < endAddress);

        return max;
    }

    public static unsafe nint GetMaxReadableMemory(void* ptr, long size) => GetMaxReadableMemory((nint)ptr, size);

    public static unsafe HashSet<nint> GetReadableMemory(nint address, long size)
    {
        var set = new HashSet<nint>();
        if (address == 0 || size <= 0)
            return set;

        var endAddress = address + size;
        do
        {
            if (VirtualQuery(address, out var mbi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
                break;

            if ((mbi.State & 0x1000) == 0 || (mbi.Protect & 0x101) != 0 || (mbi.Protect & 0xEE) == 0)
            {
                address = mbi.BaseAddress + mbi.RegionSize;
            }
            else
            {
                var regionMax = (nint)Math.Min(mbi.BaseAddress + mbi.RegionSize, endAddress);
                for (nint i = address; i < regionMax; i++)
                    set.Add(i);
                address = regionMax;
            }
        } while (address < endAddress);

        return set;
    }

    public static unsafe HashSet<nint> GetReadableMemory(void* ptr, long size) => GetReadableMemory((nint)ptr, size);
}