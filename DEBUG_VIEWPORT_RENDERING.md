# ViewportEngine Rendering Debug Logging

## Purpose
Add comprehensive diagnostic logging to `ViewportEngine.Render()` to identify why the preview shows a dark/black canvas despite successful media loading.

## Symptoms
- ✓ Media loads successfully
- ✓ Placeholder disappears
- ✓ Preview area becomes dark (black)
- ✓ Pan cursor is active
- ✓ Zoom changes values
- ❌ **No image or video is visible**

## Logging Added

### 1. Source Frame Information
Logs at the start of `Render()`:
- **Source Width** - Original media width
- **Source Height** - Original media height
- **Source Channels** - Color channels (1=gray, 3=BGR, 4=BGRA)
- **Source Type** - OpenCV MatType
- **Source Empty** - Whether source Mat is empty

**Purpose**: Verify the source frame is valid and has content.

### 2. Canvas Information
- **Canvas Width** - Target render canvas width
- **Canvas Height** - Target render canvas height
- **Canvas Created** - Confirmation black canvas was created
- **Canvas Empty** - Should be false

**Purpose**: Verify the destination canvas is properly initialized.

### 3. Framing Settings
- **Zoom** - Zoom multiplier (1.0 = 100%)
- **OffsetX** - Horizontal pan offset
- **OffsetY** - Vertical pan offset
- **Rotation** - Rotation angle in degrees

**Purpose**: Verify framing parameters are reasonable values.

### 4. Scale Calculations
- **BaseScale** - Calculated scale to fit source onto canvas (aspect ratio preserved)
- **TotalScale** - BaseScale × Zoom
- **ScaledWidth** - Source width after scaling
- **ScaledHeight** - Source height after scaling
- **CenterX** - Canvas horizontal center point
- **CenterY** - Canvas vertical center point
- **ScaledCenterX** - Scaled image horizontal center
- **ScaledCenterY** - Scaled image vertical center

**Purpose**: Verify scale calculations produce reasonable dimensions.

### 5. Transform Path Selection
Logs which rendering path is taken:
- **"Using ROTATION path"** - When rotation != 0
- **"Using NO ROTATION path"** - When rotation == 0

**Purpose**: Confirm the correct rendering code path is executed.

### 6. After Resize Operation
- **scaled.Width** - Width after Cv2.Resize()
- **scaled.Height** - Height after Cv2.Resize()
- **scaled.Empty()** - Should be false

**Purpose**: Verify the resize operation produces valid output.

### 7. After WarpAffine Operation (Rotation Path Only)
- **rotated.Width** - Width after rotation
- **rotated.Height** - Height after rotation
- **rotated.Empty()** - Should be false

**Purpose**: Verify rotation produces valid output.

### 8. Placement Position
- **canvasX (TranslateX)** - X position where scaled image will be placed
- **canvasY (TranslateY)** - Y position where scaled image will be placed

**Purpose**: **CRITICAL** - Verify the placement position is within canvas bounds.

**Expected**: For centered image with no pan:
- canvasX should be near `(canvasWidth - scaledWidth) / 2`
- canvasY should be near `(canvasHeight - scaledHeight) / 2`

**If values are far outside [0, canvasWidth/canvasHeight], the image will be off-canvas!**

### 9. PlaceImageOnCanvas Details
Logs inside the placement function:
- **Input dimensions** - Canvas size, image size, position
- **Adjusted coordinates** - After handling negative positions
- **Copy region** - srcX, srcY, dstX, dstY, copyWidth, copyHeight
- **Copy result** - Whether CopyTo was performed

**Critical check**:
```
if (copyWidth > 0 && copyHeight > 0)
	✓ Copy performed
else
	❌ NO COPY PERFORMED - image is completely outside canvas bounds!
```

**Purpose**: **CRITICAL** - If `copyWidth` or `copyHeight` is ≤ 0, **nothing is copied to the canvas**, resulting in a black preview!

### 10. Final Canvas State
- **Final Width** - Should match requested canvas size
- **Final Height** - Should match requested canvas size
- **Final Empty** - Should be false
- **Final Channels** - Should be 3 (BGR)
- **PixelFormat** - Should be BGR

**Purpose**: Verify the returned canvas is valid.

### 11. Exception Logging
If any exception occurs during rendering:
- **Exception message**
- **Stack trace**

**Purpose**: Catch any OpenCV errors or crashes.

## How to Use This Logging

### Run the Application
1. Build and run the application
2. Open the **Output** window in Visual Studio
3. Select **Debug** from the dropdown
4. Load an image or video
5. Watch for the ViewportEngine log blocks

### What to Look For

#### Scenario 1: Image Completely Outside Canvas
```
[PlaceImageOnCanvas] Computed copy region:
  - copyWidth: 0 (or negative)
  - copyHeight: 0 (or negative)
❌ NO COPY PERFORMED - image is completely outside canvas bounds!
```

**Root cause**: `canvasX` or `canvasY` values place the image entirely outside the canvas.

**Common causes**:
- Incorrect pan offset calculation
- Canvas center calculation error
- Wrong coordinate system

#### Scenario 2: Scaled Dimensions Too Large or Too Small
```
[ViewportEngine] Calculated:
  - ScaledWidth: 0 (or negative, or extremely large)
  - ScaledHeight: 0 (or negative, or extremely large)
```

**Root cause**: Scale calculation produced invalid dimensions.

**Common causes**:
- BaseScale calculation error (division by zero)
- Extreme zoom values
- Invalid source dimensions

#### Scenario 3: Source Frame Invalid
```
[ViewportEngine] ❌ Source is EMPTY - returning empty frame
```

**Root cause**: MediaController returned an empty Mat.

**Common causes**:
- Frame was disposed before rendering
- GetCurrentFrame() returned null
- Image failed to load

#### Scenario 4: Resize or WarpAffine Failed
```
[ViewportEngine] After Resize:
  - scaled.Empty(): true
```

**Root cause**: OpenCV operation failed silently.

**Common causes**:
- Invalid source Mat
- Invalid dimensions (0 or negative)
- Memory allocation failure

#### Scenario 5: Exception During Rendering
```
[ViewportEngine] ❌ EXCEPTION during rendering: ...
```

**Root cause**: OpenCV operation threw exception.

**Common causes**:
- Invalid Mat access
- Out of memory
- Corrupted image data

## Expected Normal Output (Example)

For a successful render with a 1920x1080 image on a 1080x1920 canvas:

```
╔══════════════════════════════════════════════════════════════════════════════
║ ViewportEngine.Render() - START
╚══════════════════════════════════════════════════════════════════════════════
[ViewportEngine] Source:
  - Width: 1920
  - Height: 1080
  - Channels: 3
  - Type: CV_8UC3
[ViewportEngine] Canvas:
  - Width: 1080
  - Height: 1920
[ViewportEngine] Framing:
  - Zoom: 1
  - OffsetX: 0
  - OffsetY: 0
  - Rotation: 0
[ViewportEngine] Canvas created:
  - Width: 1080
  - Height: 1920
  - Empty: False
[ViewportEngine] Calculated:
  - BaseScale: 0.5625 (1080/1920 = 0.5625)
  - TotalScale: 0.5625
  - ScaledWidth: 1080
  - ScaledHeight: 607
  - CenterX: 540.00
  - CenterY: 960.00
  - ScaledCenterX: 540.00
  - ScaledCenterY: 303.50
[ViewportEngine] Using NO ROTATION path (rotation == 0)
[ViewportEngine] After Resize:
  - scaled.Width: 1080
  - scaled.Height: 607
  - scaled.Empty(): False
[ViewportEngine] Placement position:
  - canvasX (TranslateX): 0
  - canvasY (TranslateY): 656
[PlaceImageOnCanvas] Called with:
  - canvas: 1080x1920, empty: False
  - image: 1080x607, empty: False
  - position: x=0, y=656
[PlaceImageOnCanvas] Computed copy region:
  - srcX: 0, srcY: 0
  - dstX: 0, dstY: 656
  - copyWidth: 1080
  - copyHeight: 607
[PlaceImageOnCanvas] ✓ Copy dimensions valid, performing CopyTo...
[PlaceImageOnCanvas] ✓ CopyTo completed successfully
[ViewportEngine] Final canvas state:
  - Width: 1080
  - Height: 1920
  - Empty: False
  - Channels: 3
[ViewportEngine] Returning Frame with PixelFormat: BGR
╔══════════════════════════════════════════════════════════════════════════════
║ ViewportEngine.Render() - END
╚══════════════════════════════════════════════════════════════════════════════
```

## Next Steps After Running

1. **Check the logs** for the patterns described above
2. **Identify which stage fails** or produces unexpected values
3. **Report the findings** including:
   - Source dimensions
   - Canvas dimensions
   - Calculated scale values
   - Placement position (canvasX, canvasY)
   - Copy dimensions (copyWidth, copyHeight)
   - Any error messages

## Build Status
✅ **Application compiles with zero errors**

## Notes
- Logging is temporary for debugging purposes
- Performance impact is minimal (only console writes)
- Logging should be removed after issue is resolved
- OutputManager was not modified as requested
