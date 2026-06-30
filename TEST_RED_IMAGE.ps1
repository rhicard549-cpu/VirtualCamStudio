# Red Image Test Launcher
Write-Host "================================================" -ForegroundColor Red
Write-Host "  BRIGHT RED IMAGE TEST" -ForegroundColor Red
Write-Host "================================================" -ForegroundColor White
Write-Host ""

Write-Host "Step 1: Starting UnityCaptureSender..." -ForegroundColor Yellow
Write-Host "Watch for pixel diagnostics every 30 frames" -ForegroundColor Gray
Write-Host ""

Start-Process -FilePath "D:\Projects\VirtualCamStudio\UnityCaptureSender\UnityCaptureSender.exe" -WindowStyle Normal
Start-Sleep -Seconds 2

Write-Host "Step 2: Instructions" -ForegroundColor Yellow
Write-Host "  1. Press F5 in Visual Studio to start VirtualCamStudio" -ForegroundColor White
Write-Host "  2. Load a BRIGHT RED image" -ForegroundColor White
Write-Host "  3. Click 'Start Unity Capture'" -ForegroundColor White
Write-Host "  4. Watch sender console for pixel values" -ForegroundColor White
Write-Host ""
Write-Host "Expected for RED image: RGBA=(255,0,0,255)" -ForegroundColor Red
Write-Host ""
Write-Host "Press Enter to close..." -ForegroundColor Gray
Read-Host
