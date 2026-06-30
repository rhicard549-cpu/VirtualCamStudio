# SIMPLE FIX: Build UnityCaptureSender in Visual Studio

## The Problem
CloudPhone shows green screen because the old UnityCaptureSender.exe doesn't have the IPC code to receive frames from VirtualCam Studio.

## The Solution (5 Minutes)
Build the updated UnityCaptureSender in Visual Studio. It's much easier than command line!

---

## Step 1: Open Visual Studio

You already have Visual Studio 2026 open with VirtualCamStudio, right?

---

## Step 2: Add C++ Project to Solution

1. In **Solution Explorer** (right side), right-click on the **solution** (the very top item)
2. Choose **Add** → **Existing Project...**
3. Browse to: `D:\Projects\VirtualCamStudio\UnityCaptureSender`
4. Select: `UnityCaptureSender.vcxproj`
5. Click **Open**

**You should now see "UnityCaptureSender" in the solution!**

---

## Step 3: Install C++ Build Tools (If Needed)

If Visual Studio shows an error about missing C++ tools:

1. Close Visual Studio
2. Open **Visual Studio Installer** (search in Windows Start menu)
3. Click **Modify** next to Visual Studio 2026
4. Check the box: **"Desktop development with C++"**
5. Click **Modify** to install (takes 5-10 minutes)
6. Reopen Visual Studio and the solution

---

## Step 4: Retarget the Project

1. In Solution Explorer, **right-click** on **UnityCaptureSender** project
2. Choose **"Retarget Projects"** or **"Retarget Solution"**
3. In the dialog:
   - Windows SDK Version: choose **"10.0 (latest installed version)"**
   - Platform Toolset: choose **the latest one available**
4. Click **OK**

---

## Step 5: Build It!

1. In Solution Explorer, **right-click** on **UnityCaptureSender** project
2. Choose **"Build"**

Watch the Output window at the bottom. You should see:
```
Build succeeded
1>UnityCaptureSender.vcxproj -> D:\Projects\VirtualCamStudio\UnityCaptureSender\bin\Debug\x64\UnityCaptureSender\UnityCaptureSender.exe
```

**BUILD COMPLETE!** ✅

---

## Step 6: Run the New UnityCaptureSender

Open a new PowerShell window and run:

```powershell
cd "D:\Projects\VirtualCamStudio\UnityCaptureSender\bin\Debug\x64\UnityCaptureSender"
.\UnityCaptureSender.exe
```

**What you should see:**
```
=============================================================
  UnityCaptureSender - IPC Frame Receiver
=============================================================
[UnityCapture] Initialized sender (1920x1080, RGBA32)
[FrameIPC] Named pipe server started: \\.\pipe\VirtualCamStudio_Frames
[FrameIPC] Waiting for VirtualCamStudio to connect...
```

**KEY DIFFERENCE:** The NEW version says **"FrameIPC"** and **"Waiting for VirtualCamStudio to connect"**

The old version only showed "FPS Attempted/Sent" with no IPC messages.

---

## Step 7: Test with CloudPhone

With UnityCaptureSender running and waiting:

1. **In VirtualCam Studio:**
   - Make sure your photo is loaded (4.png)
   - Click **"Start Unity Video Capture"** button

2. **UnityCaptureSender console should show:**
   ```
   [FrameIPC] ✓ Client connected from VirtualCamStudio
   [FrameIPC] Received frame: 1920x1080, 8294400 bytes
   Frames sent: 30 | IPC frames: 30 | Fallback frames: 0 | FPS: 30.0
   ```

3. **In CloudPhone:**
   - Switch to a different camera, then back to "Unity Video Capture"
   - **YOUR PHOTO SHOULD NOW APPEAR!** 🎉

---

## Why This Works

**Before:** Old exe sent blue/green diagnostic frames  
**After:** New exe receives real frames from Studio via IPC  

The source code already has the IPC integration, it just needed to be compiled into a new executable.

---

## If Build Fails

### Error: "Cannot find Platform Toolset v143 or v180"
**Solution:** Install C++ build tools (see Step 3 above)

### Error: "shared.inl not found"
**Solution:** The file should exist. If not, run this in PowerShell:
```powershell
Copy-Item "D:\Projects\VirtualCamStudio\Research\UnityCapture\Source\shared.inl" -Destination "D:\Projects\VirtualCamStudio\UnityCaptureSender\" -Force
```

### Error: "FrameIPC.h not found"
**Solution:** The file should exist at `UnityCaptureSender\FrameIPC.h`. Check Solution Explorer.

---

## Alternative: Use Visual Studio Developer PowerShell

If you prefer command line after installing C++ tools:

1. Open **Developer PowerShell for VS 2026** (search in Start menu)
2. Run:
```powershell
cd "D:\Projects\VirtualCamStudio"
msbuild UnityCaptureSender\UnityCaptureSender.vcxproj /p:Configuration=Debug /p:Platform=x64
```

---

## Success Checklist

- [ ] C++ build tools installed in Visual Studio
- [ ] UnityCaptureSender project added to solution
- [ ] Project retargeted to latest SDK/toolset
- [ ] Build succeeded
- [ ] New exe runs and shows "FrameIPC" messages
- [ ] VirtualCam Studio connects when "Start Unity Video Capture" is clicked
- [ ] UnityCaptureSender shows "IPC frames: 30+"
- [ ] CloudPhone displays your photo (not green screen)

---

**Once you rebuild, the green screen will be replaced with your actual photo!** 🚀

This is the last missing piece. The Studio side is ready, the IPC code exists, it just needs to be compiled.
