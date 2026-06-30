# Pre-Flight Checklist: Unity Video Capture Testing

## ✅ Completion Status

### Code Changes
- [x] **OBS UI removed** from MainWindow.xaml
- [x] **Unity-only controls** added to toolbar
- [x] **Unity status indicators** added to status bar
- [x] **OBS code-behind removed** from MainWindow.xaml.cs
- [x] **Build succeeds** with no errors
- [x] **UnityCaptureOutput.cs** exists and is complete
- [x] **UnityCaptureSender** native project built
- [x] **FrameIPC** implementation complete
- [x] **RenderPipeline** wired to OutputManager

### Documentation
- [x] **QUICK_START_TEST.md** created
- [x] **FRAME_FLOW_DIAGRAM.md** created
- [x] **OBS_REMOVAL_COMPLETE.md** created
- [x] **UNITY_CAPTURE_TEST_GUIDE.md** created
- [x] **IMPLEMENTATION_SUMMARY.md** created
- [x] **THIS FILE** created

---

## 🔧 Testing Checklist

### Pre-Test Verification
- [ ] **UnityCaptureSender.exe exists** at `UnityCaptureSender\x64\Release\UnityCaptureSender.exe`
- [ ] **UnityCapture.dll exists** in the same directory
- [ ] **VirtualCamStudio.exe exists** at `VirtualCamStudio\bin\Debug\net8.0-windows\VirtualCamStudio.exe`
- [ ] **Test media ready** (any JPG, PNG, MP4, or AVI file)

### Test Execution
- [ ] **1. Start UnityCaptureSender.exe**
  - Console opens
  - Shows "Initialized sender"
  - Shows "Named pipe server started"
  - Shows "Waiting for VirtualCamStudio to connect..."

- [ ] **2. Launch VirtualCam Studio**
  - Application window opens
  - UI shows only Unity controls (no OBS buttons)
  - Status bar shows "Unity Video Capture: ● Stopped" (gray)

- [ ] **3. Load media**
  - Drag photo/video into Media Library
  - Click thumbnail to load
  - Preview window shows the content

- [ ] **4. Start Unity capture**
  - Click "Start Unity Video Capture" button
  - Status bar changes to "● Running" (green)
  - Visual Studio Output shows "[UnityCaptureOutput] ✓ Connected"

- [ ] **5. Verify IPC connection**
  - UnityCaptureSender console shows "✓ Client connected"
  - Console shows "Received frame: 1920x1080"
  - IPC frames counter increases (30, 60, 90...)
  - Fallback frames counter stays at 0

- [ ] **6. Test with CloudPhone**
  - Launch CloudPhone
  - Navigate to camera settings
  - See "UnityVideoCapture" in camera list
  - Select UnityVideoCapture
  - **CRITICAL:** See your uploaded photo/video (NOT green screen)

### Success Indicators
- [ ] **Visual Studio Output:**
  ```
  [UnityCaptureOutput] FPS Sent: 30 | Failed: 0
  [Outputs.OutputManager.SendFrameAsync] ✓ UnityCaptureOutput completed
  ```

- [ ] **UnityCaptureSender Console:**
  ```
  Frames sent: 300 | IPC frames: 300 | Fallback frames: 0 | FPS: 30.0
  ```

- [ ] **CloudPhone Display:**
  - Shows your actual photo or video
  - Frame rate is smooth (~30 FPS)
  - No green or blue diagnostic frames

---

## ❌ Failure Scenarios

### Scenario 1: Connection Timeout
**Symptoms:**
- Studio shows: "[UnityCaptureOutput] ⚠️ Connection timeout"
- Sender shows: "Waiting for VirtualCamStudio to connect..."

**Fix:**
1. Close Studio
2. Verify UnityCaptureSender.exe is running
3. Launch Studio again
4. Click "Start Unity Video Capture"

### Scenario 2: High Fallback Frames
**Symptoms:**
- Sender shows: "Fallback frames: 30+" (increasing)
- IPC frames = 0 or low

**Fix:**
1. Stop Unity capture in Studio
2. Restart UnityCaptureSender.exe
3. Start Unity capture again

### Scenario 3: Green Screen Persists
**Symptoms:**
- CloudPhone shows green screen even after starting Unity capture
- Sender shows good IPC frame count

**Fix:**
1. Switch CloudPhone to different camera
2. Switch back to UnityVideoCapture
3. Or restart CloudPhone

### Scenario 4: Build Errors
**Symptoms:**
- "OBSManager not found" or similar errors

**Fix:**
- This shouldn't happen after our changes
- If it does, check that MainWindow.xaml.cs doesn't reference OBS
- Run Clean Solution → Rebuild

---

## 🎯 Quick Test Commands

### Terminal 1: Start Sender
```powershell
cd D:\Projects\VirtualCamStudio\UnityCaptureSender\x64\Release
.\UnityCaptureSender.exe
```

### Terminal 2: Build and Run Studio (if not using VS)
```powershell
cd D:\Projects\VirtualCamStudio
dotnet build
cd VirtualCamStudio\bin\Debug\net8.0-windows
.\VirtualCamStudio.exe
```

### Or Just Use Visual Studio
- Open solution
- Press F5
- Done!

---

## 📊 Expected Metrics

| Component | Metric | Expected Value |
|-----------|--------|----------------|
| **Studio** | Frame rate | 30 FPS |
| **Studio** | Frames sent | Increasing continuously |
| **Studio** | Frames failed | 0 |
| **Sender** | IPC frames | Matches Studio frames sent |
| **Sender** | Fallback frames | 0 (or only non-zero before Studio connects) |
| **Sender** | FPS | ~30.0 |
| **CloudPhone** | Frame rate | Smooth, no stuttering |

---

## 🐛 Debug Commands

### Check if Pipe Exists (PowerShell)
```powershell
Get-ChildItem \\.\pipe\ | Where-Object { $_.Name -like "*VirtualCamStudio*" }
```
Should show `VirtualCamStudio_Frames` when sender is running.

### Monitor Studio Debug Output
In Visual Studio:
- View → Output
- Select "Debug" from "Show output from:" dropdown
- Look for `[UnityCaptureOutput]` messages

### Check Process Status
```powershell
Get-Process | Where-Object { $_.Name -like "*VirtualCam*" -or $_.Name -like "*UnityCapture*" }
```

---

## 🎉 Success Definition

**Test is SUCCESSFUL when:**
1. ✅ UnityCaptureSender console shows IPC frames > 0, Fallback frames = 0
2. ✅ Studio Output shows "FPS Sent: 30 | Failed: 0"
3. ✅ CloudPhone displays your uploaded photo/video
4. ✅ Frame rate is smooth with no stuttering
5. ✅ No green or blue diagnostic frames visible

**If all 5 criteria met → Implementation is COMPLETE and WORKING!**

---

## 📝 Test Report Template

After testing, fill this out:

```
Test Date: _______________
Tester: _______________

PRE-TEST:
[ ] UnityCaptureSender built and ready
[ ] VirtualCamStudio built and ready
[ ] Test media prepared

TEST EXECUTION:
[ ] Sender started successfully
[ ] Studio launched successfully
[ ] Media loaded into Studio
[ ] Unity capture started (status shows green)
[ ] IPC connection established
[ ] CloudPhone detected UnityVideoCapture

RESULTS:
IPC Frames Count: ________
Fallback Frames Count: ________
Studio FPS: ________
Sender FPS: ________
CloudPhone Display: [ ] Correct Content  [ ] Green Screen  [ ] Other: ________

ISSUES ENCOUNTERED:
________________________________________________________________________
________________________________________________________________________

OUTCOME:
[ ] ✅ SUCCESS - CloudPhone shows uploaded content
[ ] ❌ FAILED - Describe issue: ________________________________________
```

---

## 🚀 Ready to Test?

**Start with:** QUICK_START_TEST.md  
**Reference:** FRAME_FLOW_DIAGRAM.md  
**Troubleshoot:** UNITY_CAPTURE_TEST_GUIDE.md  

**Everything is ready. The code is complete. The build succeeds. Now let's see your content in CloudPhone!**

---

## 📞 What to Report Back

After testing, please report:
1. **Did CloudPhone show your photo/video?** (Yes/No)
2. **IPC frames count from sender console** (number)
3. **Fallback frames count** (should be 0)
4. **Any errors in Studio Output window?** (copy/paste)
5. **Any errors in Sender console?** (copy/paste)

This will help diagnose any remaining issues quickly.

---

**Good luck with testing! 🎯**
