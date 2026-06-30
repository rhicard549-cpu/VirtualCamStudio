# Output Plugin Quick Reference

## Plugin Interface

```csharp
public interface IOutputTarget
{
	Task SendFrameAsync(Frame frame);
}
```

## Creating a Plugin

```csharp
[OutputPlugin("Plugin Name", "Description", "Category", "1.0.0")]
public class MyPlugin : IOutputTarget
{
	public Task SendFrameAsync(Frame frame)
	{
		// Your code here
		return Task.CompletedTask;
	}
}
```

## Using OutputManager

```csharp
// Register
outputManager.Register(plugin);

// Unregister
outputManager.Unregister(plugin);

// Broadcast frame
await outputManager.SendFrameAsync(frame);

// Query
int count = outputManager.OutputCount;
IOutputTarget[] all = outputManager.GetRegisteredOutputs();
bool exists = outputManager.IsRegistered(plugin);
bool hasType = outputManager.IsRegistered<PreviewOutput>();
PreviewOutput[] previews = outputManager.GetOutputs<PreviewOutput>();
```

## Plugin Discovery

```csharp
// Discover all plugins
var plugins = OutputPluginRegistry.DiscoverPlugins();

// Get specific plugin info
var info = OutputPluginRegistry.GetPluginInfo<PreviewOutput>();

// Get plugins by category
var streaming = OutputPluginRegistry.GetPluginsByCategory("Streaming");
```

## Available Plugins

| Plugin | Category | Status |
|--------|----------|--------|
| PreviewOutput | Display | ✅ Active |
| OBSOutput | Streaming | 🔨 Future |
| RecordingOutput | Recording | 🔨 Future |
| ScreenshotOutput | Recording | 🔨 Future |
| StreamOutput | Streaming | 🔨 Future |

## Rules

1. **Never dispose the frame** - RenderPipeline owns disposal
2. **Never modify the frame** - Read-only access
3. **Handle errors gracefully** - Don't throw unhandled exceptions
4. **Be thread-safe** - Multiple frames may arrive concurrently
5. **Return quickly** - Don't block the render thread

## Example: Custom Plugin

```csharp
[OutputPlugin("Frame Counter", "Counts rendered frames", "Debug", "1.0.0")]
public class FrameCounterOutput : IOutputTarget
{
	private int _frameCount = 0;

	public Task SendFrameAsync(Frame frame)
	{
		Interlocked.Increment(ref _frameCount);
		Debug.WriteLine($"Frame #{_frameCount}: {frame.Width}x{frame.Height}");
		return Task.CompletedTask;
	}

	public int FrameCount => _frameCount;
}
```

## Registration Example (MainWindow.xaml.cs)

```csharp
// Create OutputManager
var outputManager = new Outputs.OutputManager();

// Register preview
var preview = new Outputs.PreviewOutput(PreviewImage);
outputManager.Register(preview);

// Register custom plugin
var counter = new FrameCounterOutput();
outputManager.Register(counter);

// Pass to RenderPipeline
_renderPipeline = new RenderPipeline(
	_renderLoop,
	_mediaController,
	_viewportEngine,
	_legacyOutputManager,
	outputManager  // New plugin-based system
);
```
