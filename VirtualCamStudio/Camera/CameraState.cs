using System;

namespace VirtualCamStudio.Camera
{
    /// <summary>
    /// Represents the state of the virtual smartphone camera.
    /// All parameters describe the camera's position and orientation relative to a stationary document.
    /// </summary>
    public class CameraState
    {
        // Camera Position (in document coordinate space)
        public double PositionX { get; set; } = 0.0;  // Left/Right movement over document
        public double PositionY { get; set; } = 0.0;  // Forward/Back movement over document

        // Camera Distance (height above document)
        public double Distance { get; set; } = 1.0;   // 1.0 = standard viewing distance
                                                       // < 1.0 = closer (larger view)
                                                       // > 1.0 = farther (smaller view)

        // Camera Tilt (rotation around camera axes)
        public double Pitch { get; set; } = 0.0;      // Up/Down rotation (degrees, ±12°)
        public double Yaw { get; set; } = 0.0;        // Left/Right rotation (degrees, ±12°)
        public double Roll { get; set; } = 0.0;       // Wrist rotation (degrees, ±15°)

        // Camera Motion Settings
        public double Smoothing { get; set; } = 0.15; // Interpolation factor (0 = instant, 1 = no movement)
        public bool HandheldMode { get; set; } = false; // Enable subtle motion simulation

        // Camera Physics (for momentum and natural movement)
        public double VelocityX { get; set; } = 0.0;  // Current velocity in X direction
        public double VelocityY { get; set; } = 0.0;  // Current velocity in Y direction
        public double VelocityDistance { get; set; } = 0.0; // Current velocity for distance changes
        public double VelocityPitch { get; set; } = 0.0;    // Current angular velocity for pitch
        public double VelocityYaw { get; set; } = 0.0;      // Current angular velocity for yaw
        public double VelocityRoll { get; set; } = 0.0;     // Current angular velocity for roll

        // Idle tracking (for roll recovery)
        public double TimeSinceLastInput { get; set; } = 0.0; // Seconds since last user input

        /// <summary>
        /// Creates a default camera state (centered, standard distance, no rotation).
        /// </summary>
        public CameraState()
        {
        }

        /// <summary>
        /// Creates a copy of the current camera state.
        /// </summary>
        public CameraState Clone()
        {
            return new CameraState
            {
                PositionX = this.PositionX,
                PositionY = this.PositionY,
                Distance = this.Distance,
                Pitch = this.Pitch,
                Yaw = this.Yaw,
                Roll = this.Roll,
                Smoothing = this.Smoothing,
                HandheldMode = this.HandheldMode,
                VelocityX = this.VelocityX,
                VelocityY = this.VelocityY,
                VelocityDistance = this.VelocityDistance,
                VelocityPitch = this.VelocityPitch,
                VelocityYaw = this.VelocityYaw,
                VelocityRoll = this.VelocityRoll,
                TimeSinceLastInput = this.TimeSinceLastInput
            };
        }

        /// <summary>
        /// Resets camera to default state.
        /// </summary>
        public void Reset()
        {
            PositionX = 0.0;
            PositionY = 0.0;
            Distance = 1.0;
            Pitch = 0.0;
            Yaw = 0.0;
            Roll = 0.0;
            VelocityX = 0.0;
            VelocityY = 0.0;
            VelocityDistance = 0.0;
            VelocityPitch = 0.0;
            VelocityYaw = 0.0;
            VelocityRoll = 0.0;
            TimeSinceLastInput = 0.0;
        }

        /// <summary>
        /// Linearly interpolates toward a target state.
        /// </summary>
        public void LerpToward(CameraState target, double factor)
        {
            PositionX = Lerp(PositionX, target.PositionX, factor);
            PositionY = Lerp(PositionY, target.PositionY, factor);
            Distance = Lerp(Distance, target.Distance, factor);
            Pitch = Lerp(Pitch, target.Pitch, factor);
            Yaw = Lerp(Yaw, target.Yaw, factor);
            Roll = Lerp(Roll, target.Roll, factor);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }
    }
}
