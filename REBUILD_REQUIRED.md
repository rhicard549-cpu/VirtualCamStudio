# CRITICAL: UnityCaptureSender Needs Rebuild

## Problem Identified ✅

The **UnityCaptureSender.exe** that's currently compiled is an **old version without IPC support**. That's why:
- CloudPhone shows green screen (fallback diagnostic frame)
- The console shows "FPS Attempted/Sent" but **NO IPC messages**
- VirtualCam Studio can't connect to send frames

The source code **has** the IPC integration, but the compiled executable is outdated.

---

## Solution: Rebuild UnityCaptureSender in Visual Studio

### Step 1: Open the C++ Project
1. Open **Visual Studio 2026**
2. File → Open → Project/Solution
3. Navigate to: `D:\Projects\VirtualCamStudio\UnityCaptureSender`
4. Open **UnityCaptureSender.vcxproj**

### Step 2: Retarget the Project
1. **Right-click** the project in Solution Explorer
2. Select **"Retarget Projects..."**
3. In the dialog:
   - Windows SDK Version: Select latest (10.0.xxxxx)
   - Platform Toolset: Select latest available
4. Click **OK**

### Step 3: Clean and Rebuild
1. Build → Clean Solution
2. Select **Debug** configuration and **x64** platform
3. Build → Rebuild Solution (Ctrl+Shift+B)
4. Wait for build to complete

### Step 4: Verify Build Output
Check the Output window for:
```
Build succeeded
1>UnityCaptureSender.vcxproj -> D:\Projects\VirtualCamStudio\...\UnityCaptureSender.exe
```

---

## After Rebuilding: Test Again

### Step 1: Start the NEW UnityCaptureSender
```powershell
cd "D:\Projects\VirtualCamStudio\UnityCaptureSender\bin\Debug\x64\UnityCaptureSender"
.\UnityCaptureSender.exe
```

**Expected NEW output:**
```
=============================================================
  UnityCaptureSender - IPC Frame Receiver
=============================================================
[UnityCapture] Initialized sender (1920x1080, RGBA32)
[FrameIPC] Named pipe server started: \\.\pipe\VirtualCamStudio_Frames
[FrameIPC] Waiting for VirtualCamStudio to connect...
```

### Step 2: Connect from Studio
With UnityCaptureSender running and waiting:
1. Open VirtualCam Studio (if not already open)
2. Load your photo (4.png)
3. Click **"Start Unity Video Capture"**

**UnityCaptureSender should now show:**
```
[FrameIPC] ✓ Client connected from VirtualCamStudio
[FrameIPC] Received frame: 1920x1080, 8294400 bytes
Frames sent: 30 | IPC frames: 30 | Fallback frames: 0 | FPS: 30.0
```

### Step 3: Check CloudPhone
- CloudPhone should now show **your photo (the child with toys)**
- NOT green screen!

---

## Why This Happened

The UnityCaptureSender executable was compiled **before** the IPC integration was added to the source code. The source files `UnityCaptureSender.cpp`, `FrameIPC.cpp`, and `FrameIPC.h` all have the IPC code, but they were never recompiled into a new executable.

---

## Alternative: Use Pre-built Binary (If Available)

If you have access to a pre-built UnityCaptureSender.exe with IPC support from another location, you can:
1. Copy it to: `D:\Projects\VirtualCamStudio\UnityCaptureSender\bin\Debug\x64\UnityCaptureSender\`
2. Overwrite the old one
3. Run it

---

## Verification Checklist

After rebuilding, the **new** executable should:
- [ ] Print "FrameIPC" initialization messages
- [ ] Show "Waiting for VirtualCamStudio to connect"
- [ ] Connect when Studio clicks "Start Unity Video Capture"
- [ ] Show "IPC frames" counter (not just "FPS Sent")
- [ ] Send frames from Studio (not blue fallback)

**The old executable only shows:**
```
FPS Attempted: 22 | FPS Sent: 22 | FPS Failed: 0
```

**The new executable shows:**
```
Frames sent: 30 | IPC frames: 30 | Fallback frames: 0 | FPS: 30.0
```

---

## Quick Test After Rebuild

```powershell
# Terminal 1: Start UnityCaptureSender
cd "D:\Projects\VirtualCamStudio\UnityCaptureSender\bin\Debug\x64\UnityCaptureSender"
.\UnityCaptureSender.exe
# Should say "Waiting for VirtualCamStudio to connect..."

# In VirtualCam Studio:
# 1. Load 4.png
# 2. Click "Start Unity Video Capture"
# 3. Check CloudPhone → Should see your photo!
```

---

## If Build Still Fails

If Visual Studio can't build due to missing C++ tools:
1. Open **Visual Studio Installer**
2. Click **Modify** on Visual Studio 2026
3. Check **"Desktop development with C++"** workload
4. Click **Modify** to install
5. After installation, retry the build

---

**The key issue: The compiled executable is old. Rebuild it with the new IPC source code and your photo will appear in CloudPhone!** 🚀
