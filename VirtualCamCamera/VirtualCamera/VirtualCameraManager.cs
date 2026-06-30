using VirtualCamCamera.Interop;

namespace VirtualCamCamera.VirtualCamera;

/// <summary>
/// Manages virtual camera registration and lifecycle
/// </summary>
public class VirtualCameraManager : IDisposable
{
    private VirtualCameraDevice? _camera;
    private bool _isRegistered;
    private bool _disposed;

    /// <summary>
    /// Register and start a virtual camera with the specified name
    /// </summary>
    public void RegisterCamera(string cameraName)
    {
        if (_isRegistered)
            throw new InvalidOperationException("Camera is already registered");

        try
        {
            Console.WriteLine("Creating Virtual Camera...");

            // Create the camera device
            _camera = new VirtualCameraDevice(cameraName);

            Console.WriteLine("Registering...");

            // Initialize Media Foundation and the camera
            _camera.Initialize();

            // Note: A full implementation would register the camera with Windows
            // using Device Manager APIs and registry entries. This minimal version
            // demonstrates the Media Foundation infrastructure.

            _isRegistered = true;
            Console.WriteLine("Camera Ready.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to register camera: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Unregister the virtual camera
    /// </summary>
    public void UnregisterCamera()
    {
        if (!_isRegistered)
            return;

        try
        {
            _camera?.Shutdown();
            _camera?.Dispose();
            _camera = null;
            _isRegistered = false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error unregistering camera: {ex.Message}");
        }
    }

    /// <summary>
    /// Keep the camera running
    /// </summary>
    public void KeepAlive()
    {
        if (!_isRegistered)
            throw new InvalidOperationException("Camera is not registered");

        // The camera is kept alive by maintaining the Media Foundation session
        // In a full implementation, this would handle frame callbacks and streaming
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        UnregisterCamera();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~VirtualCameraManager()
    {
        Dispose();
    }
}
