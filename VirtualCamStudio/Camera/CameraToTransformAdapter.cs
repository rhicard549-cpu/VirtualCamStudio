using System;
using VirtualCamStudio.Models;

namespace VirtualCamStudio.Camera
{
    /// <summary>
    /// Converts VirtualCamera state into FramingSettings for ViewportEngine.
    /// This adapter allows the camera-based model to work with the existing rendering pipeline.
    /// </summary>
    public static class CameraToTransformAdapter
    {
        /// <summary>
        /// Converts camera state to framing settings.
        /// Camera Distance → Zoom (inverse relationship: closer = larger)
        /// Camera Position → Offset X/Y
        /// Camera Roll → Rotation
        /// Camera Pitch/Yaw → (Future: perspective transform, currently approximated with offset)
        /// </summary>
        public static FramingSettings ToFramingSettings(CameraState cameraState)
        {
            if (cameraState == null)
                throw new ArgumentNullException(nameof(cameraState));

            // Convert camera distance to zoom
            // Distance 1.0 = Zoom 1.0 (standard)
            // Distance 0.5 = Zoom 2.0 (closer, larger view)
            // Distance 2.0 = Zoom 0.5 (farther, smaller view)
            double zoom = 1.0 / cameraState.Distance;

            // Camera position maps directly to offset
            // Positive X = camera moves right = document appears to move left
            double offsetX = -cameraState.PositionX;
            double offsetY = -cameraState.PositionY;

            // Camera roll maps directly to rotation
            double rotation = cameraState.Roll;

            // Pitch/Yaw: For now, approximate with additional offset
            // Future: implement true perspective transform in ViewportEngine
            // Small pitch/yaw creates subtle positional shift
            double pitchOffset = cameraState.Pitch * 5.0;  // Approximate vertical shift
            double yawOffset = cameraState.Yaw * 5.0;      // Approximate horizontal shift

            offsetX += yawOffset;
            offsetY += pitchOffset;

            return new FramingSettings
            {
                Zoom = zoom,
                OffsetX = offsetX,
                OffsetY = offsetY,
                Rotation = rotation,
                MirrorHorizontal = false,
                MirrorVertical = false
            };
        }

        /// <summary>
        /// Converts framing settings back to camera state (for backward compatibility).
        /// </summary>
        public static CameraState FromFramingSettings(FramingSettings framing)
        {
            if (framing == null)
                throw new ArgumentNullException(nameof(framing));

            return new CameraState
            {
                Distance = 1.0 / Math.Max(framing.Zoom, 0.01),
                PositionX = -framing.OffsetX,
                PositionY = -framing.OffsetY,
                Roll = framing.Rotation,
                Pitch = 0.0,
                Yaw = 0.0,
                Smoothing = 0.15,
                HandheldMode = false
            };
        }
    }
}
