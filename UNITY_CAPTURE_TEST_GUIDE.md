# Unity Video Capture Testing Guide

## Overview
VirtualCam Studio now sends frames **only** to Unity Video Capture via the UnityCaptureSender IPC bridge. All OBS integration has been removed.

## Architecture
```
VirtualCamStudio (WPF) 
  → UnityCaptureOutput (IPC Client)
	→ Named Pipe: \\.\pipe\VirtualCamStudio_Frames
	  → UnityCaptureSender.exe (Native C++ IPC Server)
		→ UnityCapture Shared Memory
		  → Unity Video Capture Virtual Camera
			→ CloudPhone / Any Application
```

## Testing Steps

### 1. Start UnityCaptureSender
```powershell
cd UnityCaptureSender\x64\Release
.\UnityCaptureSender.exe
```

**Expected Output:**
```
[UnityCapture] Initialized sender (1920x1080, RGBA32)
[FrameIPC] Named pipe server started: \\.\pipe\VirtualCamStudio_Frames
[FrameIPC] Waiting for VirtualCamStudio to connect...
```

### 2. Launch VirtualCam Studio
- Open `VirtualCamStudio.sln` in Visual Studio
- Run the application (F5)

### 3. Load Media
- Drag and drop an **image** or **video** file into the Media Library panel
- Click the media item to load it into the viewport

### 4. Start Unity Video Capture Output
- Click **"Start Unity Video Capture"** button in the toolbar
- **Status bar** should show:
  - `Unity Video Capture: ● Running` (green indicator)

**Expected UnityCaptureSender Console:**
```
[FrameIPC] ✓ Client connected from VirtualCamStudio
[FrameIPC] Received frame: 1920x1080, 8294400 bytes
Frames sent: 30 | IPC frames: 30 | Fallback frames: 0 | FPS: 30.0
```

### 5. Open CloudPhone or Other Application
- Launch CloudPhone (or any app that can use virtual cameras)
- Select **"UnityVideoCapture"** as the camera source
- **Expected Result:**
  - You should see the **image or video** you loaded in VirtualCam Studio
  - NOT a green screen or blue diagnostic frame

## Troubleshooting

### Issue: Green Screen in CloudPhone
**Cause:** UnityCaptureSender is sending a fallback diagnostic frame because it's not receiving frames from Studio.

**Solutions:**
1. **Check pipe connection:**
   - UnityCaptureSender console should show `"✓ Client connected from VirtualCamStudio"`
   - If not, restart UnityCaptureSender first, then Studio

2. **Verify Unity capture is started:**
   - Studio status bar should show green indicator for Unity Video Capture
   - If gray, click "Start Unity Video Capture"

3. **Check media is loaded:**
   - An image or video must be selected in the Media Library
   - The preview window should show your media content

4. **Verify frame flow:**
   - UnityCaptureSender should show `IPC frames > 0`
   - If `Fallback frames > 0`, frames aren't arriving from Studio

### Issue: Blue Diagnostic Frame
**Cause:** UnityCaptureSender is running but Studio hasn't connected yet, or the pipe connection was lost.

**Solution:**
- Make sure to click "Start Unity Video Capture" in Studio after loading media

### Issue: No Virtual Camera in CloudPhone
**Cause:** Unity Video Capture driver not installed or UnityCaptureSender not running.

**Solution:**
1. Ensure UnityCaptureSender.exe is running
2. Check that `UnityCapture.dll` exists in the UnityCaptureSender directory
3. Restart CloudPhone to refresh the camera list

### Issue: Stuttering or Frame Drops
**Cause:** Network issues or high CPU usage.

**Solution:**
1. Check Studio's FPS in the status bar (should be ~30 FPS)
2. Check UnityCaptureSender console for FPS stats
3. Try a lower-resolution media file
4. Close other CPU-intensive applications

## Frame Flow Verification

### Check VirtualCam Studio Debug Output
In Visual Studio Output window, look for:
```
[UnityCaptureOutput] ✓ Connected to UnityCaptureSender
[UnityCaptureOutput] Frame sent: 1920x1080 RGBA32 (8294400 bytes)
[UnityCaptureOutput] Stats: 300 sent | 0 failed | 30.0 fps
```

### Check UnityCaptureSender Console
```
[FrameIPC] ✓ Client connected from VirtualCamStudio
[FrameIPC] Received frame: 1920x1080, 8294400 bytes
Frames sent: 300 | IPC frames: 300 | Fallback frames: 0 | FPS: 30.0
```

**Key Metrics:**
- `IPC frames` should match `Frames sent`
- `Fallback frames` should be 0 (or only non-zero before Studio connects)
- FPS should be stable around 30

## Success Criteria
✅ **Unity Video Capture is working correctly when:**
1. UnityCaptureSender console shows `IPC frames > 0`
2. CloudPhone displays the **actual image/video** from Studio
3. No green or blue diagnostic frames are visible
4. Frame rate is stable (25-30 FPS)
5. Studio status bar shows green Unity Video Capture indicator

## UI Reference

### Toolbar Controls
- **Camera Profile:** Select resolution/framerate preset (e.g., "1080x1920 @30 FPS")
- **Start Unity Video Capture:** Begin sending frames to UnityCaptureSender
- **Stop Unity Video Capture:** Stop sending frames
- **Video Controls:** Play/Pause/Stop for video files

### Status Bar
- **StatusText:** General status messages
- **ResolutionText:** Current output resolution and framerate
- **Unity Video Capture:** Connection status indicator
  - ● Gray "Stopped" - Output not started
  - ● Green "Running" - Actively sending frames

## Notes
- The **preview window** in Studio always shows your content (this is independent of Unity capture)
- You must load media (image/video) **before** starting Unity capture
- CloudPhone will only see frames **after** you click "Start Unity Video Capture"
- The first few frames might be diagnostic frames until Studio connects
