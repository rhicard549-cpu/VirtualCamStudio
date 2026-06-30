# Deep Format Diagnosis

## Current Expected Flow

1. **Studio loads red image**: BGR `(0, 0, 255)` - 3 channels
2. **Studio converts to BGRA**: `(0, 0, 255, 255)` - 4 channels
3. **Studio converts BGRA→RGBA**: `(255, 0, 0, 255)` - 4 channels ✓
4. **Sender receives**: RGBA `(255, 0, 0, 255)` ✓
5. **Sender sends to UnityCapture**: RGBA `(255, 0, 0, 255)` ✓
6. **UnityCapture converts RGBA→BGRA**: `(255, 0, 0, 255)` → DirectShow BGRA
7. **CloudPhone/WebcamTest displays**: Should show RED

## Current Problem

CloudPhone shows **PURPLE/MAGENTA** instead of red.

## Diagnosis Options

### Option A: UnityCapture expects BGRA (not RGBA)
If the filter documentation is wrong and it actually expects BGRA input:
- We send RGBA `(255,0,0,255)`
- It reads as BGRA → R and B swapped → shows as BLUE
- But we're seeing purple (red+blue), not pure blue

### Option B: Stride/alignment issue
The IPC header uses `stride = width` (pixels), but UnityCapture might expect bytes.
- Current: `stride = 1080` (pixels)
- Should be: `stride = 1080 * 4` (bytes)?

### Option C: Row order (top-down vs bottom-up)
DIB bitmaps can be top-down (negative height) or bottom-up (positive height).
- We send height as positive
- UnityCapture might expect negative for top-down

## Test: Check what .NET is ACTUALLY sending

Add this diagnostic BEFORE the BGRA→RGBA conversion to see raw values:

```csharp
// In UnityCaptureOutput.cs, after line 137 (after BGR2BGRA):
unsafe
{
	byte* ptr = (byte*)bgraFrame.DataPointer;
	int centerIdx = (bgraFrame.Height / 2) * bgraFrame.Width + (bgraFrame.Width / 2);
	Debug.WriteLine($"[Before RGBA convert] Center BGRA: ({ptr[centerIdx*4]}, {ptr[centerIdx*4+1]}, {ptr[centerIdx*4+2]}, {ptr[centerIdx*4+3]})");
}
```

Then after the conversion:
```csharp
// After line 149 (after BGRA2RGBA):
unsafe
{
	byte* ptr = (byte*)rgbaFrame.DataPointer;
	int centerIdx = (rgbaFrame.Height / 2) * rgbaFrame.Width + (rgbaFrame.Width / 2);
	Debug.WriteLine($"[After RGBA convert] Center RGBA: ({ptr[centerIdx*4]}, {ptr[centerIdx*4+1]}, {ptr[centerIdx*4+2]}, {ptr[centerIdx*4+3]})");
}
```

## Test Commands

1. **Stop sender**
2. **Run VirtualCamStudio** from Visual Studio with Debug output visible
3. **Load red image and start capture**
4. **Check Output window** for the before/after diagnostics
5. **Share the output** showing what bytes are actually being converted

Then we'll know if the problem is:
- Conversion not happening
- Conversion happening wrong
- UnityCapture expecting different format
- Something else entirely (stride, height sign, etc.)
