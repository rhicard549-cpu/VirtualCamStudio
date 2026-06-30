using VirtualCamCamera.VirtualCamera;

namespace VirtualCamCamera;

class Program
{
    static void Main(string[] args)
    {
        const string CameraName = "VirtualCam Studio Camera";

        using var cameraManager = new VirtualCameraManager();

        try
        {
            // Register and start the virtual camera
            cameraManager.RegisterCamera(CameraName);

            // Keep the camera alive
            cameraManager.KeepAlive();

            // Wait for user input
            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
        finally
        {
            // Cleanup is handled by the using statement
            cameraManager.UnregisterCamera();
        }
    }
}
