using System.Runtime.InteropServices;

namespace VirtualCamCamera.Interop;

/// <summary>
/// Media Foundation COM interface definitions and helpers
/// </summary>
public static class MediaFoundation
{
    // Media Foundation startup/shutdown
    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFStartup(uint Version, uint dwFlags = 0);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFShutdown();

    [DllImport("mf.dll", ExactSpelling = true)]
    public static extern int MFCreateDeviceSource(
        IntPtr pAttributes,
        out IntPtr ppSource);

    [DllImport("mfplat.dll", ExactSpelling = true)]
    public static extern int MFCreateAttributes(
        out IntPtr ppMFAttributes,
        uint cInitialSize);

    // Constants
    public const uint MF_SDK_VERSION = 0x0002;
    public const uint MF_API_VERSION = 0x0070;
    public const uint MF_VERSION = (MF_SDK_VERSION << 16) | MF_API_VERSION;

    public const int S_OK = 0;
    public const int E_FAIL = unchecked((int)0x80004005);

    // Media Foundation attribute GUIDs for virtual camera
    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE = 
        new("c60ac5fe-252a-478f-a0ef-bc8fa5f7cad3");

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID = 
        new("8ac3587a-4ae7-42d8-99e0-0a6013eef90f");

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME = 
        new("60d0e559-52f8-4fa2-bbce-acdb34a8ec01");

    public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK = 
        new("58f0aad8-22bf-4f8a-bb3d-d2c4978c6e2f");

    // Helper method to check HRESULT
    public static void ThrowOnError(int hr, string operation)
    {
        if (hr != S_OK)
        {
            throw new COMException($"{operation} failed with HRESULT: 0x{hr:X8}", hr);
        }
    }
}

/// <summary>
/// IMFAttributes interface for setting Media Foundation attributes
/// </summary>
[ComImport]
[Guid("2cd2d921-c447-44a7-a13c-4adabfc247e3")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFAttributes
{
    void GetItem([In] ref Guid guidKey, [In, Out] IntPtr pValue);
    void GetItemType([In] ref Guid guidKey, out uint pType);
    void CompareItem([In] ref Guid guidKey, IntPtr Value, out bool pbResult);
    void Compare(IMFAttributes pTheirs, int MatchType, out bool pbResult);
    void GetUINT32([In] ref Guid guidKey, out uint punValue);
    void GetUINT64([In] ref Guid guidKey, out ulong punValue);
    void GetDouble([In] ref Guid guidKey, out double pfValue);
    void GetGUID([In] ref Guid guidKey, out Guid pguidValue);
    void GetStringLength([In] ref Guid guidKey, out uint pcchLength);
    void GetString([In] ref Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pwszValue, uint cchBufSize, out uint pcchLength);
    void GetAllocatedString([In] ref Guid guidKey, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppwszValue, out uint pcchLength);
    void GetBlobSize([In] ref Guid guidKey, out uint pcbBlobSize);
    void GetBlob([In] ref Guid guidKey, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] pBuf, int cbBufSize, out uint pcbBlobSize);
    void GetAllocatedBlob([In] ref Guid guidKey, out IntPtr ppBuf, out uint pcbSize);
    void GetUnknown([In] ref Guid guidKey, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppv);
    void SetItem([In] ref Guid guidKey, IntPtr Value);
    void DeleteItem([In] ref Guid guidKey);
    void DeleteAllItems();
    void SetUINT32([In] ref Guid guidKey, uint unValue);
    void SetUINT64([In] ref Guid guidKey, ulong unValue);
    void SetDouble([In] ref Guid guidKey, double fValue);
    void SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
    void SetString([In] ref Guid guidKey, [In, MarshalAs(UnmanagedType.LPWStr)] string wszValue);
    void SetBlob([In] ref Guid guidKey, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pBuf, int cbBufSize);
    void SetUnknown([In] ref Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
    void LockStore();
    void UnlockStore();
    void GetCount(out uint pcItems);
    void GetItemByIndex(uint unIndex, out Guid pguidKey, IntPtr pValue);
    void CopyAllItems(IMFAttributes pDest);
}

/// <summary>
/// IMFMediaSource interface
/// </summary>
[ComImport]
[Guid("279a808d-aec7-40c8-9c6b-a6b492c78a66")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMFMediaSource
{
    // Simplified interface - we don't need all methods for minimal implementation
    void GetCharacteristics(out uint pdwCharacteristics);
    void CreatePresentationDescriptor(out IntPtr ppPresentationDescriptor);
    void Start(IntPtr pPresentationDescriptor, IntPtr pguidTimeFormat, IntPtr pvarStartPosition);
    void Stop();
    void Pause();
    void Shutdown();
}
