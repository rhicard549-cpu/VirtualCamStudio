# VCamNetSample Virtual Camera Research Notes

## Executive Summary

VCamNetSample by Simon Mourier is a proven .NET implementation of a Windows 11 virtual camera using Media Foundation. This document details the architecture, identifies the exact frame generation point we will replace with VirtualCamStudio's OpenCV-rendered frames, and provides implementation guidance.

---

## Repository Information

- **Project**: VCamNetSample
- **Author**: Simon Mourier ([@smourier](https://github.com/smourier))
- **Repository**: https://github.com/smourier/VCamNetSample
- **License**: MIT
- **Local Clone**: `D:\Projects\VirtualCamStudio\Research\VCamNetSample`
- **Build Status**: ✅ Built successfully (0 errors)
- **Copied Location**: `C:\VCamNetSample` (publicly accessible for Frame Server access)

---

## Solution Structure

The solution contains 5 projects:

### 1. **VCamNetSample** (Registration Application)
- **Type**: Windows Forms Application (.NET 10 / Windows 11)
- **Purpose**: GUI application to register/unregister the virtual camera
- **Output**: `VCamNetSample.exe`
- **Key File**: `Main.cs`

### 2. **VCamNetSampleSource** (COM Media Source) ⭐
- **Type**: COM-visible Library (.NET 10 / Windows 11)
- **Purpose**: The actual virtual camera COM Media Source implementation
- **Output**: `VCamNetSampleSource.comhost.dll` + `VCamNetSampleSource.dll`
- **CLSID**: `{b624d1c1-4f7d-4646-aac0-1a51cd4cedd7}`
- **Key Files**:
  - `Activator.cs` - COM entry point
  - `MediaSource.cs` - Media Foundation source
  - `MediaStream.cs` - Stream implementation
  - `FrameGenerator.cs` - **THE FRAME GENERATION CLASS** ⭐⭐⭐

### 3. **VCamNetSampleAOT** (AOT Registration Application)
- **Type**: AOT-compiled registration app
- **Purpose**: Alternative registration using Native AOT compilation

### 4. **VCamNetSampleSourceAOT** (AOT COM Media Source)
- **Type**: AOT-compiled COM source
- **Purpose**: Native AOT version of the media source

### 5. **DebuggerAttach**
- **Type**: Utility console app
- **Purpose**: Attach debugger to Windows Frame Server (svchost.exe) for diagnostics

---

## Registration Mechanism

### How the Virtual Camera is Registered

**File**: `VCamNetSample/Main.cs` → `OnShown()` method

```csharp
// Initialize Media Foundation
MFFunctions.MFStartup();

// Create virtual camera instance
var hr = Functions.MFCreateVirtualCamera(
	__MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0001.MFVirtualCameraType_SoftwareCameraSource,
	__MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0002.MFVirtualCameraLifetime_Session,
	__MIDL___MIDL_itf_mfvirtualcamera_0000_0000_0003.MFVirtualCameraAccess_CurrentUser,
	Text,  // Camera friendly name: "VCam .NET Sample"
	"{" + Shared.CLSID_VCamNet + "}",  // CLSID: {b624d1c1-4f7d-4646-aac0-1a51cd4cedd7}
	null,
	0,
	out var camera);

if (hr.IsSuccess)
{
	_camera = new ComObject<IMFVirtualCamera>(camera);
	hr = _camera.Object.Start(null);  // Start the camera
}
```

### Key Registration Facts

1. **Camera Type**: Software Camera Source (not hardware)
2. **Lifetime**: Session (exists while registered, removed on unregister)
3. **Access**: Current User only
4. **Display Name**: "VCam .NET Sample"
5. **COM CLSID**: Defined in `VCamNetSampleSource/Shared.cs`:
   ```csharp
   public const string CLSID_VCamNet = "b624d1c1-4f7d-4646-aac0-1a51cd4cedd7";
   ```

### Registration Flow

1. User runs `VCamNetSample.exe` as administrator
2. App calls `MFCreateVirtualCamera()` with CLSID pointing to COM DLL
3. Windows Frame Server loads `VCamNetSampleSource.comhost.dll`
4. Camera appears in Windows Camera app as "VCam .NET Sample"
5. Dialog stays open - closing it calls `camera.Remove()` and unregisters

---

## COM Media Source Implementation

### Architecture Overview

**VCamNetSampleSource.dll** implements the COM Media Source that Windows Frame Server loads in-process.

### Key Classes

#### 1. **Activator.cs** - COM Entry Point

```csharp
[ComVisible(true), Guid(Shared.CLSID_VCamNet)]
public class Activator : MFAttributes, IMFActivateImpl
```

- **Purpose**: COM class factory - Windows instantiates this via CLSID
- **Implements**: `IMFActivate` interface
- **Key Method**: `ActivateObject()` - Creates and returns `MediaSource` instances

```csharp
public HRESULT ActivateObject(Guid riid, out nint ppv)
{
	if (riid == typeof(IMFMediaSourceEx).GUID || riid == typeof(IMFMediaSource).GUID)
	{
		var source = new MediaSource();
		SetDefaultAttributes(source);
		ppv = ComObject.QueryObjectInterface(source, riid, false);
		return HRESULTS.S_OK;
	}
	// ...
}
```

#### 2. **MediaSource.cs** - Media Foundation Source

- **Implements**: `IMFMediaSourceEx`, `IMFMediaSource`
- **Purpose**: Represents the camera device to Media Foundation
- **Responsibilities**:
  - Create presentation descriptors
  - Manage `MediaStream` instances (one per stream - typically just one for video)
  - Handle Media Foundation event queue
  - Respond to Start/Stop/Pause commands

#### 3. **MediaStream.cs** - Stream Implementation

- **Implements**: `IMFMediaStream`
- **Purpose**: Represents a single video stream from the camera
- **Responsibilities**:
  - Create `IMFSample` objects (video frames)
  - Call `FrameGenerator.Generate()` to fill samples with content
  - Handle sample requests from Media Foundation
  - Support both RGB32 and NV12 pixel formats
  - Manage GPU (Direct3D) and CPU rendering paths

---

## Frame Generation - THE CRITICAL CODE ⭐⭐⭐

### File: `VCamNetSampleSource/FrameGenerator.cs`

This is the **EXACT** class we will replace with VirtualCamStudio's OpenCV-rendered frames.

### The Generate Method - Line 158

```csharp
public IComObject<IMFSample> Generate(IComObject<IMFSample> sample, Guid format)
```

**This is THE method where frames are created.**

### What It Does

1. **Clears the frame**: `_renderTarget.Clear(new _D3DCOLORVALUE(1, 0, 0, 1));` (red background)

2. **Draws HSL color blocks** (the animated squares):
```csharp
for (uint i = 0; i < _width / DIVISOR; i++)
{
	for (uint j = 0; j < _height / DIVISOR; j++)
	{
		var brush = _blockBrushes[k++];
		_renderTarget.FillRectangle(new D2D_RECT_F(i * DIVISOR, j * DIVISOR, (i + 1) * DIVISOR, (j + 1) * DIVISOR), brush);
	}
}
```

3. **Draws white circles at corners**:
```csharp
_renderTarget.DrawEllipse(new D2D1_ELLIPSE(new D2D_POINT_2F(radius + padding, radius + padding), radius, radius), _whiteBrush);
// ... 3 more circles
```

4. **Draws white border**:
```csharp
_renderTarget.DrawRectangle(new D2D_RECT_F(radius, radius, _width - radius, _height - radius), _whiteBrush);
```

5. **Draws text overlay** (frame number, FPS, resolution):
```csharp
var text = $"Format: {fmt}\n.NET Frame#: {_frameCount}\nFps: {_fps}\nResolution: {_width} x {_height}";
using var layout = _dwrite.CreateTextLayout(_textFormat, text, text.Length, _width, _height);
_renderTarget.DrawTextLayout(new D2D_POINT_2F(0, 0), layout, _whiteBrush);
```

6. **Ends drawing**: `_renderTarget.EndDraw();`

7. **Handles format conversion**:
   - If GPU (Direct3D): Converts texture to `IMFSample`
   - If CPU: Uses WIC bitmap, copies pixels
   - Converts RGB32 to NV12 if needed (most apps prefer NV12)

### Rendering Technology

- **Direct2D** (`ID2D1RenderTarget`) for all drawing operations
- **DirectWrite** (`IDWriteFactory`) for text rendering
- **WIC** (Windows Imaging Component) for CPU-based bitmap rendering
- **Direct3D 11** (`ID3D11Texture2D`) for GPU-based rendering

### GPU vs CPU Paths

**GPU Path (when `HasD3DManager == true`):**
- Renders to `ID3D11Texture2D`
- Fast hardware acceleration
- Used by Windows 11 Camera app

**CPU Path (when `HasD3DManager == false`):**
- Renders to `IWICBitmap`
- Software rendering
- Used by Chrome, Edge, Teams (they don't provide D3D manager)

---

## Integration Strategy for VirtualCamStudio

### What We Will Do

1. **Keep VCamNetSample architecture intact** - It's proven and works perfectly
2. **Replace only the frame content** in `FrameGenerator.Generate()`
3. **Use our OpenCV Mat** from VirtualCamStudio's renderer

### The Replacement Point

**File**: `VCamNetSampleSource/FrameGenerator.cs`  
**Method**: `Generate(IComObject<IMFSample> sample, Guid format)`  
**Lines to Replace**: 168-210 (all the Direct2D drawing code)

### Replacement Strategy

**Current Code** (lines 168-210):
```csharp
_renderTarget.BeginDraw();
_renderTarget.Clear(...);
// Draw HSL blocks, circles, rectangles, text...
_renderTarget.EndDraw();
```

**New Code** (our implementation):
```csharp
_renderTarget.BeginDraw();

// Get frame from VirtualCamStudio's renderer
byte[] frameData = VirtualCamStudioBridge.GetCurrentFrame();
// frameData is OpenCV Mat converted to BGRA32 byte array

// Copy frameData to Direct2D render target
// (We'll need to create an ID2D1Bitmap from byte array and draw it)

_renderTarget.EndDraw();
```

### Data Flow

```
VirtualCamStudio (WPF)
	↓
OpenCV Rendering Pipeline (OBS overlay + effects)
	↓
Mat (BGRA or RGB frame)
	↓
Convert to byte[]
	↓
Shared Memory or Named Pipe or gRPC
	↓
VCamNetSampleSource (COM DLL)
	↓
FrameGenerator.Generate()
	↓
Copy to Direct2D render target
	↓
Convert to IMFSample
	↓
Media Foundation
	↓
Windows Camera App / Teams / Zoom / etc.
```

---

## Critical Implementation Details

### 1. Process Boundary

The COM DLL runs in **separate processes**:
- Windows Frame Server (svchost.exe)
- Camera Monitor (svchost.exe)
- The consuming app (Camera.exe, Teams.exe, etc.)

VirtualCamStudio runs in **its own process**.

**Solution**: Use Inter-Process Communication (IPC):
- Named Pipes (fast, efficient)
- Shared Memory (fastest, but more complex)
- gRPC (robust, but overhead)
- Memory-Mapped Files (good balance)

### 2. Frame Format Considerations

- **OpenCV Mat**: Typically BGR or BGRA (8-bit per channel)
- **Direct2D**: Uses BGRA32 or RGB32
- **Media Foundation**: Prefers NV12 (YUV 4:2:0)

**Conversion Path**:
1. OpenCV Mat (BGR/BGRA) → byte array
2. byte array → Direct2D bitmap (BGRA32/RGB32)
3. Direct2D → IMFSample (RGB32)
4. RGB32 → NV12 (via GPU Video Processor MFT or CPU Color Converter)

### 3. Performance Requirements

- **Target FPS**: 30-60 FPS
- **Frame Time**: 16-33ms per frame
- **Resolution**: 1920x1080 or 1280x720
- **IPC Overhead**: Must be < 5ms

### 4. Synchronization

- Use double-buffering in shared memory
- Lock-free or minimal locking mechanisms
- Handle frame drops gracefully

---

## Testing and Verification

### How to Test

1. **Register the camera**:
   - Run `C:\VCamNetSample\VCamNetSample.exe` as admin
   - Click "Register"

2. **Verify in Windows Camera**:
   - Open Windows Camera app
   - Select "VCam .NET Sample"
   - Should see animated colored blocks and text

3. **Test in other apps**:
   - Microsoft Teams
   - Zoom
   - Discord
   - Google Meet (Chrome)

### Debugging Tools

**TraceSpy** (ETW Viewer):
- Download: https://github.com/smourier/TraceSpy
- ETW Provider GUID: `964d4572-adb9-4f3a-8170-fcbecec27467`
- Use to see detailed traces from all processes loading the COM DLL

**DebuggerAttach**:
- Run as admin from VCamNetSample solution
- Attaches debugger to Frame Server process

---

## Build Configuration

### Important Build Notes

1. **Public Location Required**:
   - COM DLL must be in a location accessible by Local Service and Local System accounts
   - ❌ `C:\Users\<username>\...` - Access Denied
   - ✅ `C:\VCamNetSample\` - Everyone can access

2. **Platform Target**:
   - Built for ARM64 in our case (or x64 for Intel)
   - Must match system architecture

3. **Dependencies**:
   - `DirectNCore.dll` - DirectN wrapper library
   - `Microsoft.Windows.SDK.NET.dll` - Windows SDK projections
   - `WinRT.Runtime.dll` - WinRT runtime support

---

## Next Steps for Integration

### Phase 1: IPC Bridge (Immediate)
1. Create `VirtualCamStudioBridge` class in VCamNetSampleSource
2. Implement Named Pipe client to receive frames from VirtualCamStudio
3. Test frame transfer without rendering

### Phase 2: Frame Injection (Next)
1. Modify `FrameGenerator.Generate()` to receive byte array from bridge
2. Create Direct2D bitmap from byte array
3. Draw bitmap to render target
4. Test in Windows Camera

### Phase 3: VirtualCamStudio Integration (Final)
1. Add Named Pipe server to VirtualCamStudio
2. Capture OpenCV Mat from renderer
3. Convert Mat to BGRA byte array
4. Send via Named Pipe to COM DLL
5. End-to-end testing

### Phase 4: Optimization
1. Implement double-buffering
2. Add frame rate control
3. Handle resolution changes dynamically
4. Add error recovery

---

## File Locations Reference

### VCamNetSample Source
```
D:\Projects\VirtualCamStudio\Research\VCamNetSample\
├── VCamNetSample\              (Registration app)
│   └── Main.cs                 (Registration logic)
├── VCamNetSampleSource\        (COM Media Source) ⭐
│   ├── Activator.cs           (COM entry point)
│   ├── MediaSource.cs         (MF source)
│   ├── MediaStream.cs         (MF stream)
│   ├── FrameGenerator.cs      (FRAME GENERATION - Line 158) ⭐⭐⭐
│   └── Shared.cs              (CLSID definition)
└── VCamNetSample.sln
```

### Deployed Location
```
C:\VCamNetSample\
├── VCamNetSample.exe                (Registration app)
├── VCamNetSampleSource.comhost.dll  (COM server)
├── VCamNetSampleSource.dll          (Managed code)
└── [dependencies]
```

---

## Summary

### Which project registers the virtual camera?
**VCamNetSample** (VCamNetSample.exe) - The Windows Forms GUI application

### Which project implements the COM Media Source?
**VCamNetSampleSource** (VCamNetSampleSource.comhost.dll + VCamNetSampleSource.dll)

### Which class generates video frames?
**FrameGenerator** (VCamNetSampleSource/FrameGenerator.cs)

### Where does the sample create each frame?
**FrameGenerator.Generate() method at line 158**

### The exact method we will replace?
**FrameGenerator.Generate(IComObject<IMFSample> sample, Guid format)**

Specifically, replace the Direct2D drawing code (lines 168-210) with:
1. Receive frame data from VirtualCamStudio via IPC
2. Copy frame data to Direct2D render target
3. Let the existing code handle format conversion and sample creation

---

## Conclusion

VCamNetSample provides a **proven, working foundation** for our virtual camera. We do NOT need to understand every detail of Media Foundation COM interfaces - Simon Mourier has already implemented all the complex parts correctly.

**Our task is simple and focused**:
1. Add IPC bridge to receive frames from VirtualCamStudio
2. Replace the drawing code in `FrameGenerator.Generate()`
3. Test and optimize

This approach minimizes risk and development time while ensuring compatibility with Windows 11 and all video conferencing applications.

---

**Document Version**: 1.0  
**Date**: 2026-06-29  
**Status**: Research Complete ✅  
**Next Action**: Begin Phase 1 - IPC Bridge Implementation
