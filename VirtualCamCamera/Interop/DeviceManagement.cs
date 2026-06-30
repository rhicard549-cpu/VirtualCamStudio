using System.Runtime.InteropServices;

namespace VirtualCamCamera.Interop;

/// <summary>
/// Registry and device management interop
/// </summary>
public static class DeviceManagement
{
    // Registry manipulation for device registration
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int RegCreateKeyEx(
        IntPtr hKey,
        string lpSubKey,
        uint Reserved,
        string? lpClass,
        uint dwOptions,
        uint samDesired,
        IntPtr lpSecurityAttributes,
        out IntPtr phkResult,
        out uint lpdwDisposition);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int RegSetValueEx(
        IntPtr hKey,
        string lpValueName,
        uint Reserved,
        uint dwType,
        byte[] lpData,
        uint cbData);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern int RegCloseKey(IntPtr hKey);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int RegDeleteKey(IntPtr hKey, string lpSubKey);

    // Registry constants
    public static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
    public const uint KEY_WRITE = 0x20006;
    public const uint REG_OPTION_NON_VOLATILE = 0;
    public const uint REG_SZ = 1;
    public const uint REG_DWORD = 4;
    public const int ERROR_SUCCESS = 0;
}
