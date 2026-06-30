# Quick Test Guide - Green Screen & Orientation Fix

## Test 1: Green Screen Default ✓

### Steps:
1. **Launch UnityCaptureSender.exe**
   ```
   D:\Projects\VirtualCamStudio\UnityCaptureSender\UnityCaptureSender.exe
   ```

2. **Check cameras (CloudPhone/WebcamTest)**
   - Should show **GREEN SCREEN** (not red, not blue)
   - No text overlay
   - Pure chroma key green color

3. **Leave sender running** - green screen should persist

### Expected Output:
```
=============================================================
  UnityCaptureSender - Frame Forwarder
  Receives from VirtualCamStudio, sends to Unity Video Capture
=============================================================

Configuration:
  Resolution: 1920x1080
  Frame Rate: 30 FPS
  Fallback: Green screen (until Studio connects)

Starting frame transmission...
```

---

## Test 2: Studio Preview Orientation ✓

### Steps:
1. **Launch VirtualCamStudio**

2. **Load an image** (drag & drop or click to load)

3. **Check Studio preview**
   - Image should be **RIGHT-SIDE-UP** (not flipped)
   - Same orientation as the original file

4. **Camera still shows green screen** at this point

---

## Test 3: Capture with Correct Orientation ✓

### Steps:
1. **Click "Start Unity Capture"** in Studio

2. **Check cameras (CloudPhone/WebcamTest)**
   - Should now show **your image** (not green screen)
   - Image should be **RIGHT-SIDE-UP** (correct orientation)

3. **Test controls:**
   - Move **Zoom** slider → instant response on camera
   - Move **X/Y Pan** sliders → instant response
   - Move **Rotation** slider → instant response

---

## Test 4: Stop Capture Returns to Green ✓

### Steps:
1. **Click "Stop Unity Capture"** in Studio

2. **Check cameras**
   - Should revert to **GREEN SCREEN** immediately
   - No diagnostic frames, no text

3. **Optional: Close/restart sender**
   - Should always start with green screen
   - Green persists until Studio starts capture again

---

## Quick Verification Checklist

- [ ] Sender starts with **green screen** (not red/blue diagnostic)
- [ ] Studio preview shows image **right-side-up**
- [ ] Camera shows **green** before capture starts
- [ ] Camera shows **correct orientation** after capture starts
- [ ] Zoom/pan/rotate updates are **instant** (no lag)
- [ ] Stop capture returns to **green screen**

---

## If Issues Occur

### Green screen is wrong color:
- Should be RGB(0, 177, 64) - standard chroma key green
- If it's blue, old sender may still be running

### Studio preview is upside-down:
- Rebuild VirtualCamStudio (should be done already)
- Restart Studio

### Camera shows upside-down image:
- Should be fixed now - if not, check that new sender is running
- Timestamp should be recent (18:55:29 or later)

---

## File Locations

- **Sender**: `D:\Projects\VirtualCamStudio\UnityCaptureSender\UnityCaptureSender.exe`
- **Studio**: Launch from Visual Studio or build output
- **Build timestamp**: June 30, 2026 18:55:29 (latest)

---

**All systems ready for testing!** 🟩✅
