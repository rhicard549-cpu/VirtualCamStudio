# VirtualCam Studio → CloudPhone Frame Flow

## Complete Data Flow Diagram

```
┌──────────────────────────────────────────────────────────────────┐
│                     VirtualCam Studio (WPF)                       │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  1. USER LOADS MEDIA                                             │
│     ┌───────────────┐                                            │
│     │  Photo/Video  │ (JPG, PNG, MP4, AVI)                       │
│     └───────┬───────┘                                            │
│             │                                                     │
│             ↓                                                     │
│  2. MEDIA CONTROLLER                                             │
│     ┌───────────────┐                                            │
│     │ MediaLibrary  │ → Loads and decodes media                  │
│     └───────┬───────┘                                            │
│             │                                                     │
│             ↓                                                     │
│  3. VIEWPORT ENGINE                                              │
│     ┌───────────────┐                                            │
│     │ViewportEngine │ → Applies effects, overlays, framing       │
│     └───────┬───────┘                                            │
│             │                                                     │
│             ↓                                                     │
│  4. RENDER PIPELINE                                              │
│     ┌───────────────────────────────────────┐                    │
│     │  RenderPipeline.OnFrameRequested()   │                    │
│     │  • Reads current frame                │                    │
│     │  • Applies final compositing          │                    │
│     │  • Sends to output managers           │                    │
│     └───────┬─────────────────────┬─────────┘                    │
│             │                     │                               │
│             ↓                     ↓                               │
│  5a. LEGACY OUTPUT     5b. NEW OUTPUT MANAGER                    │
│     (Preview Only)      ┌────────────────────┐                   │
│                         │ Outputs.           │                   │
│                         │ OutputManager      │                   │
│                         │ SendFrameAsync()   │                   │
│                         └────────┬───────────┘                   │
│                                  │                                │
│                                  ↓                                │
│  6. UNITY CAPTURE OUTPUT                                         │
│     ┌──────────────────────────────────────┐                     │
│     │  UnityCaptureOutput.cs               │                     │
│     │  • Converts frame to RGBA32          │                     │
│     │  • Writes to named pipe IPC          │                     │
│     │  Pipe: \\.\pipe\                     │                     │
│     │        VirtualCamStudio_Frames       │                     │
│     └──────────────────┬───────────────────┘                     │
│                        │                                          │
└────────────────────────┼──────────────────────────────────────────┘
						 │
						 │ Named Pipe IPC
						 │ (FrameHeader + RGBA32 pixels)
						 │
┌────────────────────────┼──────────────────────────────────────────┐
│                        ↓                                          │
│                 UnityCaptureSender.exe                            │
│                   (Native C++ Process)                            │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  7. IPC FRAME RECEIVER                                           │
│     ┌──────────────────────────────────┐                         │
│     │  FrameIPC.cpp                    │                         │
│     │  • Named pipe server             │                         │
│     │  • Reads FrameHeader (20 bytes)  │                         │
│     │  • Reads pixel data              │                         │
│     │  • Stores in buffer              │                         │
│     └──────────────┬───────────────────┘                         │
│                    │                                              │
│                    ↓                                              │
│  8. UNITY CAPTURE SENDER                                         │
│     ┌──────────────────────────────────┐                         │
│     │  UnityCaptureSender.cpp          │                         │
│     │  • Receives IPC frame OR         │                         │
│     │  • Generates fallback (if no IPC)│                         │
│     │  • Converts RGBA → ARGB          │                         │
│     └──────────────┬───────────────────┘                         │
│                    │                                              │
│                    ↓                                              │
│  9. UNITY CAPTURE PROTOCOL                                       │
│     ┌──────────────────────────────────┐                         │
│     │  SharedImageMemory::Send()       │                         │
│     │  • Writes to shared memory       │                         │
│     │  • Signals frame ready           │                         │
│     │  • UnityCapture.dll reads it     │                         │
│     └──────────────┬───────────────────┘                         │
│                    │                                              │
└────────────────────┼──────────────────────────────────────────────┘
					 │
					 │ Shared Memory
					 │ (Unity Video Capture Driver)
					 │
┌────────────────────┼──────────────────────────────────────────────┐
│                    ↓                                              │
│             Unity Video Capture                                   │
│           Virtual Camera Device                                   │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  • Appears as "UnityVideoCapture" in camera list                 │
│  • Works like a physical webcam                                  │
│  • Any app can read from it                                      │
│                                                                   │
└────────────────────┬─────────────────────────────────────────────┘
					 │
					 ↓
			┌────────────────┐
			│   CloudPhone   │
			│                │
			│  Displays your │
			│  photo/video!  │
			└────────────────┘
```

---

## Frame Data Structure

### IPC Protocol (Pipe Communication)

**FrameHeader (20 bytes):**
```
Offset | Size | Field       | Description
-------+------+-------------+----------------------------------
0      | 4    | Width       | Frame width in pixels (e.g., 1920)
4      | 4    | Height      | Frame height in pixels (e.g., 1080)
8      | 4    | Stride      | Pixels per row (usually = Width)
12     | 4    | DataSize    | Total pixel data size (W×H×4)
16     | 4    | PixelFormat | 0 = RGBA32
```

**Pixel Data (Width × Height × 4 bytes):**
```
[R][G][B][A][R][G][B][A][R][G][B][A]...
 ↑           ↑           ↑
Pixel 0    Pixel 1    Pixel 2  ... (Width × Height pixels)
```

### UnityCapture Protocol (Shared Memory)

UnityCaptureSender converts RGBA → ARGB and sends via `SharedImageMemory::Send()`.
This matches Unity Video Capture's expected format.

---

## Current vs Target State

### CURRENT STATE (What you're seeing now)
```
VirtualCam Studio
  ↓ (NO frames being sent)
UnityCaptureOutput (NOT started)
  ↓
UnityCaptureSender
  ↓ (Generates green diagnostic fallback)
Unity Video Capture
  ↓
CloudPhone: GREEN SCREEN ✅ (Confirms virtual camera works!)
```

### TARGET STATE (After clicking "Start Unity Video Capture")
```
VirtualCam Studio (Your photo/video loaded)
  ↓ (Frames flowing @ 30 FPS)
UnityCaptureOutput (ACTIVE, sending via pipe)
  ↓ (1920×1080 RGBA32 frames)
UnityCaptureSender (Receiving IPC frames, NOT fallback)
  ↓ (IPC frames: 30+, Fallback frames: 0)
Unity Video Capture
  ↓
CloudPhone: YOUR PHOTO/VIDEO! 🎉
```

---

## Key Components

### Studio Side (Managed C# / .NET 8)
| Component | File | Purpose |
|-----------|------|---------|
| RenderPipeline | `Services/RenderPipeline.cs` | Orchestrates frame rendering and output dispatch |
| OutputManager | `Outputs/OutputManager.cs` | Manages all output targets (async) |
| UnityCaptureOutput | `Outputs/UnityCaptureOutput.cs` | IPC client, converts frames to RGBA32, writes to pipe |
| MainWindow | `MainWindow.xaml.cs` | Registers/unregisters Unity capture output on button clicks |

### Sender Side (Native C++)
| Component | File | Purpose |
|-----------|------|---------|
| FrameIPC | `FrameIPC.cpp/h` | Named pipe server, reads FrameHeader + pixel data |
| UnityCaptureSender | `UnityCaptureSender.cpp` | Main loop, receives IPC frames or generates fallback, sends to UnityCapture |
| SharedImageMemory | `UnityCapture/SharedImageMemory.cpp` | Writes to Unity Video Capture's shared memory (UNCHANGED) |

---

## Debug Points

### Where to Add Breakpoints (if needed)

**Studio (C#):**
1. `UnityCaptureOutput.cs` line ~90: `SendFrameAsync()` entry
2. `UnityCaptureOutput.cs` line ~145: After writing to pipe
3. `OutputManager.cs` line ~89: Before calling `SendFrameAsync()`

**Sender (C++):**
1. `FrameIPC.cpp` line in `ReceiveFrame()`: After reading header
2. `UnityCaptureSender.cpp` line in main loop: After calling `FrameIPC::ReceiveFrame()`
3. `SharedImageMemory.cpp` in `Send()`: Before writing to shared memory

### Console Output to Monitor

**Studio (Visual Studio Output):**
```
[Outputs.OutputManager.SendFrameAsync] → Sending to UnityCaptureOutput
[UnityCaptureOutput] ✓ Connected to UnityCaptureSender
[UnityCaptureOutput] FPS Sent: 30 | Failed: 0
```

**Sender (Console Window):**
```
[FrameIPC] ✓ Client connected from VirtualCamStudio
[FrameIPC] Received frame: 1920x1080, 8294400 bytes
Frames sent: 30 | IPC frames: 30 | Fallback frames: 0 | FPS: 30.0
```

---

## Performance Characteristics

| Metric | Expected Value | Notes |
|--------|---------------|-------|
| **Frame Rate** | 30 FPS | Configurable via Camera Profile |
| **Frame Size** | 1920×1080 RGBA32 | 8,294,400 bytes per frame |
| **Bandwidth** | ~237 MB/s | 30 frames × 8.3 MB/frame |
| **Latency** | < 33 ms | One frame time at 30 FPS |
| **IPC Overhead** | < 5 ms | Pipe write + read |

### Typical Frame Timings
```
Studio Render:        10 ms (depends on effects)
IPC Write (Studio):    2 ms
IPC Read (Sender):     2 ms
Format Conversion:     1 ms
Shared Memory Write:  <1 ms
-------------------------
Total Latency:      ~16 ms (well under 33 ms budget)
```

---

## What Changed from OBS Version

### REMOVED:
- ❌ OBS WebSocket connection
- ❌ OBS source/scene setup
- ❌ OBS Virtual Camera controls
- ❌ All OBS UI buttons and status indicators

### KEPT:
- ✅ UnityCapture IPC pipeline (native sender + Studio client)
- ✅ RenderPipeline frame generation
- ✅ Media loading and playback
- ✅ Viewport effects and overlays
- ✅ Preview window

### ADDED:
- ✅ Unity-specific UI controls
- ✅ Unity status indicators
- ✅ Cleaner single-path architecture

---

## Next Steps for Testing

1. **Start UnityCaptureSender.exe** → See blue diagnostic frames (normal)
2. **Launch Studio** → Load your photo/video
3. **Click "Start Unity Video Capture"** → See sender console show "Client connected"
4. **Watch counters:** IPC frames should increase, Fallback frames = 0
5. **Open CloudPhone** → Select UnityVideoCapture
6. **SUCCESS:** Your photo/video appears (not green screen!)

---

## Troubleshooting Quick Reference

| Symptom | Likely Cause | Fix |
|---------|-------------|-----|
| Green screen in CloudPhone | Unity capture not started in Studio | Click "Start Unity Video Capture" |
| Blue frame in CloudPhone | IPC not connected | Restart sender, then Studio |
| "Connection timeout" in Studio | Sender not running | Start UnityCaptureSender.exe first |
| High "Fallback frames" count | IPC pipe broken | Restart both applications |
| Low FPS | Heavy media file or CPU load | Use lower resolution media |
| No camera in CloudPhone | UnityCapture.dll missing | Check sender directory for DLL |

---

**The architecture is ready! All components are in place. Follow QUICK_START_TEST.md to verify the complete flow.**
