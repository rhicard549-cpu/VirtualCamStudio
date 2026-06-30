# UnityCapture IPC - Quick Reference Card

## 🚀 Quick Start (3 Steps)

### Step 1: Start UnityCaptureSender
```powershell
.\Start-UnityCaptureSender.ps1
```
Or manually:
```powershell
.\UnityCaptureSender\bin\Debug\x64\UnityCaptureSender\UnityCaptureSender.exe
```

### Step 2: Launch VirtualCamStudio
- Press F5 in Visual Studio, or
- Run the compiled executable

### Step 3: Enable UnityCapture
1. Click **"Start UnityCapture"** button (blue) in toolbar
2. Load media (image or video)
3. Watch UnityCaptureSender console for IPC frames

---

## 📊 Console Output Reference

### UnityCaptureSender Console

#### Waiting for Connection:
```
FPS Attempted: 30 | Sent: 30 | Failed: 0 | IPC: 0 | Diagnostic: 30
```
- **IPC: 0** = No VirtualCamStudio connection yet
- **Diagnostic: 30** = Blue fallback frames showing

#### Connected & Receiving:
```
FPS Attempted: 30 | Sent: 30 | Failed: 0 | IPC: 30 | Diagnostic: 0
```
- **IPC: 30** = Receiving frames from VirtualCamStudio ✓
- **Diagnostic: 0** = Fallback not needed

#### UnityCapture Inactive:
```
FPS Attempted: 30 | Sent: 0 | Failed: 30 | IPC: 30 | Diagnostic: 0
```
- **Failed: 30** = Unity not running or plugin inactive
- **IPC: 30** = Still receiving from Studio (frames queued)

---

## 🎛️ UI Controls

### Toolbar Buttons
- **Start UnityCapture** (Blue #0066cc)
  - Creates `UnityCaptureOutput`
  - Registers with `OutputManager`
  - Enables Stop button

- **Stop UnityCapture** (Red #cc0000)
  - Unregisters output
  - Disposes IPC client
  - Enables Start button

### Status Messages
- **"UnityCapture output started (waiting for UnityCaptureSender)"** = OK, sender may not be running yet
- **"UnityCapture output started"** = Connected successfully
- **"UnityCapture output stopped"** = Cleanly shut down

---

## 🔌 IPC Connection States

| Studio State | Sender State | Result |
|--------------|-------------|--------|
| Stopped | Running | Sender shows diagnostic frames |
| Started | Stopped | Studio shows connection errors |
| Started | Running | ✓ IPC connected, frames transmitting |
| Stopped | Stopped | No activity |

**Auto-reconnect**: Both ends will automatically reconnect when the other becomes available

---

## 🐛 Common Issues & Solutions

### "Connection timeout - UnityCaptureSender not available"
→ Start `UnityCaptureSender.exe` first

### IPC always 0 in console
→ Click "Start UnityCapture" in VirtualCamStudio

### Diagnostic always 30 in console  
→ VirtualCamStudio not connected yet, or crashed

### Failed always 30 in console
→ Unity Editor not running, launch Unity

### Black frames in Unity
→ Load media in VirtualCamStudio viewport

---

## 📁 Key Files

| File | Purpose |
|------|---------|
| `UnityCaptureSender.exe` | Native sender process |
| `UnityCaptureOutput.cs` | C# IPC client |
| `FrameIPC.cpp` | Named pipe protocol |
| `shared.inl` | UnityCapture protocol |
| `Start-UnityCaptureSender.ps1` | Launch helper |

---

## 🔧 IPC Protocol at a Glance

**Named Pipe**: `\\.\pipe\VirtualCamStudio_Frames`  
**Direction**: VirtualCamStudio (client) → UnityCaptureSender (server)  
**Frame Format**: 
```
[Header: 20 bytes] + [Pixels: width×height×4 bytes]
```

**Header Structure**:
```cpp
uint32_t width;       // 1920
uint32_t height;      // 1080  
uint32_t stride;      // 1920
uint32_t dataSize;    // 8294400 (1920×1080×4)
uint32_t pixelFormat; // 0 (RGBA32)
```

---

## 📚 Full Documentation

- **Architecture & Usage**: `UnityCapture_IPC_Integration_Guide.md`
- **Implementation Details**: `IMPLEMENTATION_COMPLETE.md`
- **Research Materials**: `Research/UnityCapture/`

---

## ⚡ Performance Targets

- **Frame Rate**: 30 FPS
- **Resolution**: 1920×1080
- **Format**: RGBA32
- **Throughput**: ~249 MB/s
- **Latency**: < 20ms end-to-end

---

## ✅ Verification Checklist

- [ ] UnityCaptureSender.exe starts without errors
- [ ] Console shows "Waiting for VirtualCamStudio connection..."
- [ ] Blue diagnostic frame visible (if Unity running)
- [ ] VirtualCamStudio launches successfully
- [ ] "Start UnityCapture" button clickable
- [ ] Console switches to IPC frames after click
- [ ] Media loads and renders in viewport
- [ ] Unity shows VirtualCamStudio output

---

**Last Updated**: Current session  
**Status**: ✅ Complete and ready for testing
