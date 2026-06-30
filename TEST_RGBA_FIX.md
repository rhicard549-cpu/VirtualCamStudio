# RGBA Color Format Fix - Test Instructions

## What Changed

The pipeline now correctly sends **RGBA** format to UnityCapture:

1. **Studio loads images as BGR** (OpenCV default)
2. **Studio renders to BGR canvas** (3 channels)
3. **UnityCaptureOutput converts BGR→BGRA→RGBA** before sending
4. **UnityCaptureSender receives and forwards RGBA**
5. **UnityCapture expects RGBA** and converts it internally to BGRA for display

### The Problem We Fixed

- **Before**: We sent BGRA `(0,0,255,255)` for red pixels
- **UnityCapture read it as**: RGBA, putting blue (0) in red channel → **purple/magenta**
- **Now**: We send RGBA `(255,0,0,255)` for red pixels → **correct red output**

## Test Steps

### 1. Stop Current Sender
Close the UnityCaptureSender console window if it's running.

### 2. Rebuild Sender (ONLY IF NEEDED)
The sender is already built. If you need to rebuild:

```powershell
cmd /c '"C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat" x64 && cd /d "D:\Projects\VirtualCamStudio\UnityCaptureSender" && cl.exe /nologo /EHsc /std:c++17 /D_UNICODE /DUNICODE UnityCaptureSender.cpp FrameIPC.cpp gdiplus.lib user32.lib /link /OUT:UnityCaptureSender.exe'
```

### 3. Start Fresh Test

1. **Launch sender:**
   ```powershell
   D:\Projects\VirtualCamStudio\UnityCaptureSender\UnityCaptureSender.exe
   ```

2. **Start VirtualCamStudio**

3. **Load your bright red test image**

4. **Click "Start Unity Capture"**

5. **Check CloudPhone** - you should now see **RED**, not purple!

## Expected Diagnostics

When you see frame 30/60/90 diagnostics:

### Studio Output (Debug window):
```
[UnityCaptureOutput] Frame: 1080x1920, Center pixel RGBA: (255,0,0,255)
```

### Sender Receive:
```
[RECV] Pixel [center]: RGBA=(255,0,0,255)
```

### Sender Send:
```
[SEND] Pixel [center]: RGBA=(255,0,0,255)
```

### CloudPhone:
Should show **RED** image (not purple!)

## Success Criteria

✅ **CloudPhone shows the correct red color**  
✅ **Diagnostics show RGBA=(255,0,0,255) for red pixels**  
✅ **No purple/magenta/blue tint**

## If It Still Shows Purple

That would mean UnityCapture has a different internal format expectation than documented. Let me know and we'll investigate the UnityCapture driver configuration.
