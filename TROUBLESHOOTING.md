# VirtualCamStudio Troubleshooting Guide

## Window Not Appearing Issue

### Diagnostic Steps Implemented

The application now includes comprehensive error logging to diagnose why the window might not appear:

1. **Error Log Location**: `Desktop\VirtualCamStudio_Error.log`
   - This file will be created automatically when you launch the app
   - Contains detailed startup sequence and any errors

2. **What to Check**:
   - Launch the application
   - Check your Desktop for `VirtualCamStudio_Error.log`
   - Open the log file and look for error messages

### Common Causes

#### 1. **Off-Screen Window**
**Symptom**: App is running but window not visible
**Solution**: 
- The window might be positioned off-screen on a disconnected monitor
- Delete the app settings to reset window position
- Settings location: `%APPDATA%\VirtualCamStudio\` or similar

#### 2. **Missing Dependencies**
**Symptom**: App crashes immediately on startup
**Common missing dependencies**:
- OpenCvSharp4 runtime
- .NET 8 Runtime
- Visual C++ Redistributables

**Solution**:
- Reinstall .NET 8 Runtime from: https://dotnet.microsoft.com/download/dotnet/8.0
- Check if OpenCvSharp native binaries are in the output folder

#### 3. **Exception During Initialization**
**Symptom**: Process appears in Task Manager but no window
**Check**: 
- Look in `VirtualCamStudio_Error.log` for exception details
- Common issues:
  - Camera profile loading failure
  - OBS service initialization failure
  - Media service initialization failure

### Debug Mode

The application now logs each initialization step:
1. Application starting
2. Exception handlers registered
3. Creating MainWindow
4. MainWindow created (with visibility status)
5. Show() called
6. Activate() called

If the log stops at any point, that's where the failure occurs.

### Quick Fixes

#### Reset Application State
Delete these folders if they exist:
- `%APPDATA%\VirtualCamStudio\`
- `%LOCALAPPDATA%\VirtualCamStudio\`

#### Check File Permissions
Ensure the application has write access to:
- Desktop (for error logs)
- AppData folders (for settings)

#### Run as Administrator
Right-click the .exe → "Run as Administrator"

### Getting Help

When reporting the issue, please include:
1. Contents of `VirtualCamStudio_Error.log` from your Desktop
2. Your OS version (Windows 10/11)
3. .NET version (`dotnet --version` in command prompt)
4. Whether OpenCV dependencies are present in the app folder

### Manual Window Creation Test

If automatic startup fails, the app now:
1. Creates comprehensive error logs
2. Shows message boxes with error details
3. Provides exact exception messages and stack traces

Check the log file immediately after launch to see exactly where initialization fails.
