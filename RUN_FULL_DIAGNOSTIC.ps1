# VirtualCamStudio Full Diagnostic Test
# This script runs both components with full logging and collects diagnostic data

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "VirtualCamStudio Full Diagnostic Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Launch UnityCaptureSender
Write-Host "Step 1: Starting UnityCaptureSender..." -ForegroundColor Yellow
Write-Host "Look for these key messages:" -ForegroundColor Gray
Write-Host "  - '[UnityCaptureSender] Starting up...'" -ForegroundColor Gray
Write-Host "  - '[SharedImageMemory] Initialization successful'" -ForegroundColor Gray
Write-Host "  - '[FrameIPC] VirtualCamStudio connected.'" -ForegroundColor Gray
Write-Host "  - '[FrameIPC] Received frame X: WxH, first pixel: (R,G,B,A)'" -ForegroundColor Gray
Write-Host "  - '[DEBUG] Sending frame X to UnityCapture: WxH, first pixel: (R,G,B,A)'" -ForegroundColor Gray
Write-Host ""

$senderProcess = Start-Process -FilePath "D:\Projects\VirtualCamStudio\UnityCaptureSender\UnityCaptureSender.exe" -PassThru -WindowStyle Normal
Write-Host "Sender started (PID: $($senderProcess.Id)). Waiting 3 seconds..." -ForegroundColor Green
Start-Sleep -Seconds 3

# Step 2: Launch VirtualCamStudio
Write-Host ""
Write-Host "Step 2: Starting VirtualCamStudio..." -ForegroundColor Yellow
Write-Host "The Studio will launch in a moment." -ForegroundColor Gray
Write-Host ""

$studioProcess = Start-Process -FilePath "D:\Projects\VirtualCamStudio\VirtualCamStudio\bin\Debug\net8.0-windows\VirtualCamStudio.exe" -PassThru -WindowStyle Normal
Write-Host "Studio started (PID: $($studioProcess.Id)). Waiting 3 seconds..." -ForegroundColor Green
Start-Sleep -Seconds 3

# Step 3: Instructions for user
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "MANUAL TEST STEPS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "In VirtualCamStudio:" -ForegroundColor Yellow
Write-Host "  1. Click 'Start Unity Capture' button" -ForegroundColor White
Write-Host "  2. Click 'Add Image' or 'Add Video' to load media" -ForegroundColor White
Write-Host "  3. Select a test image or video file" -ForegroundColor White
Write-Host "  4. Observe the preview in Studio" -ForegroundColor White
Write-Host ""
Write-Host "Expected Console Output:" -ForegroundColor Yellow
Write-Host "  FROM SENDER WINDOW:" -ForegroundColor Cyan
Write-Host "    - '[FrameIPC] VirtualCamStudio connected.'" -ForegroundColor Gray
Write-Host "    - Every 30 frames: '[FrameIPC] Received frame X: WxH, first pixel: (R,G,B,A)'" -ForegroundColor Gray
Write-Host "    - Every 30 frames: '[DEBUG] Sending frame X to UnityCapture: WxH, first pixel: (R,G,B,A)'" -ForegroundColor Gray
Write-Host ""
Write-Host "  FROM STUDIO (Visual Studio Output > Debug):" -ForegroundColor Cyan
Write-Host "    - '[UnityCaptureOutput] Connected to sender pipe'" -ForegroundColor Gray
Write-Host "    - '[UnityCaptureOutput] Frame: WxH, First pixel BGRA: (B,G,R,A)'" -ForegroundColor Gray
Write-Host "    - '[UnityCaptureOutput] Frame: Center pixel BGRA: (B,G,R,A)'" -ForegroundColor Gray
Write-Host ""
Write-Host "In CloudPhone or Webcamtests:" -ForegroundColor Yellow
Write-Host "  1. Open CloudPhone or https://webcamtests.com/" -ForegroundColor White
Write-Host "  2. Select 'Unity Video Capture' as camera" -ForegroundColor White
Write-Host "  3. Observe what is displayed" -ForegroundColor White
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "DIAGNOSTIC COMPARISON" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Compare the pixel values across three points:" -ForegroundColor Yellow
Write-Host "  1. Studio Output (BGRA format): First pixel values from Debug output" -ForegroundColor White
Write-Host "  2. Sender Received (should match Studio after BGRA->RGBA): FrameIPC output" -ForegroundColor White
Write-Host "  3. Sender Sent (what goes to UnityCapture): DEBUG output" -ForegroundColor White
Write-Host ""
Write-Host "If the pixel values MATCH but CloudPhone shows wrong colors:" -ForegroundColor Yellow
Write-Host "  -> The problem is in UnityCapture driver or CloudPhone interpretation" -ForegroundColor Red
Write-Host ""
Write-Host "If the pixel values DON'T MATCH between Studio and Sender:" -ForegroundColor Yellow
Write-Host "  -> The problem is in the IPC transmission or format conversion" -ForegroundColor Red
Write-Host ""
Write-Host "If CloudPhone shows green/purple instead of your image:" -ForegroundColor Yellow
Write-Host "  -> Check if pixel values are actually changing from the diagnostic frames" -ForegroundColor Red
Write-Host "  -> Green = diagnostic frame from sender (means Studio frames not arriving)" -ForegroundColor Red
Write-Host "  -> Purple = some corruption or format mismatch" -ForegroundColor Red
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Press Enter when ready to stop all processes..." -ForegroundColor Yellow
Read-Host

# Cleanup
Write-Host ""
Write-Host "Stopping processes..." -ForegroundColor Yellow
if (!$studioProcess.HasExited) { Stop-Process -Id $studioProcess.Id -Force }
if (!$senderProcess.HasExited) { Stop-Process -Id $senderProcess.Id -Force }
Write-Host "Done!" -ForegroundColor Green
