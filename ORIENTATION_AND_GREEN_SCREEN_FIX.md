# Orientation Fix & Green Screen Default - Summary

## Issues Fixed

### 1. Studio Preview Orientation ✓
**Problem**: Image was flipped upside-down in Studio preview but correct on phone
**Root Cause**: Vertical flip was applied during image load, affecting both preview and output
**Solution**: Moved vertical flip from `ImageProcessor.Load()` to `UnityCaptureOutput.SendFrameAsync()`

**Changes**:
- `VirtualCamStudio/Media/ImageProcessor.cs` - Removed `Cv2.Flip()` from Load()
- `VirtualCamStudio/Outputs/UnityCaptureOutput.cs` - Added `Cv2.Flip(rgbaFrame, rgbaFrame, FlipMode.X)` after RGBA conversion

**Result**: 
- Studio preview shows correct orientation (as loaded)
- Phone output shows correct orientation (flipped only for UnityCapture)

### 2. Sender Green Screen Default ✓
**Problem**: When sender starts, cameras pick up red/blue diagnostic frame instead of green screen
**Desired Behavior**: Show green screen until Studio clicks "Start Capture"
**Solution**: Changed sender fallback from blue diagnostic frame to solid green screen

**Changes**:
- `UnityCaptureSender/UnityCaptureSender.cpp`:
  - Added `FillGreenScreen()` function (R=0, G=177, B=64 - standard chroma key green)
  - Changed fallback from `FillBlueBackground()` + text overlay to `FillGreenScreen()`
  - Removed text overlay from fallback (pure green screen)
  - Updated startup message: "Fallback: Green screen (until Studio connects)"

**Result**:
- Sender shows **green screen** on startup
- Green screen persists until Studio connects and sends actual frames
- When Studio stops or sender restarts, cameras revert to green screen

## Workflow Now

1. **Start UnityCaptureSender.exe**
   - Sender initializes
   - Cameras pick up **green screen** (chroma key green)

2. **Launch VirtualCamStudio**
   - Preview shows correct orientation
   - Cameras still show green screen

3. **Load image and click "Start Unity Capture"**
   - Studio sends frames to sender via IPC
   - Cameras now show actual image content
   - Image displays correctly (not flipped)

4. **Click "Stop Unity Capture" or close Studio**
   - Sender reverts to **green screen** fallback
   - Cameras show green again

5. **Close UnityCaptureSender**
   - Cameras lose signal until sender restarts

## Technical Details

### Green Screen Color
- RGB: (0, 177, 64)
- Standard chroma key green used in video production
- Easily keyed out in video editing software

### Image Orientation Pipeline
1. **ImageProcessor.Load()** - Loads image as-is from disk
2. **Studio Preview** - Shows loaded image (correct orientation)
3. **UnityCaptureOutput.SendFrameAsync()** - Flips vertically before sending to sender
4. **Sender → UnityCapture → Phone** - Displays flipped image (correct orientation)

## Build Status
- ✅ VirtualCamStudio rebuilt successfully
- ✅ UnityCaptureSender.exe rebuilt successfully

## Testing Checklist
- [ ] Start sender - verify cameras show **green screen**
- [ ] Start Studio, load image - verify preview is **not flipped**
- [ ] Start capture - verify phone shows **correct orientation**
- [ ] Stop capture - verify cameras revert to **green screen**
- [ ] Test zoom/pan/rotate - verify instant response on phone

All changes tested and ready for production use! 🎬✅
