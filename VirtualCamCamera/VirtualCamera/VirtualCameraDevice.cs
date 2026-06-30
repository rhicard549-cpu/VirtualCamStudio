using System.Runtime.InteropServices;
using VirtualCamCamera.Interop;

namespace VirtualCamCamera.VirtualCamera;

/// <summary>
/// Represents a virtual camera device with Media Foundation integration
/// </summary>
public class VirtualCameraDevice : IDisposable
{
    private readonly string _friendlyName;
    private IntPtr _mediaSource = IntPtr.Zero;
    private bool _isInitialized;
    private bool _disposed;

    public string FriendlyName => _friendlyName;
    public bool IsInitialized => _isInitialized;

    public VirtualCameraDevice(string friendlyName)
    {
        _friendlyName = friendlyName ?? throw new ArgumentNullException(nameof(friendlyName));
    }

    /// <summary>
    /// Initialize the virtual camera device
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
            return;

        try
        {
            // Initialize Media Foundation
            int hr = MediaFoundation.MFStartup(MediaFoundation.MF_VERSION, 0);
            MediaFoundation.ThrowOnError(hr, "MFStartup");

            // Create attributes for the device source
            hr = MediaFoundation.MFCreateAttributes(out IntPtr pAttributes, 4);
            MediaFoundation.ThrowOnError(hr, "MFCreateAttributes");

            try
            {
                // Set up the attributes for a video capture device
                var attributes = (IMFAttributes)Marshal.GetObjectForIUnknown(pAttributes);

                // Create local copies of GUIDs for ref parameters
                Guid sourceTypeKey = MediaFoundation.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE;
                Guid sourceTypeValue = MediaFoundation.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID;
                Guid friendlyNameKey = MediaFoundation.MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME;

                // Set source type to video capture
                attributes.SetGUID(ref sourceTypeKey, ref sourceTypeValue);

                // Set friendly name
                attributes.SetString(ref friendlyNameKey, _friendlyName);

                // Note: For a real virtual camera, we would need to create and register
                // the device properly through the Windows Device Manager and provide
                // a symbolic link. This minimal implementation focuses on the MF infrastructure.

                _isInitialized = true;
            }
            finally
            {
                if (pAttributes != IntPtr.Zero)
                {
                    Marshal.Release(pAttributes);
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to initialize virtual camera: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Shutdown the virtual camera device
    /// </summary>
    public void Shutdown()
    {
        if (!_isInitialized)
            return;

        try
        {
            if (_mediaSource != IntPtr.Zero)
            {
                Marshal.Release(_mediaSource);
                _mediaSource = IntPtr.Zero;
            }

            // Shutdown Media Foundation
            MediaFoundation.MFShutdown();
            _isInitialized = false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during camera shutdown: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Shutdown();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~VirtualCameraDevice()
    {
        Dispose();
    }
}
