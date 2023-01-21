using System.Runtime.InteropServices;

namespace uDev;

public static class Debug
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

    [DllImport("kernel32.dll")]
    private static extern int VirtualQuery(nint lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, int dwLength);

    public static unsafe bool CanReadMemory(nint address, long size)
    {
        if (address == 0 || size == 0)
            return false;

        var endAddress = address + size - 1;
        do
        {
            if (VirtualQuery(address, out var mbi, sizeof(MEMORY_BASIC_INFORMATION)) == 0)
                break;

            if ((mbi.State & 0x1000) == 0 || (mbi.Protect & 0x101) != 0 || (mbi.Protect & 0xEE) == 0)
                return false;

            address = mbi.BaseAddress + mbi.RegionSize;
        } while (address <= endAddress);

        return true;
    }
}