using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace uDev;

public static partial class Debug
{
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

    public static unsafe bool CanReadMemory(nint address, long size)
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
}