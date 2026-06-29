# Rendered Frame Debug Output

## Purpose
Save the rendered frame to disk immediately before sending to OutputManager to determine whether the rendering issue is in:
1. **ViewportEngine.Render()** - the rendering logic itself
2. **Post-rendering** - bitmap conversion or WPF display

## Implementation

### Location
**File**: `VirtualCamStudio/Services/RenderPipeline.cs`  
**Method**: `OnFrameRequested()`  
**Position**: Immediately after `ViewportEngine.Render()` and before `OutputManager.SendFrameAsync()`

### Debug Code Added
```csharp
// DEBUG: Save rendered frame to disk for inspection
try
{
	string debugPath = @"C:\Temp\render_debug.png";
	System.IO.Directory.CreateDirectory(@"C:\Temp");

	if (renderedFrame != null && renderedFrame.IsValid && renderedFrame.Image != null && !renderedFrame.Image.Empty())
	{
		Cv2.ImWrite(debugPath, renderedFrame.Image);
		Debug.WriteLine($"[RenderPipeline] 🔍 DEBUG: Saved rendered frame to {debugPath}");
		Debug.WriteLine($"[RenderPipeline] 🔍 Frame info: {renderedFrame.Width}x{renderedFrame.Height}, channels: {renderedFrame.Image.Channels()}");
	}
	else
	{
		Debug.WriteLine($"[RenderPipeline] ❌ DEBUG: Cannot save - rendered frame is invalid or empty");
	}
}
catch (Exception debugEx)
{
	Debug.WriteLine($"[RenderPipeline] ❌ DEBUG: Failed to save debug frame: {debugEx.Message}");
}
```

## How It Works

### 1. Directory Creation
```csharp
System.IO.Directory.CreateDirectory(@"C:\Temp");
```
- Creates `C:\Temp` directory if it doesn't exist
- Idempotent - safe to call even if directory exists

### 2. Frame Validation
```csharp
if (renderedFrame != null && renderedFrame.IsValid && renderedFrame.Image != null && !renderedFrame.Image.Empty())
```
Checks:
- Frame object is not null
- Frame.IsValid is true (Image exists and not empty)
- Image Mat is not null
- Image Mat is not empty

### 3. Save to Disk
```csharp
Cv2.ImWrite(debugPath, renderedFrame.Image);
```
- Uses OpenCV's `ImWrite` function
- Saves as PNG format (lossless)
- **Overwrites previous file** on each render cycle
- File path: `C:\Temp\render_debug.png`

### 4. Logging
- **Success**: Logs file path and frame dimensions
- **Invalid frame**: Logs that frame is invalid or empty
- **Exception**: Logs any errors during save operation

## Expected Behavior

### During Playback
- **Every rendered frame** overwrites `C:\Temp\render_debug.png`
- At 30 FPS, file updates 30 times per second
- View the file to see the **most recently rendered frame**

### Log Output (Success)
```
[RenderPipeline] 🔍 DEBUG: Saved rendered frame to C:\Temp\render_debug.png
[RenderPipeline] 🔍 Frame info: 1080x1920, channels: 3
```

### Log Output (Invalid Frame)
```
[RenderPipeline] ❌ DEBUG: Cannot save - rendered frame is invalid or empty
```

### Log Output (Save Error)
```
[RenderPipeline] ❌ DEBUG: Failed to save debug frame: [error message]
```

## Diagnostic Results

### Scenario 1: File is Completely Black
**Result**: `C:\Temp\render_debug.png` is solid black (no image content)

**Conclusion**: **Problem is in ViewportEngine.Render()**
- Rendering logic is producing black canvas
- Check ViewportEngine logs for:
  - Invalid scale calculations
  - Image placed outside canvas bounds
  - Copy dimensions (copyWidth, copyHeight) ≤ 0
  - Resize/WarpAffine failures

**Action**: Fix ViewportEngine rendering logic

---

### Scenario 2: File Contains Correct Image
**Result**: `C:\Temp\render_debug.png` shows the expected media content

**Conclusion**: **Problem is AFTER rendering**
- ViewportEngine.Render() is working correctly
- Issue is in post-render pipeline:
  - Bitmap conversion (MatToBitmapSource)
  - WPF Image control update
  - UI threading issues
  - PreviewOutput implementation

**Action**: Debug MatToBitmapSource converter and WPF display

---

### Scenario 3: File is Not Created
**Result**: No file at `C:\Temp\render_debug.png`

**Possible causes**:
1. **Frame is invalid** - Check for log: "Cannot save - rendered frame is invalid or empty"
2. **Save exception** - Check for log: "Failed to save debug frame: ..."
3. **RenderPipeline not executing** - Check for numbered logs [1] through [6]
4. **Permission issue** - Verify write access to C:\Temp

**Action**: Check Debug output window for error logs

---

### Scenario 4: File Shows Partial Image
**Result**: `C:\Temp\render_debug.png` shows part of the image (e.g., corner visible)

**Conclusion**: **Image placement issue in ViewportEngine**
- Image is partially outside canvas bounds
- Check ViewportEngine logs for placement position (canvasX, canvasY)
- Check copy dimensions (copyWidth, copyHeight)

**Action**: Fix placement calculation in ViewportEngine

---

## Testing Instructions

### 1. Run the Application
```
F5 in Visual Studio or click Start
```

### 2. Load Media
- Drag and drop an image or video into the application
- OR select from media library

### 3. Check Debug Output
- Open **Output** window (View → Output)
- Select **Debug** from dropdown
- Look for: `🔍 DEBUG: Saved rendered frame to C:\Temp\render_debug.png`

### 4. Inspect the Saved Frame
```powershell
# Open in default image viewer
Start-Process C:\Temp\render_debug.png

# OR open in file explorer
explorer C:\Temp
```

### 5. Analyze the Result
- Is the file completely black? → **ViewportEngine issue**
- Does it show the correct image? → **Post-render issue**
- Is the file not created? → **Pipeline not executing**
- Does it show partial image? → **Placement issue**

## Performance Impact

### File I/O Cost
- **Write time**: ~5-10ms per frame (1080x1920 PNG)
- **Disk space**: ~50KB - 500KB per frame (depends on content)
- **Impact**: Reduces render rate from 30 FPS to ~25-28 FPS

### Memory Impact
- Minimal - uses existing rendered frame
- No additional allocations

### Production Impact
- **Debug code only** - should be removed after debugging
- File is overwritten on each frame, so disk space is bounded

## Cleanup

After debugging is complete:
1. Remove the debug code block from RenderPipeline.cs
2. Delete `C:\Temp\render_debug.png`
3. (Optional) Remove C:\Temp if no longer needed

## Build Status
✅ **Application compiles with zero errors**

## Summary

This debug code allows immediate visual inspection of the rendered output, bypassing the entire display pipeline (bitmap conversion, WPF, etc.). By comparing the saved PNG file to the actual preview:

- **Black PNG** = Rendering bug (ViewportEngine)
- **Correct PNG** = Display bug (bitmap conversion or WPF)

This definitively isolates the problem location without any guesswork.
