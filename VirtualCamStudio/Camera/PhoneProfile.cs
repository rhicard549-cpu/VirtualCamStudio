namespace VirtualCamStudio.Camera;

/// <summary>
/// Defines camera behavior characteristics for different smartphone models.
/// Encapsulates physics, limits, and motion parameters to simulate real device cameras.
/// </summary>
public class PhoneProfile
{
    public string Name { get; set; } = "Generic";
    public string Brand { get; set; } = "Generic";
    public string Description { get; set; } = "";

    // Camera Physics
    public double CameraMass { get; set; } = 1.0;                  // Affects momentum and inertia
    public double MovementFriction { get; set; } = 0.15;            // Resistance to movement (0-1)
    public double VelocitySmoothing { get; set; } = 0.2;            // How quickly velocity changes
    public double MaxVelocity { get; set; } = 500.0;                // Maximum movement speed

    // Stabilization
    public double StabilizationStrength { get; set; } = 0.7;        // Image stabilization (0-1)
    public double SmoothingFactor { get; set; } = 0.15;             // General smoothing for movements

    // Camera Limits
    public double MaxPitch { get; set; } = 12.0;                    // Maximum pitch angle (degrees)
    public double MaxYaw { get; set; } = 12.0;                      // Maximum yaw angle (degrees)
    public double MaxRoll { get; set; } = 15.0;                     // Maximum roll angle (degrees)
    public double SoftLimitFactor { get; set; } = 0.8;              // When to start soft resistance (0-1)

    // Distance & Sensitivity
    public double MinDistance { get; set; } = 0.2;                  // Closest zoom (inverse units)
    public double MaxDistance { get; set; } = 5.0;                  // Farthest zoom (inverse units)
    public double ProximitySensitivityScale { get; set; } = 0.3;    // Sensitivity reduction when close

    // Autofocus & Exposure (placeholders for future)
    public double AutofocusSpeed { get; set; } = 1.0;
    public double ExposureAdaptation { get; set; } = 1.0;

    // Field of View (placeholder)
    public double FieldOfView { get; set; } = 75.0;                 // Degrees

    // Roll Recovery
    public double RollRecoverySpeed { get; set; } = 0.5;            // How fast roll auto-levels
    public double RollRecoveryDelay { get; set; } = 1.0;            // Seconds before recovery starts

    // Handheld Simulation
    public double HandheldAmplitude { get; set; } = 0.3;            // Micro-motion amplitude
    public double HandheldBreathingFreq { get; set; } = 0.2;        // Breathing motion frequency (Hz)
    public double HandheldMuscleFreq { get; set; } = 1.5;           // Muscle correction frequency (Hz)

    // Verification Mode Modifiers (applied when verification mode is active)
    public double VerificationSpeedMultiplier { get; set; } = 0.5;  // Slower movement
    public double VerificationTiltLimit { get; set; } = 8.0;        // Reduced tilt limit
    public double VerificationSmoothingMultiplier { get; set; } = 1.5; // More smoothing
}

/// <summary>
/// Factory for creating predefined phone camera profiles.
/// </summary>
public static class PhoneProfileFactory
{
    /// <summary>
    /// Creates the Redmi Flagship profile (Xiaomi premium camera behavior).
    /// Default profile optimized for document verification with strong stabilization.
    /// </summary>
    public static PhoneProfile CreateRedmiFlagship()
    {
        return new PhoneProfile
        {
            Name = "Redmi Flagship",
            Brand = "Xiaomi",
            Description = "Premium Redmi camera with strong stabilization and smooth movement",

            // Camera Physics - Smooth with noticeable weight
            CameraMass = 1.2,
            MovementFriction = 0.18,
            VelocitySmoothing = 0.22,
            MaxVelocity = 450.0,

            // Strong Stabilization (Xiaomi is known for this)
            StabilizationStrength = 0.85,
            SmoothingFactor = 0.18,

            // Limits - Slightly conservative for stability
            MaxPitch = 12.0,
            MaxYaw = 12.0,
            MaxRoll = 15.0,
            SoftLimitFactor = 0.75,

            // Distance & Sensitivity
            MinDistance = 0.2,
            MaxDistance = 5.0,
            ProximitySensitivityScale = 0.25,

            // Autofocus - Fast (Redmi flagship strength)
            AutofocusSpeed = 1.3,
            ExposureAdaptation = 1.1,

            // Field of View - Wide (typical flagship)
            FieldOfView = 78.0,

            // Roll Recovery - Gentle auto-leveling
            RollRecoverySpeed = 0.6,
            RollRecoveryDelay = 1.2,

            // Handheld - Very subtle (strong stabilization)
            HandheldAmplitude = 0.2,
            HandheldBreathingFreq = 0.18,
            HandheldMuscleFreq = 1.3,

            // Verification Mode - Optimized for documents
            VerificationSpeedMultiplier = 0.45,
            VerificationTiltLimit = 8.0,
            VerificationSmoothingMultiplier = 1.6
        };
    }

    /// <summary>
    /// Creates a generic Android profile (placeholder for future implementation).
    /// </summary>
    public static PhoneProfile CreateGenericAndroid()
    {
        return new PhoneProfile
        {
            Name = "Generic Android",
            Brand = "Generic",
            Description = "Standard Android camera behavior"
        };
    }

    /// <summary>
    /// Creates a Samsung Galaxy Flagship profile (placeholder for future implementation).
    /// </summary>
    public static PhoneProfile CreateSamsungGalaxy()
    {
        return new PhoneProfile
        {
            Name = "Samsung Galaxy Flagship",
            Brand = "Samsung",
            Description = "Premium Samsung camera (not yet implemented)"
        };
    }

    /// <summary>
    /// Creates a Google Pixel profile (placeholder for future implementation).
    /// </summary>
    public static PhoneProfile CreateGooglePixel()
    {
        return new PhoneProfile
        {
            Name = "Google Pixel",
            Brand = "Google",
            Description = "Google Pixel computational photography (not yet implemented)"
        };
    }

    /// <summary>
    /// Creates an iPhone profile (placeholder for future implementation).
    /// </summary>
    public static PhoneProfile CreateIPhone()
    {
        return new PhoneProfile
        {
            Name = "iPhone",
            Brand = "Apple",
            Description = "Apple iPhone camera (not yet implemented)"
        };
    }

    /// <summary>
    /// Gets all available profiles (implemented and placeholders).
    /// </summary>
    public static List<PhoneProfile> GetAllProfiles()
    {
        return new List<PhoneProfile>
        {
            CreateRedmiFlagship(),
            CreateGenericAndroid(),
            CreateSamsungGalaxy(),
            CreateGooglePixel(),
            CreateIPhone()
        };
    }

    /// <summary>
    /// Gets the default profile (Redmi Flagship).
    /// </summary>
    public static PhoneProfile GetDefault()
    {
        return CreateRedmiFlagship();
    }
}
