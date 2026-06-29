# Preview Diagnostic Steps

## Quick Test to Isolate the Issue

Add this temporary button to MainWindow.xaml (inside any Grid or StackPanel):

```xml
<Button x:Name="TestRenderButton" 
		Content="TEST RENDER NOW" 
		Click="TestRenderButton_Click"
		Margin="10"/>
```

Add this to MainWindow.xaml.cs:

```csharp
private void TestRenderButton_Click(object sender, RoutedEventArgs e)
{
	System.Diagnostics.Debug.WriteLine("========== MANUAL RENDER TEST ==========");

	// Check if media is loaded
	if (!_mediaController.HasMedia)
	{
		System.Diagnostics.Debug.WriteLine("❌ No media loaded");
		StatusText.Text = "No media loaded - drag a file first";
		return;
	}

	System.Diagnostics.Debug.WriteLine($"✓ Media loaded: {_mediaController.CurrentMedia?.FileName}");

	// Get current frame
	var sourceFrame = _mediaController.GetCurrentFrame();
	if (sourceFrame == null || sourceFrame.Empty())
	{
		System.Diagnostics.Debug.WriteLine("❌ Source frame is null or empty");
		return;
	}

	System.Diagnostics.Debug.WriteLine($"✓ Source frame: {sourceFrame.Width}x{sourceFrame.Height}");

	// Check active profile
	if (ActiveProfile == null)
	{
		System.Diagnostics.Debug.WriteLine("❌ No active profile");
		return;
	}

	System.Diagnostics.Debug.WriteLine($"✓ Active profile: {ActiveProfile.Name} ({ActiveProfile.DisplayWidth}x{ActiveProfile.DisplayHeight})");

	// Render manually
	var frame = _viewportEngine.Render(
		sourceFrame,
		ActiveProfile.DisplayWidth,
		ActiveProfile.DisplayHeight,
		_renderPipeline.FramingSettings);

	System.Diagnostics.Debug.WriteLine($"✓ Frame rendered: {frame.Width}x{frame.Height}");

	// Convert to bitmap
	var bitmap = MatToBitmapSource.Convert(frame.Image);
	if (bitmap == null)
	{
		System.Diagnostics.Debug.WriteLine("❌ Bitmap conversion failed");
		frame.Dispose();
		return;
	}

	System.Diagnostics.Debug.WriteLine($"✓ Bitmap created: {bitmap.PixelWidth}x{bitmap.PixelHeight}");

	// Update preview directly
	PreviewImage.Source = bitmap;
	System.Diagnostics.Debug.WriteLine("✓ Preview updated directly");
	StatusText.Text = "Manual render successful!";

	frame.Dispose();
	System.Diagnostics.Debug.WriteLine("========================================");
}
```

## What This Tests

1. **If clicking the button shows the preview** → The pipeline works, but RenderLoop isn't firing
2. **If the button doesn't show preview** → Check which step fails in Debug Output
3. **If you get an exception** → That's the root cause

## After Testing

Share the Debug Output from clicking the test button so we can see exactly where it fails.
