# ROOT CAUSE FOUND: Color Format Mismatch

## The Problem

CloudPhone and webcamtests were showing incorrect colors (purple/wrong colors) because of a **double color swap** bug in the pipeline.

## The Bug

### What Was Happening (WRONG):

1. **VirtualCamStudio** sent frames in **BGRA** format
2. **VirtualCamStudio** marked them as format `0` (which means RGBA)
3. **UnityCaptureSender** received the data and believed it was RGBA
4. **UnityCaptureSender** passed it to UnityCapture driver as-is
5. **UnityCapture driver** converted "RGBA" → BGRA (swapping R↔B channels)
   - See `UnityCaptureFilter.cpp` line 226: `#define RGBATOBGRA(x) ((x&0xFF00FF00)|((x&0x00FF0000)>>16)|((x&0x000000FF)<<16))`
6. **Result**: Red and Blue channels got swapped TWICE, creating wrong colors

### Example of Double-Swap:
- Original pixel: Red (R=255, G=0, B=0, A=255) in BGRA = `(0, 0, 255, 255)`
- Studio sends as: `(0, 0, 255, 255)` marked as "RGBA"
- UnityCapture reads byte 0 (0) as Red, byte 2 (255) as Blue
- UnityCapture swaps to BGRA: Now thinks Red=255, result = Blue pixel!

## The Fix

**Changed `VirtualCamStudio/Outputs/UnityCaptureOutput.cs`** to:
1. Convert frames from BGRA → RGBA **before** sending
2. Updated diagnostic logs to show RGBA values
3. Added proper cleanup of converted frame

### Code Changes:
```csharp
// NEW: Convert BGRA → RGBA before sending
Mat rgbaFrame = new Mat();
Cv2.CvtColor(bgraFrame, rgbaFrame, ColorConversionCodes.BGRA2RGBA);

// Now send RGBA data (matches format 0 expectation)
// ... send rgbaFrame.Data instead of bgraFrame.Data ...

// Clean up
rgbaFrame.Dispose();
```

### What Now Happens (CORRECT):

1. **VirtualCamStudio** converts frames from BGRA → RGBA
2. **VirtualCamStudio** sends them in RGBA format, marked as format `0` (RGBA) ✓
3. **UnityCaptureSender** receives RGBA data (correct expectation) ✓
4. **UnityCaptureSender** passes RGBA to UnityCapture ✓
5. **UnityCapture driver** converts RGBA → BGRA for Windows display ✓
6. **Result**: Colors are correct! Only ONE swap, as intended.

## Testing

Run the diagnostic script to verify:

```powershell
cd D:\Projects\VirtualCamStudio
powershell -ExecutionPolicy Bypass -File .\RUN_FULL_DIAGNOSTIC.ps1
```

### What to Look For:

1. **In Sender Console:**
   - `[FrameIPC] Received frame 30: WxH, first pixel: (R,G,B,A)`
   - Values should match the image colors (e.g., red pixel = `(255,0,0,255)`)

2. **In Visual Studio Debug Output:**
   - `[UnityCaptureOutput] Frame: WxH, First pixel RGBA: (R,G,B,A)`
   - Values should match what you expect from the loaded media

3. **In CloudPhone/Webcamtests:**
   - Should now show your uploaded image/video with CORRECT colors!
   - No more purple or green screens
   - Colors should match the Studio preview

## Why This Happened

The confusion arose from:
1. Windows typically uses BGRA format internally
2. UnityCapture expects RGBA input and does its own conversion to BGRA
3. The IPC contract header comment was misleading (said BGRA but meant RGBA)
4. Previous code sent BGRA directly without conversion

## Files Modified

1. **VirtualCamStudio/Outputs/UnityCaptureOutput.cs**
   - Added BGRA→RGBA conversion before sending
   - Updated diagnostic logging
   - Added proper resource cleanup

2. **UnityCaptureSender/FrameIPC.cpp** (diagnostic logging only)
   - Added frame receive logging every 30 frames

3. **UnityCaptureSender/UnityCaptureSender.cpp** (diagnostic logging only)
   - Added frame send logging every 30 frames

## Next Steps

1. Run the diagnostic test
2. Verify CloudPhone/webcamtests shows correct colors
3. If successful, remove or reduce diagnostic logging frequency for production use
4. Celebrate! 🎉

---

## Technical Reference

### UnityCapture Format Expectation:
From `Research/UnityCapture/Source/UnityCaptureFilter.cpp`:
- Line 876: Uses `MEDIASUBTYPE_ARGB32` for 32-bit mode
- Line 226: `RGBATOBGRA` macro swaps R↔B channels
- **Conclusion**: UnityCapture expects RGBA input, outputs BGRA

### Named Pipe Contract:
From `UnityCaptureSender/FrameIPC.h`:
- Line 20: `pixelFormat; // 0 = RGBA32`
- **Conclusion**: Format 0 means RGBA, not BGRA

### Studio Frame Format:
From `VirtualCamStudio/Core/Frame.cs`:
- Frames are internally stored as BGRA (OpenCV default)
- **Conclusion**: Must convert before sending to UnityCapture pipeline
