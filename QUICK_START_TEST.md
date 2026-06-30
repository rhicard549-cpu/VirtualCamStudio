# Quick Start: Testing Unity Video Capture with CloudPhone

## Current Status ✅
- **OBS Integration:** ❌ Removed (UI clean, build successful)
- **Unity Video Capture:** ✅ Active (IPC pipeline ready)
- **Frame Source:** ✅ Connected to render pipeline
- **CloudPhone:** ✅ Seeing Unity Video Capture camera (currently green screen)

## Goal
Get CloudPhone to display the actual photo/video you upload to VirtualCam Studio instead of the green diagnostic frame.

---

## Step-by-Step Test Procedure

### 1. Start UnityCaptureSender (Native Sender)
```powershell
# Open PowerShell in the project directory
cd D:\Projects\VirtualCamStudio\UnityCaptureSender\x64\Release
.\UnityCaptureSender.exe
```

**What to Look For:**
```
[UnityCapture] Initialized sender (1920x1080, RGBA32)
[FrameIPC] Named pipe server started: \\.\pipe\VirtualCamStudio_Frames
[FrameIPC] Waiting for VirtualCamStudio to connect...
```

**If you see blue frames:**
- ✅ This is normal! The sender generates blue diagnostic frames until Studio connects
- ✅ UnityCapture is working correctly

**Leave this window open and running.**

---

### 2. Launch VirtualCam Studio
- Open Visual Studio
- Press **F5** to run the project in Debug mode
- Or run: `D:\Projects\VirtualCamStudio\VirtualCamStudio\bin\Debug\net8.0-windows\VirtualCamStudio.exe`

**What to Look For:**
- Studio UI should appear with:
  - ✅ Camera Profile selector
  - ✅ "Start Unity Video Capture" button (blue)
  - ✅ "Stop Unity Video Capture" button (grayed out)
  - ✅ Status bar showing "Unity Video Capture: ● Stopped" (gray indicator)

---

### 3. Load Media into Studio

**Option A: Load an Image**
1. Drag and drop a **JPG/PNG** file into the **Media Library** panel (left side)
2. Click the image thumbnail to load it
3. The **Preview Window** (center) should show your image

**Option B: Load a Video**
1. Drag and drop an **MP4/AVI** file into the Media Library
2. Click the video thumbnail to load it
3. Click **Play** button to start playback
4. The Preview Window should show your video playing

**Verify:**
- ✅ Preview window displays your media
- ✅ No errors in Visual Studio Output window

---

### 4. Start Unity Video Capture

1. Click **"Start Unity Video Capture"** button in the toolbar
2. Watch the status bar change to:
   - ✅ "Unity Video Capture: ● Running" (green indicator)

**In Visual Studio Output Window, Look For:**
```
[UnityCaptureOutput] Initializing...
[UnityCaptureOutput] Connecting to UnityCaptureSender...
[UnityCaptureOutput] ✓ Connected to UnityCaptureSender
[Outputs.OutputManager.SendFrameAsync] Broadcasting to 1 output plugin(s)...
[Outputs.OutputManager.SendFrameAsync] → Sending to UnityCaptureOutput
[UnityCaptureOutput] FPS Sent: 30 | Failed: 0
```

**In UnityCaptureSender Console, Look For:**
```
[FrameIPC] ✓ Client connected from VirtualCamStudio
[FrameIPC] Received frame: 1920x1080, 8294400 bytes
Frames sent: 30 | IPC frames: 30 | Fallback frames: 0 | FPS: 30.0
```

**Key Success Indicators:**
- ✅ `IPC frames` count should increase (30, 60, 90...)
- ✅ `Fallback frames` should be 0
- ✅ FPS should be stable around 30

---

### 5. Open CloudPhone and Test

1. Launch CloudPhone
2. Navigate to the camera settings
3. Select **"UnityVideoCapture"** as the camera source
4. **EXPECTED RESULT:**
   - ✅ You should see your uploaded photo or video
   - ❌ **NOT** a green screen
   - ❌ **NOT** a blue diagnostic frame

---

## Troubleshooting

### Issue: CloudPhone Still Shows Green Screen

**Cause:** Studio is running but Unity capture wasn't started, or frames aren't flowing.

**Solutions:**
1. ✅ **Verify Unity capture is running:**
   - Status bar should show green indicator: "● Running"
   - If gray, click "Start Unity Video Capture"

2. ✅ **Check UnityCaptureSender console:**
   - Should show `IPC frames > 0`
   - If `Fallback frames > 0`, frames aren't arriving from Studio

3. ✅ **Check Visual Studio Output:**
   - Look for `[UnityCaptureOutput] FPS Sent: 30`
   - If you see connection errors, restart UnityCaptureSender first, then Studio

4. ✅ **Restart sequence:**
   - Close Studio
   - Restart UnityCaptureSender.exe
   - Launch Studio again
   - Load media → Start Unity capture

---

### Issue: No Connection Between Studio and Sender

**Symptoms:**
- UnityCaptureSender shows: `[FrameIPC] Waiting for VirtualCamStudio to connect...`
- Studio Output shows: `[UnityCaptureOutput] ⚠️ Connection timeout`

**Solution:**
1. Make sure UnityCaptureSender is running **BEFORE** clicking "Start Unity Video Capture"
2. Check that the pipe name matches:
   - Studio uses: `\\.\pipe\VirtualCamStudio_Frames`
   - Sender expects: `\\.\pipe\VirtualCamStudio_Frames`
3. Restart both applications in the correct order:
   - UnityCaptureSender → Studio → Start Unity Capture

---

### Issue: Frames Sending But CloudPhone Shows Old Content

**Symptoms:**
- UnityCaptureSender shows high `IPC frames` count
- Studio shows "● Running"
- CloudPhone still shows green/blue diagnostic frame

**Solution:**
1. **Refresh CloudPhone camera:**
   - Switch to a different camera
   - Switch back to UnityVideoCapture
   - Or restart CloudPhone

2. **Verify frame data:**
   - UnityCaptureSender should show: `Received frame: 1920x1080, 8294400 bytes`
   - If byte count is wrong, check Studio's camera profile resolution

---

### Issue: Performance Problems (Low FPS, Stuttering)

**Check These:**
1. **Studio FPS:**
   - Status bar should show "30 FPS"
   - If lower, media file might be too high resolution

2. **Sender FPS:**
   - Console should show "FPS: 30.0"
   - If lower, CPU might be overloaded

3. **Solutions:**
   - Try a lower resolution image/video
   - Close other CPU-intensive applications
   - Check camera profile is set to 30 FPS (not 60)

---

## Expected Debug Output

### Visual Studio Output (When Working):
```
[RenderPipeline.OnFrameRequested] Sending to new OutputManager (1 outputs)...
[Outputs.OutputManager.SendFrameAsync] Broadcasting to 1 output plugin(s)...
[Outputs.OutputManager.SendFrameAsync] → Sending to UnityCaptureOutput
[UnityCaptureOutput] FPS Sent: 30 | Failed: 0
[Outputs.OutputManager.SendFrameAsync] ✓ UnityCaptureOutput completed
[RenderPipeline.OnFrameRequested] ✓ New OutputManager completed
```

### UnityCaptureSender Console (When Working):
```
[FrameIPC] ✓ Client connected from VirtualCamStudio
[FrameIPC] Received frame: 1920x1080, 8294400 bytes
Frames sent: 300 | IPC frames: 300 | Fallback frames: 0 | FPS: 30.0
```

---

## Success Checklist

Before testing with CloudPhone, verify:
- ✅ UnityCaptureSender.exe is running
- ✅ Console shows "Waiting for VirtualCamStudio to connect..." or "Client connected"
- ✅ Studio is running with media loaded
- ✅ Preview window shows your image/video
- ✅ "Start Unity Video Capture" button was clicked
- ✅ Status bar shows green "● Running"
- ✅ Visual Studio Output shows frames being sent
- ✅ UnityCaptureSender shows `IPC frames > 0` and `Fallback frames: 0`

Then test CloudPhone:
- ✅ Open CloudPhone
- ✅ Select "UnityVideoCapture" camera
- ✅ **Should see your uploaded photo/video (not green screen)**

---

## Architecture Reminder

```
Your Photo/Video (in Studio)
	 ↓
VirtualCam Studio RenderPipeline
	 ↓
Outputs.OutputManager
	 ↓
UnityCaptureOutput.cs (IPC Client)
	 ↓
Named Pipe: \\.\pipe\VirtualCamStudio_Frames
	 ↓
UnityCaptureSender.exe (IPC Server)
	 ↓
SharedImageMemory::Send (Native UnityCapture Protocol)
	 ↓
Unity Video Capture Virtual Camera
	 ↓
CloudPhone (or any app using virtual cameras)
```

The green screen you're seeing means the IPC pipeline exists but **real frames aren't flowing yet**. Following these steps will establish the full frame flow.

---

## Quick Test Commands

```powershell
# Terminal 1: Start the native sender
cd D:\Projects\VirtualCamStudio\UnityCaptureSender\x64\Release
.\UnityCaptureSender.exe

# Terminal 2: Run Studio from VS or directly
cd D:\Projects\VirtualCamStudio\VirtualCamStudio\bin\Debug\net8.0-windows
.\VirtualCamStudio.exe
```

Then: Load media → Start Unity capture → Open CloudPhone → Select UnityVideoCapture → **See your content!**
