using ImGuiNET;

namespace uDev.UI;

public static class SignatureUI
{
    public static nint Address { get; set; } = DalamudApi.SigScanner.BaseTextAddress;
    public static string HexAddress
    {
        get => Address.ToString("X");
        set => Address = nint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var tmp) ? (tmp > uint.MaxValue ? tmp : tmp + DalamudApi.SigScanner.BaseAddress) : nint.Zero;
    }

    private static string signature = string.Empty;

    public static void Draw()
    {
        DrawAddressInput();
        ImGui.InputText("Signature", ref signature, 512, ImGuiInputTextFlags.AutoSelectAll);

        if (ImGui.Button("Module"))
        {
            try
            {
                Address = DalamudApi.SigScanner.DalamudSigScanner.ScanModule(signature);
            }
            catch
            {
                Address = nint.Zero;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Text"))
        {
            try
            {
                Address = DalamudApi.SigScanner.DalamudSigScanner.ScanText(signature);
            }
            catch
            {
                Address = nint.Zero;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Data"))
        {
            try
            {
                Address = DalamudApi.SigScanner.DalamudSigScanner.ScanData(signature);
            }
            catch
            {
                Address = nint.Zero;
            }
        }

        ImGui.SameLine();

        if (ImGui.Button("Static"))
        {
            try
            {
                Address = DalamudApi.SigScanner.DalamudSigScanner.GetStaticAddressFromSig(signature);
            }
            catch
            {
                Address = nint.Zero;
            }
        }

        if (!Debug.CanReadMemory(Address, 1)) return;
        ImGui.BeginChild("MemoryDetails", ImGui.GetContentRegionAvail(), true);
        MemoryUI.DrawMemoryDetails(Address, 0x2000);
        ImGui.EndChild();
    }

    public static void DrawAddressInput()
    {
        var _ = HexAddress;
        if (ImGui.InputText("Address", ref _, 16, ImGuiInputTextFlags.CharsHexadecimal | ImGuiInputTextFlags.CharsUppercase | ImGuiInputTextFlags.AutoSelectAll))
            HexAddress = _;
    }
}