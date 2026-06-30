# Building UnityCaptureSender - Quick Fix

## Issue
The UnityCaptureSender C++ project can't build from command line due to toolset version mismatch.

## Solution: Build from Visual Studio

### Step 1: Open the Project in Visual Studio
1. Open Visual Studio 2026
2. File → Open → Project/Solution
3. Navigate to: `D:\Projects\VirtualCamStudio\UnityCaptureSender`
4. Open `UnityCaptureSender.vcxproj`

### Step 2: Retarget the Project
1. Right-click the project in Solution Explorer
2. Select **"Retarget Projects"**
3. Choose the latest Windows SDK and Platform Toolset
4. Click OK

### Step 3: Build
1. Select **Release** configuration and **x64** platform from the toolbar
2. Build → Build Solution (or press Ctrl+Shift+B)
3. The executable will be created at:
   `D:\Projects\VirtualCamStudio\UnityCaptureSender\x64\Release\UnityCaptureSender.exe`

## Alternative: Use Existing Debug Build

A Debug build already exists at:
```
D:\Projects\VirtualCamStudio\UnityCaptureSender\bin\Debug\x64\UnityCaptureSender\UnityCaptureSender.exe
```

You can use this for testing. The Debug build works the same as Release, just with debug symbols.

## After Building

Once the executable exists, continue with the test instructions in QUICK_START_TEST.md:

1. Start UnityCaptureSender.exe
2. Launch VirtualCam Studio
3. Load media
4. Click "Start Unity Video Capture"
5. Open CloudPhone and test

## Important Note

The `shared.inl` file has now been copied from Research/UnityCapture to the UnityCaptureSender directory, so the build should succeed once the toolset is properly configured in Visual Studio.
