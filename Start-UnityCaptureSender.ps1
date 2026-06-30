# UnityCapture Quick Start Script
# This script launches UnityCaptureSender.exe for testing with VirtualCamStudio

$senderPath = "UnityCaptureSender\bin\Debug\x64\UnityCaptureSender\UnityCaptureSender.exe"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " UnityCapture IPC Integration Launcher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if the sender binary exists
if (-not (Test-Path $senderPath)) {
	Write-Host "ERROR: UnityCaptureSender.exe not found!" -ForegroundColor Red
	Write-Host "Expected location: $senderPath" -ForegroundColor Yellow
	Write-Host ""
	Write-Host "Please build the native project first:" -ForegroundColor Yellow
	Write-Host "  msbuild UnityCaptureSender\UnityCaptureSender.vcxproj /p:Configuration=Debug /p:Platform=x64" -ForegroundColor White
	Write-Host ""
	Write-Host "Or use Visual Studio:" -ForegroundColor Yellow
	Write-Host "  1. Open VirtualCamStudio.slnx" -ForegroundColor White
	Write-Host "  2. Set configuration to Debug | x64" -ForegroundColor White
	Write-Host "  3. Build > Build UnityCaptureSender" -ForegroundColor White
	Write-Host ""
	exit 1
}

Write-Host "✓ UnityCaptureSender.exe found" -ForegroundColor Green
Write-Host ""
Write-Host "Starting UnityCaptureSender..." -ForegroundColor Cyan
Write-Host "This process will:" -ForegroundColor White
Write-Host "  1. Create named pipe: \\.\pipe\VirtualCamStudio_Frames" -ForegroundColor White
Write-Host "  2. Wait for VirtualCamStudio to connect" -ForegroundColor White
Write-Host "  3. Show blue diagnostic frames until IPC connects" -ForegroundColor White
Write-Host "  4. Forward received frames to Unity Video Capture" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Launch VirtualCamStudio" -ForegroundColor White
Write-Host "  2. Click 'Start UnityCapture' button" -ForegroundColor White
Write-Host "  3. Load media and render frames" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop UnityCaptureSender" -ForegroundColor Yellow
Write-Host ""
Write-Host "----------------------------------------" -ForegroundColor DarkGray
Write-Host ""

# Launch UnityCaptureSender
& $senderPath

Write-Host ""
Write-Host "UnityCaptureSender stopped." -ForegroundColor Yellow
