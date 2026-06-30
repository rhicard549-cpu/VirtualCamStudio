# Frame Trace Diagnostics - Added

This document describes the comprehensive frame tracing diagnostics added to identify why a blue diagnostic frame is being sent instead of the rendered viewport.

## Purpose

Track every frame through the entire pipeline from source to SharedImageMemory to identify:
1. Where the frame content changes
2. If ViewportEngine is receiving and returning valid frames
3. If a fallback to a diagnostic frame occurs anywhere
4. If the blue frame is coming from an external source (e.g., UnityCaptureSender.exe)

## Diagnostic Points Added

### 1. **RenderPipeline.OnFrameRequested()**
   - **Location**: `VirtualCamStudio/Services/RenderPipeline.cs`
   - **Logs**:
	 - "Fallback reason: No media loaded" - When `_mediaController.HasMedia` is false
	 - "Fallback reason: MediaController returned null/empty frame" - When `GetCurrentFrame()` returns null/empty
	 - "Note: Using default canvas dimensions (no active profile)" - When no camera profile is selected

### 2. **ViewportEngine.Render() - INPUT**
   - **Location**: `VirtualCamStudio/Media/ViewportEngine.cs` (start of method)
   - **Logs**:
	 - Source Mat dimensions, channels, type
	 - **Pixel sampling**: RGB values of top-left pixel `[0,0]`
	 - "Fallback reason: Source Mat is empty" - If source is empty
   - **Purpose**: Verify the input frame from MediaController is valid

### 3. **ViewportEngine.Render() - OUTPUT**
   - **Location**: `VirtualCamStudio/Media/ViewportEngine.cs` (end of method)
   - **Logs**:
	 - Final canvas dimensions, channels
	 - **Pixel sampling**: RGB values of top-left pixel `[0,0]`
	 - **Blue frame detection**: "WARNING: ViewportEngine output is SOLID BLUE" if pixel is (0, 0, 255)
   - **Purpose**: Verify the rendered frame content before returning

### 4. **UnityCaptureOutputService.SendFrameAsync() - INPUT**
   - **Location**: `VirtualCamStudio/Services/UnityCaptureOutputService.cs`
   - **Logs**:
	 - Frame validity checks (null, empty, dimensions, channels)
	 - **Pixel sampling**: RGB values of top-left and center pixels (BGR format)
	 - "Fallback reason: Received null/empty frame from RenderPipeline" - If frame is invalid

### 5. **UnityCaptureOutputService.SendFrameAsync() - AFTER COLOR CONVERSION**
   - **Location**: `VirtualCamStudio/Services/UnityCaptureOutputService.cs`
   - **Logs**:
	 - RGBA frame dimensions and type
	 - **Pixel sampling**: RGBA values of top-left and center pixels
	 - **Blue frame detection**: "WARNING: Frame appears to be SOLID BLUE (diagnostic frame?)"
   - **Purpose**: Verify color conversion didn't corrupt data

### 6. **UnityCaptureOutputService.SendFrameAsync() - BEFORE SharedImageMemory**
   - **Location**: `VirtualCamStudio/Services/UnityCaptureOutputService.cs`
   - **Logs**:
	 - Final dimensions and buffer pointer
	 - **Pixel sampling**: RGB values of first pixel before calling `Send()`
   - **Purpose**: Confirm the exact data being written to shared memory

## Expected Output Pattern

When working correctly, you should see:

```
[ViewportEngine] Source Pixel[0,0] RGB: (R, G, B)  ← Non-blue values
[ViewportEngine] Output Pixel[0,0] RGB: (R, G, B)  ← Same or letterboxed values
[UnityCaptureOutputService] Pixel[0,0] RGB: (R, G, B)  ← Matching
[UnityCaptureOutputService] RGBA Pixel[0,0]: (R, G, B, A)  ← Color-converted
[UnityCaptureOutputService] Final pixel before Send(): RGB=(R, G, B)  ← Final validation
```

## Blue Frame Indicators

If you see **ANY** of these, the blue frame is coming from your C# pipeline:

```
⚠️ WARNING: ViewportEngine output is SOLID BLUE
⚠️ WARNING: Frame appears to be SOLID BLUE (diagnostic frame?)
```

If pixel values are **NOT** blue in the logs but ManyCam shows blue:
- The blue frame is coming from **UnityCaptureSender.exe** (the C++ test app)
- **Solution**: Make sure `UnityCaptureSender.exe` is NOT running at the same time

## Fallback Reasons

All fallback conditions now explicitly log their reason:

1. **"Fallback reason: No media loaded"** - No image/video loaded in MediaController
2. **"Fallback reason: MediaController returned null/empty frame"** - Media playback issue
3. **"Fallback reason: Source Mat is empty"** - ViewportEngine received empty input
4. **"Fallback reason: Received null/empty frame from RenderPipeline"** - Pipeline failed

## How to Use These Diagnostics

1. **Run VirtualCamStudio** with media loaded
2. **Click "Connect UnityCapture"** and **"Start Streaming"**
3. **Check the Debug Output** in Visual Studio (View → Output → Debug)
4. **Look for**:
   - Pixel RGB values at each stage
   - Any "SOLID BLUE" warnings
   - Any "Fallback reason" messages
5. **Compare** the pixel values through the pipeline to identify where content changes

## Expected Issues

### Issue 1: UnityCaptureSender.exe is running
- **Symptom**: All C# logs show correct RGB values, but ManyCam shows blue
- **Root Cause**: The C++ test app overwrites shared memory with blue frames
- **Solution**: Kill `UnityCaptureSender.exe` process

### Issue 2: No media loaded
- **Symptom**: "Fallback reason: No media loaded"
- **Root Cause**: No image/video in the media list
- **Solution**: Load media via the UI

### Issue 3: Black canvas with no content
- **Symptom**: All pixels are (0, 0, 0)
- **Root Cause**: Framing settings or source dimensions causing no visible content
- **Solution**: Check zoom, offset, canvas dimensions

## Build Status

✅ **Build successful with zero errors**

All diagnostics are in place and ready for testing.
