# Output Plugin Architecture - Visual Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       VirtualCamStudio                          │
└─────────────────────────────────────────────────────────────────┘
								│
								│ Media Input
								↓
┌─────────────────────────────────────────────────────────────────┐
│                      Media Layer                                │
│  • MediaController (manages current frame)                      │
│  • VideoPlayer (decodes video)                                  │
│  • PlaybackEngine (video playback state)                        │
└────────────────────┬────────────────────────────────────────────┘
					 │
					 │ Current Frame
					 ↓
┌─────────────────────────────────────────────────────────────────┐
│                    Rendering Layer                              │
│  • RenderLoop (schedules frames)                                │
│  • ViewportEngine (renders with zoom/pan/rotation)              │
│  • RenderPipeline (orchestrates flow)                           │
└────────────────────┬────────────────────────────────────────────┘
					 │
					 │ Rendered Frame
					 ↓
┌─────────────────────────────────────────────────────────────────┐
│                   OutputManager                                 │
│  (Plugin-Based Frame Broadcasting)                              │
│                                                                 │
│  • Thread-safe plugin collection                                │
│  • Concurrent frame distribution                                │
│  • Error isolation per plugin                                   │
└────┬────────────┬──────────────┬──────────────┬─────────────────┘
	 │            │              │              │
	 │            │              │              │
	 ↓            ↓              ↓              ↓
┌─────────┐ ┌─────────┐ ┌──────────────┐ ┌──────────┐
│ Preview │ │   OBS   │ │  Recording   │ │Screenshot│
│ Output  │ │ Output  │ │   Output     │ │  Output  │
│   ✅    │ │   🔨    │ │     🔨       │ │    🔨    │
└─────────┘ └─────────┘ └──────────────┘ └──────────┘
	 │            │              │              │
	 ↓            ↓              ↓              ↓
┌─────────┐ ┌─────────┐ ┌──────────────┐ ┌──────────┐
│   WPF   │ │   OBS   │ │  Video File  │ │Image File│
│Preview  │ │ Studio  │ │  (MP4/AVI)   │ │(PNG/JPG) │
└─────────┘ └─────────┘ └──────────────┘ └──────────┘
```

## Plugin Lifecycle

```
┌──────────────────────────────────────────────────────────────┐
│ 1. Application Startup                                       │
└──────────────────┬───────────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────────┐
│ 2. Create OutputManager                                      │
│    var outputManager = new OutputManager();                  │
└──────────────────┬───────────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────────┐
│ 3. Create & Register Plugins                                 │
│    var preview = new PreviewOutput(PreviewImage);            │
│    outputManager.Register(preview);                          │
└──────────────────┬───────────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────────┐
│ 4. RenderLoop Fires FrameRequested                           │
└──────────────────┬───────────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────────┐
│ 5. RenderPipeline Gets Frame & Renders                       │
│    Frame frame = ViewportEngine.Render(...);                 │
└──────────────────┬───────────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────────┐
│ 6. Broadcast to All Plugins                                  │
│    await outputManager.SendFrameAsync(frame);                │
└──────┬───────────┬──────────────┬──────────────┬─────────────┘
	   │           │              │              │
	   ↓           ↓              ↓              ↓
   Plugin 1    Plugin 2       Plugin 3      Plugin 4
   (Async)     (Async)        (Async)       (Async)
	   │           │              │              │
	   └───────────┴──────────────┴──────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────────┐
│ 7. All Plugins Complete                                      │
└──────────────────┬───────────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────────┐
│ 8. RenderPipeline Disposes Frame                             │
│    frame.Dispose();                                          │
└──────────────────────────────────────────────────────────────┘
```

## Plugin Registration Flow

```
┌─────────────────┐
│  Create Plugin  │
│  Instance       │
└────────┬────────┘
		 │
		 ↓
┌─────────────────────────────────────────────────────────┐
│  outputManager.Register(plugin)                         │
├─────────────────────────────────────────────────────────┤
│  1. Validate plugin != null                             │
│  2. lock (_lock) { _outputs.Add(plugin); }              │
│  3. Log registration                                    │
└────────┬────────────────────────────────────────────────┘
		 │
		 ↓
┌─────────────────┐
│  Plugin Active  │
│  (Receives      │
│   Frames)       │
└─────────────────┘
```

## Frame Broadcasting Flow

```
┌────────────────────────────────────────────────────────────────┐
│  await outputManager.SendFrameAsync(frame)                     │
├────────────────────────────────────────────────────────────────┤
│  1. Validate frame != null && frame.IsValid                    │
│  2. Snapshot registered plugins (thread-safe)                  │
│  3. Create Task for each plugin (concurrent)                   │
│  4. await Task.WhenAll(tasks)                                  │
└────┬───────────┬───────────────┬───────────────┬───────────────┘
	 │           │               │               │
	 ↓           ↓               ↓               ↓
┌─────────┐ ┌─────────┐   ┌──────────┐   ┌──────────┐
│Plugin 1 │ │Plugin 2 │   │ Plugin 3 │   │ Plugin 4 │
│  Task   │ │  Task   │   │   Task   │   │   Task   │
└────┬────┘ └────┬────┘   └─────┬────┘   └─────┬────┘
	 │           │              │              │
	 │ Success   │ Success      │ Error        │ Success
	 │           │              │ (logged,     │
	 │           │              │  isolated)   │
	 └───────────┴──────────────┴──────────────┘
					  │
					  ↓
			All tasks complete
```

## Plugin Metadata System

```
┌──────────────────────────────────────────────────────────┐
│  [OutputPlugin("Name", "Desc", "Category", "Version")]   │
│  public class MyPlugin : IOutputTarget                   │
└──────────────────┬───────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────┐
│  OutputPluginRegistry.DiscoverPlugins()                  │
├──────────────────────────────────────────────────────────┤
│  1. Scan assembly for IOutputTarget implementations      │
│  2. Read [OutputPlugin] attributes                       │
│  3. Create OutputPluginInfo objects                      │
│  4. Sort by Category → Name                              │
└──────────────────┬───────────────────────────────────────┘
				   │
				   ↓
┌──────────────────────────────────────────────────────────┐
│  List<OutputPluginInfo>                                  │
│  • PluginType (Type)                                     │
│  • Name (string)                                         │
│  • Description (string)                                  │
│  • Category (string)                                     │
│  • Version (string)                                      │
│  • CreateInstance(args) → IOutputTarget                  │
└──────────────────────────────────────────────────────────┘
```

## Plugin Categories

```
┌─────────────────────────────────────────────────────────────┐
│                    Plugin Categories                        │
└─────────────────────────────────────────────────────────────┘

┌────────────┐     ┌────────────┐     ┌────────────┐
│  Display   │     │ Recording  │     │ Streaming  │
├────────────┤     ├────────────┤     ├────────────┤
│ • Preview  │     │ • Record   │     │ • OBS      │
│   Output   │     │ • Screen   │     │ • Stream   │
│   (WPF)    │     │   shot     │     │   (RTMP)   │
└────────────┘     └────────────┘     └────────────┘
```

## Error Isolation

```
┌──────────────────────────────────────────────────────────┐
│  Frame Broadcasting                                      │
└──────────────────────────────────────────────────────────┘

Plugin 1 ──→ ✅ Success ─────────┐
								 │
Plugin 2 ──→ ❌ Exception ────→  │  Continue
			  (logged)           │  Broadcasting
								 │
Plugin 3 ──→ ✅ Success ─────────┤
								 │
Plugin 4 ──→ ✅ Success ─────────┘

Result: 3 plugins succeed, 1 fails (isolated)
```

## Thread Safety Model

```
┌────────────────────────────────────────────────────────┐
│  OutputManager (Thread-Safe Collection)                │
├────────────────────────────────────────────────────────┤
│                                                        │
│  private readonly object _lock = new();                │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ Register/Unregister                            │   │
│  │ lock (_lock) { ... }                           │   │
│  └────────────────────────────────────────────────┘   │
│                                                        │
│  ┌────────────────────────────────────────────────┐   │
│  │ SendFrameAsync                                 │   │
│  │ lock (_lock) { snapshot = _outputs.ToArray(); }│   │
│  │ (Release lock)                                 │   │
│  │ await Task.WhenAll(snapshot.Select(...))       │   │
│  └────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────┘

Benefits:
✅ No lock held during async operations
✅ Plugins run concurrently
✅ Registration safe during broadcast
```

## Future Plugin Integration

```
Current State (Commit 42):
┌──────────────────────────────────────────────────────────┐
│  OutputManager                                           │
│    └─→ PreviewOutput (✅ Active)                         │
└──────────────────────────────────────────────────────────┘

After Commit 43:
┌──────────────────────────────────────────────────────────┐
│  OutputManager                                           │
│    ├─→ PreviewOutput (✅ Active)                         │
│    └─→ OBSOutput (✅ Active)                             │
└──────────────────────────────────────────────────────────┘

After Commit 44:
┌──────────────────────────────────────────────────────────┐
│  OutputManager                                           │
│    ├─→ PreviewOutput (✅ Active)                         │
│    ├─→ OBSOutput (✅ Active)                             │
│    └─→ RecordingOutput (✅ Active)                       │
└──────────────────────────────────────────────────────────┘

After Commit 45:
┌──────────────────────────────────────────────────────────┐
│  OutputManager                                           │
│    ├─→ PreviewOutput (✅ Active)                         │
│    ├─→ OBSOutput (✅ Active)                             │
│    ├─→ RecordingOutput (✅ Active)                       │
│    └─→ ScreenshotOutput (✅ Active)                      │
└──────────────────────────────────────────────────────────┘

After Commit 46:
┌──────────────────────────────────────────────────────────┐
│  OutputManager                                           │
│    ├─→ PreviewOutput (✅ Active)                         │
│    ├─→ OBSOutput (✅ Active)                             │
│    ├─→ RecordingOutput (✅ Active)                       │
│    ├─→ ScreenshotOutput (✅ Active)                      │
│    └─→ StreamOutput (✅ Active)                          │
└──────────────────────────────────────────────────────────┘
```

## Plugin API Summary

```
IOutputTarget (Interface)
├── Task SendFrameAsync(Frame frame)
│
OutputPluginAttribute (Metadata)
├── string Name
├── string Description
├── string Category
└── string Version
│
OutputManager (Centralized Manager)
├── void Register(IOutputTarget)
├── void Unregister(IOutputTarget)
├── Task SendFrameAsync(Frame)
├── int OutputCount { get; }
├── IOutputTarget[] GetRegisteredOutputs()
├── bool IsRegistered(IOutputTarget)
├── bool IsRegistered<T>()
└── T[] GetOutputs<T>()
│
OutputPluginRegistry (Discovery)
├── List<OutputPluginInfo> DiscoverPlugins()
├── OutputPluginInfo GetPluginInfo<T>()
└── List<OutputPluginInfo> GetPluginsByCategory(string)
│
OutputPluginInfo (Plugin Metadata)
├── Type PluginType { get; }
├── string Name { get; }
├── string Description { get; }
├── string Category { get; }
├── string Version { get; }
└── IOutputTarget CreateInstance(params object[])
```

## Key Design Principles

```
┌────────────────────────────────────────────────────────┐
│  1. Single Responsibility                              │
│     Each plugin handles ONE output destination         │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│  2. Open/Closed Principle                              │
│     Open for extension (new plugins)                   │
│     Closed for modification (core unchanged)           │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│  3. Dependency Inversion                               │
│     OutputManager depends on IOutputTarget (interface) │
│     Not on concrete plugin implementations             │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│  4. Separation of Concerns                             │
│     Rendering ≠ Output Distribution ≠ Display          │
└────────────────────────────────────────────────────────┘

┌────────────────────────────────────────────────────────┐
│  5. Fail-Safe Operation                                │
│     Plugin errors don't crash the application          │
│     Error isolation per plugin                         │
└────────────────────────────────────────────────────────┘
```

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Implemented & Active |
| 🔨 | Scaffolded (Future Implementation) |
| ─→ | Data Flow |
| ↓ | Process Flow |
| │ | Dependency/Relationship |

---

**Commit 42 establishes a clean, extensible plugin architecture ready for future outputs.**
