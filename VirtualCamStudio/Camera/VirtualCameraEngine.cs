using System;

namespace VirtualCamStudio.Camera
{
    /// <summary>
    /// Central engine managing the virtual smartphone camera with realistic physics.
    /// Simulates mass, momentum, friction, and phone-specific behavior from profiles.
    /// The camera moves; the document stays stationary.
    /// </summary>
    public class VirtualCameraEngine
    {
        // Current camera state (actual position)
        private readonly CameraState _current = new();

        // Target camera state (where user wants to move)
        private readonly CameraState _target = new();

        // Phone profile (defines camera behavior)
        private PhoneProfile _profile = PhoneProfileFactory.GetDefault();

        // Handheld motion state
        private double _handheldTime = 0.0;
        private Random _random = new Random();

        // Verification mode
        private bool _verificationMode = true; // Enabled by default for document work

        // Camera Limits (from profile, can be overridden by verification mode)
        public const double MaxPositionX = 500.0;   // Max left/right movement (pixels)
        public const double MaxPositionY = 500.0;   // Max forward/back movement (pixels)

        public CameraState Current => _current;
        public CameraState Target => _target;
        public PhoneProfile Profile => _profile;
        public bool VerificationMode
        {
            get => _verificationMode;
            set => _verificationMode = value;
        }

        public VirtualCameraEngine()
        {
        }

        /// <summary>
        /// Loads a phone profile to change camera behavior.
        /// </summary>
        public void LoadProfile(PhoneProfile profile)
        {
            _profile = profile ?? PhoneProfileFactory.GetDefault();
            _current.Smoothing = _profile.SmoothingFactor;
            _target.Smoothing = _profile.SmoothingFactor;
        }

        /// <summary>
        /// Updates camera state with physics simulation, handheld motion, and roll recovery.
        /// Call this every frame.
        /// </summary>
        public void Update(double deltaTime)
        {
            // Get effective limits and smoothing based on verification mode
            double effectiveMaxPitch = _verificationMode ? _profile.VerificationTiltLimit : _profile.MaxPitch;
            double effectiveMaxYaw = _verificationMode ? _profile.VerificationTiltLimit : _profile.MaxYaw;
            double effectiveSmoothingFactor = _profile.SmoothingFactor * 
                (_verificationMode ? _profile.VerificationSmoothingMultiplier : 1.0);

            // Apply physics-based movement (velocity, momentum, friction)
            ApplyPhysics(deltaTime, effectiveSmoothingFactor);

            // Apply roll recovery when idle
            ApplyRollRecovery(deltaTime);

            // Apply handheld motion to current state if enabled
            if (_current.HandheldMode)
            {
                ApplyHandheldMotion(_current, deltaTime);
            }

            // Apply soft limits with progressive resistance
            ApplySoftLimits(_current, effectiveMaxPitch, effectiveMaxYaw);

            // Hard clamp to absolute limits
            ClampToLimits(_current, effectiveMaxPitch, effectiveMaxYaw);
        }

        /// <summary>
        /// Sets target camera distance.
        /// </summary>
        public void SetDistance(double distance)
        {
            _target.Distance = Math.Clamp(distance, _profile.MinDistance, _profile.MaxDistance);
            _target.TimeSinceLastInput = 0.0;
        }

        /// <summary>
        /// Adjusts target camera distance by delta.
        /// </summary>
        public void AdjustDistance(double delta)
        {
            SetDistance(_target.Distance + delta);
        }

        /// <summary>
        /// Sets target camera position with distance-based sensitivity scaling.
        /// </summary>
        public void SetPosition(double x, double y)
        {
            _target.PositionX = Math.Clamp(x, -MaxPositionX, MaxPositionX);
            _target.PositionY = Math.Clamp(y, -MaxPositionY, MaxPositionY);
            _target.TimeSinceLastInput = 0.0;
        }

        /// <summary>
        /// Adjusts target camera position by delta with automatic sensitivity scaling.
        /// Movement becomes more precise when camera is closer to document.
        /// </summary>
        public void AdjustPosition(double deltaX, double deltaY)
        {
            // Calculate sensitivity based on distance (closer = finer control)
            double sensitivity = CalculateSensitivity(_current.Distance);

            // Apply verification mode speed reduction if active
            if (_verificationMode)
            {
                sensitivity *= _profile.VerificationSpeedMultiplier;
            }

            SetPosition(_target.PositionX + deltaX * sensitivity, 
                       _target.PositionY + deltaY * sensitivity);
        }

        /// <summary>
        /// Sets target camera tilt (pitch and yaw).
        /// </summary>
        public void SetTilt(double pitch, double yaw)
        {
            double maxPitch = _verificationMode ? _profile.VerificationTiltLimit : _profile.MaxPitch;
            double maxYaw = _verificationMode ? _profile.VerificationTiltLimit : _profile.MaxYaw;

            _target.Pitch = Math.Clamp(pitch, -maxPitch, maxPitch);
            _target.Yaw = Math.Clamp(yaw, -maxYaw, maxYaw);
            _target.TimeSinceLastInput = 0.0;
        }

        /// <summary>
        /// Adjusts target camera tilt by delta.
        /// </summary>
        public void AdjustTilt(double deltaPitch, double deltaYaw)
        {
            SetTilt(_target.Pitch + deltaPitch, _target.Yaw + deltaYaw);
        }

        /// <summary>
        /// Sets target camera roll.
        /// </summary>
        public void SetRoll(double roll)
        {
            _target.Roll = Math.Clamp(roll, -_profile.MaxRoll, _profile.MaxRoll);
            _target.TimeSinceLastInput = 0.0;
        }

        /// <summary>
        /// Adjusts target camera roll by delta.
        /// </summary>
        public void AdjustRoll(double delta)
        {
            SetRoll(_target.Roll + delta);
        }

        /// <summary>
        /// Sets smoothing factor (0 = instant, 1 = no movement).
        /// </summary>
        public void SetSmoothing(double smoothing)
        {
            _current.Smoothing = Math.Clamp(smoothing, 0.0, 0.95);
            _target.Smoothing = _current.Smoothing;
        }

        /// <summary>
        /// Enables or disables handheld mode.
        /// </summary>
        public void SetHandheldMode(bool enabled)
        {
            _current.HandheldMode = enabled;
            _target.HandheldMode = enabled;
            if (enabled)
            {
                _handheldTime = 0.0;
            }
        }

        /// <summary>
        /// Smoothly resets camera to default state.
        /// </summary>
        public void ResetCamera()
        {
            _target.Reset();
            // Don't reset velocities - allow momentum to carry camera to center
        }

        /// <summary>
        /// Instantly snaps camera to default state (no interpolation).
        /// </summary>
        public void ResetCameraInstant()
        {
            _current.Reset();
            _target.Reset();
        }

        /// <summary>
        /// Calculates movement sensitivity based on camera distance.
        /// Closer distance = finer control for precise document positioning.
        /// </summary>
        private double CalculateSensitivity(double distance)
        {
            // Guard against division by zero
            double range = _profile.MaxDistance - _profile.MinDistance;
            if (range <= 0.0001)
                return 1.0;

            // Linear interpolation from 1.0 at max distance to ProximitySensitivityScale at min distance
            double t = (distance - _profile.MinDistance) / range;
            return _profile.ProximitySensitivityScale + (1.0 - _profile.ProximitySensitivityScale) * t;
        }

        /// <summary>
        /// Applies physics-based movement with velocity, momentum, and friction.
        /// Creates natural camera weight and smooth acceleration/deceleration.
        /// </summary>
        private void ApplyPhysics(double deltaTime, double smoothingFactor)
        {
            // Guard against invalid deltaTime (first frame or paused)
            if (deltaTime <= 0.0001 || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime))
            {
                deltaTime = 0.016; // Default to 60 FPS frame time
            }

            // Clamp deltaTime to reasonable bounds (prevent huge jumps)
            deltaTime = Math.Clamp(deltaTime, 0.001, 0.1);

            // Calculate desired velocity toward target (with smoothing)
            double targetVelX = (_target.PositionX - _current.PositionX) * smoothingFactor / deltaTime;
            double targetVelY = (_target.PositionY - _current.PositionY) * smoothingFactor / deltaTime;
            double targetVelDist = (_target.Distance - _current.Distance) * smoothingFactor / deltaTime;
            double targetVelPitch = (_target.Pitch - _current.Pitch) * smoothingFactor / deltaTime;
            double targetVelYaw = (_target.Yaw - _current.Yaw) * smoothingFactor / deltaTime;
            double targetVelRoll = (_target.Roll - _current.Roll) * smoothingFactor / deltaTime;

            // Apply velocity smoothing (simulates mass/inertia)
            double velSmooth = _profile.VelocitySmoothing;
            _current.VelocityX = Lerp(_current.VelocityX, targetVelX, velSmooth);
            _current.VelocityY = Lerp(_current.VelocityY, targetVelY, velSmooth);
            _current.VelocityDistance = Lerp(_current.VelocityDistance, targetVelDist, velSmooth);
            _current.VelocityPitch = Lerp(_current.VelocityPitch, targetVelPitch, velSmooth);
            _current.VelocityYaw = Lerp(_current.VelocityYaw, targetVelYaw, velSmooth);
            _current.VelocityRoll = Lerp(_current.VelocityRoll, targetVelRoll, velSmooth);

            // Guard against NaN/Infinity propagation
            _current.VelocityX = SanitizeValue(_current.VelocityX);
            _current.VelocityY = SanitizeValue(_current.VelocityY);
            _current.VelocityDistance = SanitizeValue(_current.VelocityDistance);
            _current.VelocityPitch = SanitizeValue(_current.VelocityPitch);
            _current.VelocityYaw = SanitizeValue(_current.VelocityYaw);
            _current.VelocityRoll = SanitizeValue(_current.VelocityRoll);

            // Apply friction (gradual slowdown)
            double friction = 1.0 - _profile.MovementFriction;
            _current.VelocityX *= friction;
            _current.VelocityY *= friction;
            _current.VelocityDistance *= friction;
            _current.VelocityPitch *= friction;
            _current.VelocityYaw *= friction;
            _current.VelocityRoll *= friction;

            // Clamp velocities to max
            double maxVel = _profile.MaxVelocity;
            _current.VelocityX = Math.Clamp(_current.VelocityX, -maxVel, maxVel);
            _current.VelocityY = Math.Clamp(_current.VelocityY, -maxVel, maxVel);

            // Update position based on velocity
            _current.PositionX += _current.VelocityX * deltaTime;
            _current.PositionY += _current.VelocityY * deltaTime;
            _current.Distance += _current.VelocityDistance * deltaTime;
            _current.Pitch += _current.VelocityPitch * deltaTime;
            _current.Yaw += _current.VelocityYaw * deltaTime;
            _current.Roll += _current.VelocityRoll * deltaTime;

            // Sanitize final position values
            _current.PositionX = SanitizeValue(_current.PositionX);
            _current.PositionY = SanitizeValue(_current.PositionY);
            _current.Distance = SanitizeValue(_current.Distance);
            _current.Pitch = SanitizeValue(_current.Pitch);
            _current.Yaw = SanitizeValue(_current.Yaw);
            _current.Roll = SanitizeValue(_current.Roll);

            // Track idle time
            double movementThreshold = 0.1;
            if (Math.Abs(_current.VelocityX) > movementThreshold || 
                Math.Abs(_current.VelocityY) > movementThreshold ||
                Math.Abs(_current.VelocityPitch) > movementThreshold ||
                Math.Abs(_current.VelocityYaw) > movementThreshold ||
                Math.Abs(_current.VelocityRoll) > movementThreshold)
            {
                _current.TimeSinceLastInput = 0.0;
            }
            else
            {
                _current.TimeSinceLastInput += deltaTime;
            }
        }

        /// <summary>
        /// Applies soft camera limits with progressive resistance.
        /// Camera can exceed normal limits slightly but with increasing resistance.
        /// </summary>
        private void ApplySoftLimits(CameraState state, double maxPitch, double maxYaw)
        {
            // Calculate soft limit thresholds
            double softLimitDistance = _profile.MinDistance + (_profile.MaxDistance - _profile.MinDistance) * _profile.SoftLimitFactor;
            double softLimitPitch = maxPitch * _profile.SoftLimitFactor;
            double softLimitYaw = maxYaw * _profile.SoftLimitFactor;
            double softLimitRoll = _profile.MaxRoll * _profile.SoftLimitFactor;

            // Apply soft resistance to distance
            if (state.Distance < softLimitDistance)
            {
                double excess = softLimitDistance - state.Distance;
                double resistance = excess / (softLimitDistance - _profile.MinDistance);
                state.VelocityDistance *= (1.0 - resistance * 0.8);
            }

            // Apply soft resistance to pitch
            if (Math.Abs(state.Pitch) > softLimitPitch)
            {
                double excess = Math.Abs(state.Pitch) - softLimitPitch;
                double resistance = excess / (maxPitch - softLimitPitch);
                state.VelocityPitch *= (1.0 - resistance * 0.9);
            }

            // Apply soft resistance to yaw
            if (Math.Abs(state.Yaw) > softLimitYaw)
            {
                double excess = Math.Abs(state.Yaw) - softLimitYaw;
                double resistance = excess / (maxYaw - softLimitYaw);
                state.VelocityYaw *= (1.0 - resistance * 0.9);
            }

            // Apply soft resistance to roll
            if (Math.Abs(state.Roll) > softLimitRoll)
            {
                double excess = Math.Abs(state.Roll) - softLimitRoll;
                double resistance = excess / (_profile.MaxRoll - softLimitRoll);
                state.VelocityRoll *= (1.0 - resistance * 0.9);
            }
        }

        /// <summary>
        /// Gradually returns roll toward level when camera is idle.
        /// Simulates natural wrist correction.
        /// </summary>
        private void ApplyRollRecovery(double deltaTime)
        {
            // Only recover if camera has been idle long enough
            if (_current.TimeSinceLastInput < _profile.RollRecoveryDelay)
                return;

            // Only recover if target roll is near zero (user isn't actively rotating)
            if (Math.Abs(_target.Roll) > 2.0)
                return;

            // Gradually reduce current roll toward zero
            double recoveryAmount = _profile.RollRecoverySpeed * deltaTime;
            if (Math.Abs(_current.Roll) < recoveryAmount)
            {
                _current.Roll = 0.0;
                _current.VelocityRoll = 0.0;
            }
            else
            {
                _current.Roll -= Math.Sign(_current.Roll) * recoveryAmount;
            }
        }

        /// <summary>
        /// Applies subtle handheld motion simulating breathing, muscle correction, and wrist drift.
        /// Much more realistic than random jitter.
        /// </summary>
        private void ApplyHandheldMotion(CameraState state, double deltaTime)
        {
            _handheldTime += deltaTime;

            double amplitude = _profile.HandheldAmplitude;

            // Breathing motion (slow, smooth sine wave)
            double breathingX = Math.Sin(_handheldTime * _profile.HandheldBreathingFreq * Math.PI * 2.0) * amplitude * 0.5;
            double breathingY = Math.Cos(_handheldTime * _profile.HandheldBreathingFreq * Math.PI * 1.7) * amplitude * 0.3;

            // Muscle correction (tiny adjustments)
            double muscleX = Math.Sin(_handheldTime * _profile.HandheldMuscleFreq * Math.PI * 2.3) * amplitude * 0.2;
            double muscleY = Math.Cos(_handheldTime * _profile.HandheldMuscleFreq * Math.PI * 1.9) * amplitude * 0.2;

            // Wrist drift (very slow rotation drift)
            double wristRoll = Math.Sin(_handheldTime * 0.3) * amplitude * 0.08;
            double wristPitch = Math.Cos(_handheldTime * 0.35) * amplitude * 0.05;
            double wristYaw = Math.Sin(_handheldTime * 0.4) * amplitude * 0.05;

            // Stabilization correction (profile-based resistance to handheld motion)
            double stabilization = _profile.StabilizationStrength;
            double motionReduction = 1.0 - stabilization;

            // Apply to state (reduced by stabilization)
            state.PositionX += (breathingX + muscleX) * motionReduction;
            state.PositionY += (breathingY + muscleY) * motionReduction;
            state.Roll += wristRoll * motionReduction;
            state.Pitch += wristPitch * motionReduction;
            state.Yaw += wristYaw * motionReduction;
        }

        /// <summary>
        /// Hard clamps camera state to absolute limits.
        /// </summary>
        private void ClampToLimits(CameraState state, double maxPitch, double maxYaw)
        {
            state.Distance = Math.Clamp(state.Distance, _profile.MinDistance, _profile.MaxDistance);
            state.PositionX = Math.Clamp(state.PositionX, -MaxPositionX, MaxPositionX);
            state.PositionY = Math.Clamp(state.PositionY, -MaxPositionY, MaxPositionY);
            state.Pitch = Math.Clamp(state.Pitch, -maxPitch, maxPitch);
            state.Yaw = Math.Clamp(state.Yaw, -maxYaw, maxYaw);
            state.Roll = Math.Clamp(state.Roll, -_profile.MaxRoll, _profile.MaxRoll);
        }

        private static double Lerp(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        /// Sanitizes a value to prevent NaN and Infinity propagation.
        /// Returns 0 if the value is invalid.
        /// </summary>
        private static double SanitizeValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;
            return value;
        }
    }
}
