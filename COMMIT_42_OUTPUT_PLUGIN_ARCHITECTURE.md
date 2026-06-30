# Commit 42 - Output Plugin Architecture

## Overview
Refactored the output system into a plugin-based architecture where any output target can be registered/unregistered at runtime. PreviewOutput is now just another plugin, alongside future plugins for OBS, recording, screenshots, and streaming.

---

## Architecture

### Core Interface
**`IOutputTarget`** - The plugin interface all outputs must implement

```csharp
public interface IOutputTarget
{
	Task SendFrameAsync(Frame frame);
}
```

### Plugin Manager
**`OutputManager`** - Centralized manager for output plugins
- Maintains a thread-safe collection of registered plugins
- Broadcasts rendered frames to all plugins concurrently
- Supports runtime registration/unregistration
- Does not modify or dispose frames

### Plugin Metadata
**`OutputPluginAttribute`** - Describes plugin capabilities

```csharp
[OutputPlugin("Plugin Name", "Description", "Category", "Version")]
public class MyOutput : IOutputTarget { }
```

### Plugin Discovery
**`OutputPluginRegistry`** - Discovers and manages plugin information
- `DiscoverPlugins()` - Finds all IOutputTarget implementations
- `GetPluginInfo<T>()` - Gets metadata for a specific plugin type
- `GetPluginsByCategory(category)` - Filters plugins by category

---

## Implemented Plugins

### 1. PreviewOutput (Active)
**Category**: Display  
**Status**: ✅ Fully implemented  
**Description**: Displays rendered frames in the WPF preview window

**Features**:
- Converts Mat to BitmapSource with freezing for thread safety
- Marshals UI updates to WPF dispatcher thread
- Real-time preview of rendered output

**Usage**:
```csharp
var preview = new PreviewOutput(PreviewImage);
outputManager.Register(preview);
```

---

### 2. OBSOutput (Placeholder)
**Category**: Streaming  
**Status**: 🔨 Future implementation (Commit 43+)  
**Description**: Sends rendered frames to OBS Studio via WebSocket

**Planned Features**:
- Convert Frame to base64 image data
- Send to OBS via WebSocket `SetSourceSettings`
- Update configured Image Source in OBS scene
- Support multiple OBS instances

**Future Usage**:
```csharp
var obs = new OBSOutput();
outputManager.Register(obs);
```

---

### 3. RecordingOutput (Placeholder)
**Category**: Recording  
**Status**: 🔨 Future implementation (Commit 44+)  
**Description**: Records rendered frames to a video file

**Planned Features**:
- Start/stop recording on demand
- Support multiple codecs (H.264, MJPEG, etc.)
- Configurable FPS, resolution, and bitrate
- Progress tracking and file size estimation

**Future API**:
```csharp
var recorder = new RecordingOutput();
recorder.StartRecording("output.mp4", fps: 30, width: 1920, height: 1080);
outputManager.Register(recorder);

// Later...
recorder.StopRecording();
```

---

### 4. ScreenshotOutput (Placeholder)
**Category**: Recording  
**Status**: 🔨 Future implementation (Commit 45+)  
**Description**: Saves rendered frames as image files (screenshots)

**Planned Features**:
- Capture single frames on demand
- Configurable output directory
- Automatic timestamp-based filenames
- Support multiple image formats (PNG, JPG)

**Future API**:
```csharp
var screenshot = new ScreenshotOutput(@"C:\Screenshots");
outputManager.Register(screenshot);

// Capture next frame
screenshot.CaptureNextFrame();
```

---

### 5. StreamOutput (Placeholder)
**Category**: Streaming  
**Status**: 🔨 Future implementation (Commit 46+)  
**Description**: Streams rendered frames to network destinations (RTMP, SRT, etc.)

**Planned Features**:
- Start/stop streaming on demand
- Support RTMP, SRT, and other protocols
- Configurable bitrate and quality
- Connection status monitoring
- Automatic reconnection on failure

**Future API**:
```csharp
var stream = new StreamOutput();
stream.StartStreaming("rtmp://server/stream", fps: 30, width: 1920, height: 1080, bitrate: 5000);
outputManager.Register(stream);

// Later...
stream.StopStreaming();
```

---

## OutputManager API

### Registration
```csharp
// Register a plugin
outputManager.Register(plugin);

// Unregister a plugin
outputManager.Unregister(plugin);
```

### Broadcasting
```csharp
// Send frame to all registered plugins
await outputManager.SendFrameAsync(frame);
```

### Query
```csharp
// Get count of registered plugins
int count = outputManager.OutputCount;

// Get all registered plugins
IOutputTarget[] plugins = outputManager.GetRegisteredOutputs();

// Check if specific plugin is registered
bool isRegistered = outputManager.IsRegistered(plugin);

// Check if any plugin of type T is registered
bool hasPreview = outputManager.IsRegistered<PreviewOutput>();

// Get all plugins of type T
PreviewOutput[] previews = outputManager.GetOutputs<PreviewOutput>();
```

---

## Plugin Discovery

### Discover All Plugins
```csharp
var plugins = OutputPluginRegistry.DiscoverPlugins();

foreach (var plugin in plugins)
{
	Console.WriteLine($"{plugin.Name} ({plugin.Category})");
	Console.WriteLine($"  {plugin.Description}");
	Console.WriteLine($"  Version: {plugin.Version}");
}
```

**Output**:
```
Preview (Display)
  Displays rendered frames in the application preview window
  Version: 1.0.0

Video Recording (Recording)
  Records rendered frames to a video file (MP4, AVI, etc.)
  Version: 1.0.0

Screenshot (Recording)
  Saves rendered frames as image files (PNG, JPG)
  Version: 1.0.0

OBS Studio (Streaming)
  Sends rendered frames to OBS Studio via WebSocket
  Version: 1.0.0

Network Stream (Streaming)
  Streams rendered frames to RTMP, SRT, or other network destinations
  Version: 1.0.0
```

### Get Plugin Info
```csharp
var info = OutputPluginRegistry.GetPluginInfo<PreviewOutput>();
Console.WriteLine($"{info.Name}: {info.Description}");
```

### Get Plugins by Category
```csharp
var streamingPlugins = OutputPluginRegistry.GetPluginsByCategory("Streaming");
// Returns: OBSOutput, StreamOutput
```

---

## Thread Safety

### OutputManager
- Uses `lock` for collection modifications
- Snapshots the collection before broadcasting
- Safe for concurrent registration/unregistration

### Plugin Execution
- All plugins receive frames **concurrently**
- Each plugin runs in its own task
- Errors in one plugin don't affect others
- OutputManager logs errors but continues broadcasting

---

## Data Flow

```
RenderPipeline
	↓
ViewportEngine.Render()
	↓
Frame (rendered)
	↓
OutputManager.SendFrameAsync()
	├─→ PreviewOutput (WPF UI)
	├─→ OBSOutput (future)
	├─→ RecordingOutput (future)
	├─→ ScreenshotOutput (future)
	└─→ StreamOutput (future)
```

---

## Frame Lifecycle

### Rules
1. **OutputManager does NOT dispose frames**
2. **Plugins do NOT dispose frames**
3. **RenderPipeline owns frame disposal**
4. **Plugins can read but not modify frames**

### Pattern
```csharp
// RenderPipeline.cs
Frame renderedFrame = ViewportEngine.Render(...);

try
{
	// Broadcast to plugins (does not dispose)
	await OutputManager.SendFrameAsync(renderedFrame);
}
finally
{
	// RenderPipeline disposes the frame
	renderedFrame.Dispose();
}
```

---

## Adding a New Plugin

### Step 1: Create Plugin Class
```csharp
using VirtualCamStudio.Core;

namespace VirtualCamStudio.Outputs
{
	[OutputPlugin("My Plugin", "Does something cool", "Custom", "1.0.0")]
	public class MyPlugin : IOutputTarget
	{
		public Task SendFrameAsync(Frame frame)
		{
			// Process the frame
			return Task.CompletedTask;
		}
	}
}
```

### Step 2: Register at Runtime
```csharp
var plugin = new MyPlugin();
outputManager.Register(plugin);
```

### Step 3: Unregister When Done
```csharp
outputManager.Unregister(plugin);
```

---

## Plugin Categories

| **Category** | **Purpose** | **Examples** |
|--------------|-------------|--------------|
| **Display** | Real-time visual output | PreviewOutput |
| **Recording** | File-based capture | RecordingOutput, ScreenshotOutput |
| **Streaming** | Network transmission | OBSOutput, StreamOutput |
| **General** | Uncategorized | Custom plugins |

---

## Benefits

### ✅ Extensibility
- Add new outputs without modifying core rendering
- Plugins are self-contained and independent

### ✅ Runtime Configuration
- Register/unregister plugins dynamically
- No restart required

### ✅ Isolation
- Each plugin runs independently
- Errors don't cascade to other plugins

### ✅ Consistency
- Single frame source (ViewportEngine)
- All plugins receive identical frames
- No duplicate rendering

### ✅ Testability
- Plugins can be tested in isolation
- Mock plugins for testing pipeline

---

## Preserved Behavior

### ✅ Preview Works Identically
- PreviewOutput is a standard plugin
- Same WPF marshaling logic
- Same BitmapSource freezing
- No visual changes

### ✅ Rendering Unchanged
- ViewportEngine not modified
- RenderPipeline not modified (except comments)
- Same frame lifecycle

### ✅ Zero Breaking Changes
- Existing preview registration works
- Legacy output system still available
- Gradual migration path

---

## File Structure

```
VirtualCamStudio/
└── Outputs/
	├── IOutputTarget.cs              (Plugin interface)
	├── OutputManager.cs              (Plugin manager)
	├── OutputPluginAttribute.cs      (Plugin metadata)
	├── OutputPluginRegistry.cs       (Plugin discovery)
	├── PreviewOutput.cs              (Display plugin - Active)
	├── OBSOutput.cs                  (Streaming plugin - Future)
	├── RecordingOutput.cs            (Recording plugin - Future)
	├── ScreenshotOutput.cs           (Recording plugin - Future)
	└── StreamOutput.cs               (Streaming plugin - Future)
```

---

## Build Status
✅ **Zero errors** - All code compiles successfully

---

## Testing Checklist

### ✅ Plugin Discovery
- [ ] `DiscoverPlugins()` finds all 5 plugins
- [ ] Each plugin has correct metadata
- [ ] Plugins sorted by category then name

### ✅ Registration
- [ ] Can register PreviewOutput
- [ ] Can register multiple plugins
- [ ] Can unregister plugins
- [ ] `OutputCount` is accurate

### ✅ Broadcasting
- [ ] Frame reaches all registered plugins
- [ ] Plugins execute concurrently
- [ ] Errors in one plugin don't affect others

### ✅ Preview
- [ ] Preview displays correctly
- [ ] No visual regression
- [ ] Thread-safe UI updates

### ✅ Query API
- [ ] `GetRegisteredOutputs()` returns all plugins
- [ ] `IsRegistered()` works correctly
- [ ] `IsRegistered<T>()` works correctly
- [ ] `GetOutputs<T>()` filters by type

---

## Future Work

### Commit 43: OBS Output Implementation
- Implement WebSocket frame transmission
- Base64 image encoding
- OBS source configuration
- Connection management

### Commit 44: Recording Output Implementation
- VideoWriter integration
- Codec selection
- File management
- Progress tracking

### Commit 45: Screenshot Output Implementation
- Single-frame capture
- Format selection (PNG/JPG)
- File naming
- Directory management

### Commit 46: Stream Output Implementation
- FFmpeg pipeline integration
- RTMP/SRT support
- Bitrate control
- Connection monitoring

---

## Summary

Commit 42 establishes a **clean plugin architecture** for output targets:

1. ✅ **IOutputTarget** interface preserved
2. ✅ **OutputManager** manages plugin collection
3. ✅ **Runtime registration/unregistration** supported
4. ✅ **Frame broadcasting** to all plugins
5. ✅ **PreviewOutput** is a standard plugin
6. ✅ **Future plugins** scaffolded (OBS, Recording, Screenshot, Stream)
7. ✅ **No renderer changes**
8. ✅ **No viewport changes**
9. ✅ **Existing preview behavior preserved**
10. ✅ **Zero build errors**

The architecture is **ready for future outputs** without any breaking changes.
