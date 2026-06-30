# RuntimeBehaviorComparison.ps1
# Run both C++ and C# senders with full runtime logging and compare behavior

Write-Host "=== Runtime Behavior Comparison ===" -ForegroundColor Cyan
Write-Host ""

# Ensure C:\Temp exists
if (-not (Test-Path "C:\Temp")) {
	New-Item -ItemType Directory -Path "C:\Temp" | Out-Null
}

# Clean up old logs
if (Test-Path "C:\Temp\cpp_runtime.log") { Remove-Item "C:\Temp\cpp_runtime.log" }
if (Test-Path "C:\Temp\csharp_runtime.log") { Remove-Item "C:\Temp\csharp_runtime.log" }

Write-Host "Step 1: Running C++ UnityCaptureSender for 5 seconds..." -ForegroundColor Yellow
$cppProcess = Start-Process -FilePath "bin\Debug\x64\UnityCaptureSender\UnityCaptureSender.exe" `
	-RedirectStandardOutput "C:\Temp\cpp_runtime.log" `
	-RedirectStandardError "C:\Temp\cpp_runtime_err.log" `
	-PassThru -NoNewWindow

Start-Sleep -Seconds 5
$cppProcess.Kill()
Write-Host "  C++ sender stopped." -ForegroundColor Green

Write-Host ""
Write-Host "Step 2: Waiting 2 seconds for cleanup..." -ForegroundColor Yellow
Start-Sleep -Seconds 2

Write-Host ""
Write-Host "Step 3: Running C# VirtualCamStudio..." -ForegroundColor Yellow
Write-Host "  (This will capture Debug output via DebugView or VS Output window)" -ForegroundColor Gray
Write-Host "  Press Ctrl+C after a few frames have been sent" -ForegroundColor Gray
Write-Host ""

# For C#, we need to capture Debug.WriteLine output
# This goes to the debugger, not stdout
# User needs to check Visual Studio Output window or use DebugView
.\VirtualCamStudio\bin\Debug\net8.0-windows\VirtualCamStudio.exe

Write-Host ""
Write-Host "=== C++ Runtime Log (first 100 lines) ===" -ForegroundColor Cyan
if (Test-Path "C:\Temp\cpp_runtime.log") {
	Get-Content "C:\Temp\cpp_runtime.log" -TotalCount 100
} else {
	Write-Host "C++ log not found!" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Analysis Instructions ===" -ForegroundColor Yellow
Write-Host "1. C++ runtime log saved to: C:\Temp\cpp_runtime.log" -ForegroundColor White
Write-Host "2. C# runtime log is in Visual Studio Output window (Debug)" -ForegroundColor White
Write-Host "3. Compare the sequence of:" -ForegroundColor White
Write-Host "   - Open() calls and Win32 API return values" -ForegroundColor Gray
Write-Host "   - Send() calls and event signaling" -ForegroundColor Gray
Write-Host "   - Mutex/event handle values" -ForegroundColor Gray
Write-Host "   - maxSize initialization" -ForegroundColor Gray
Write-Host ""
Write-Host "4. Look for the FIRST difference in:" -ForegroundColor White
Write-Host "   - API return codes (handles, errors)" -ForegroundColor Gray
Write-Host "   - Sequence of operations" -ForegroundColor Gray
Write-Host "   - Values of shared memory fields" -ForegroundColor Gray
