# VirtualCamStudio Diagnostic Analysis Guide

## Current Test Status

✅ **UnityCaptureSender** is running (PID: visible in sender console window)
✅ **VirtualCamStudio** is running (PID: visible in Studio window)

Both applications have been instrumented with byte-level diagnostic logging.

---

## What To Do Now

### 1. In VirtualCamStudio Window:
   - Click **"Start Unity Capture"** button
   - Click **"Add Image"** or **"Add Video"**
   - Select a test file (preferably a simple, colorful image)
   - Observe the preview

### 2. Watch the UnityCaptureSender Console Window for:
   ```
   [FrameIPC] VirtualCamStudio connected.
   [FrameIPC] Received frame 30: 1920x1080, first pixel: (123,45,67,255)
   [DEBUG] Sending frame 30 to UnityCapture: 1920x1080, first pixel: (123,45,67,255)
   ```

   These appear every 30 frames and show actual pixel bytes being received and sent.

### 3. In Visual Studio Output Window (Debug):
   - Open **View > Output**
   - Set dropdown to **Debug**
   - Look for:
	 ```
	 [UnityCaptureOutput] Connected to sender pipe
	 [UnityCaptureOutput] Frame: 1920x1080, First pixel BGRA: (67,45,123,255)
	 [UnityCaptureOutput] Frame: Center pixel BGRA: (100,200,150,255)
	 ```

### 4. Open CloudPhone or Webcamtests:
   - https://webcamtests.com/
   - Select **"Unity Video Capture"** as camera
   - **Observe what is displayed**

---

## Diagnostic Analysis

### Case 1: Green Screen in CloudPhone
**Symptom:** CloudPhone shows solid green
**Meaning:** Sender is generating diagnostic frames because it's NOT receiving IPC frames from Studio
**Check:**
- Does sender console show `[FrameIPC] VirtualCamStudio connected.`?
- Does Studio Debug output show `[UnityCaptureOutput] Connected to sender pipe`?
- If NO → IPC connection failed
- If YES but no frame logs → Studio not sending frames (check if capture was started)

### Case 2: Purple Screen in CloudPhone  
**Symptom:** CloudPhone shows solid purple or corruption
**Meaning:** Frames are flowing but there's a format/interpretation mismatch
**Check:**
- Compare pixel values in all three places:
  1. Studio BGRA output
  2. Sender received (should be RGBA after conversion)
  3. Sender sent to UnityCapture
- If values match → Problem is in UnityCapture driver or webcam client
- If values don't match → Problem in IPC or conversion

### Case 3: Wrong Image/Colors
**Symptom:** CloudPhone shows something but colors are wrong
**Check pixel byte patterns:**

Example for a RED pixel:
- Studio BGRA: `(0, 0, 255, 255)` → Blue=0, Green=0, Red=255, Alpha=255
- Sender RGBA: `(255, 0, 0, 255)` → Red=255, Green=0, Blue=0, Alpha=255

If Studio says `(0,0,255,255)` but Sender receives `(0,0,255,255)`:
→ **BGRA → RGBA conversion is NOT happening!**

If Sender receives correct RGBA but CloudPhone shows wrong colors:
→ **UnityCapture expects different format or has channel swap**

### Case 4: Still Shows Uploaded Image Correctly
**Symptom:** Everything works!
**Meaning:** The diagnostic logging revealed the fix, or a previous change worked

---

## Key Questions to Answer

Record the following from your test:

### From Sender Console:
- [ ] Did you see `[FrameIPC] VirtualCamStudio connected.`? (YES/NO)
- [ ] Frame dimensions received: _______ x _______
- [ ] First pixel RGBA values: `(___,___,___,___)`
- [ ] Are frame logs appearing every 30 frames? (YES/NO)

### From Studio Debug Output:
- [ ] Did you see `[UnityCaptureOutput] Connected to sender pipe`? (YES/NO)
- [ ] Frame dimensions sent: _______ x _______
- [ ] First pixel BGRA values: `(___,___,___,___)`
- [ ] Center pixel BGRA values: `(___,___,___,___)`

### From CloudPhone/Webcamtests:
- [ ] What do you see? (describe color/pattern)
- [ ] Does it change when you load different images? (YES/NO)
- [ ] Does the Studio preview show the correct image? (YES/NO)

---

## Next Steps Based on Findings

### If pixel bytes MATCH across Studio → Sender but CloudPhone shows wrong colors:
We need to investigate UnityCapture driver format expectations. Possible issues:
- UnityCapture might expect BGRA not RGBA
- Stride calculation might be wrong
- Vertical flip might be needed

### If pixel bytes DON'T MATCH between Studio and Sender:
The IPC transmission or format conversion has a bug:
- Check if BGRA→RGBA conversion is actually happening
- Verify byte order in named pipe
- Check buffer sizes and strides

### If no frames are flowing at all:
Connection issue:
- Named pipe not connecting
- Studio not starting capture properly
- Sender not reading pipe

---

## Copy This Information

When you reply, please copy/paste:

1. **Sender Console Output** (the frame logs showing pixel values)
2. **Studio Debug Output** (the BGRA pixel value logs)
3. **What CloudPhone Shows** (description or screenshot)
4. **Answers to the Key Questions** above

This will allow us to pinpoint the exact issue in the pipeline.

---

## To Stop Testing

Press **Enter** in the PowerShell window running the diagnostic script, or close both windows manually.
