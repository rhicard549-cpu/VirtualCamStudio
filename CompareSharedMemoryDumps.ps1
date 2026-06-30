# CompareSharedMemoryDumps.ps1
# Run both C++ and C# senders, capture dumps, and compare byte-level differences

Write-Host "=== Shared Memory Dump Comparison ===" -ForegroundColor Cyan
Write-Host ""

# Ensure C:\Temp exists
if (-not (Test-Path "C:\Temp")) {
	New-Item -ItemType Directory -Path "C:\Temp" | Out-Null
}

# Clean up any existing dump files
if (Test-Path "C:\Temp\cpp_sharedmem_dump.txt") {
	Remove-Item "C:\Temp\cpp_sharedmem_dump.txt"
}
if (Test-Path "C:\Temp\csharp_sharedmem_dump.txt") {
	Remove-Item "C:\Temp\csharp_sharedmem_dump.txt"
}

Write-Host "Step 1: Running C++ UnityCaptureSender..." -ForegroundColor Yellow
$cppProcess = Start-Process -FilePath "UnityCaptureSender\x64\Release\UnityCaptureSender.exe" -PassThru -NoNewWindow
Start-Sleep -Seconds 3  # Wait for first frame dump
$cppProcess.Kill()
Write-Host "  C++ sender stopped." -ForegroundColor Green

if (-not (Test-Path "C:\Temp\cpp_sharedmem_dump.txt")) {
	Write-Host "ERROR: C++ dump file not created!" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "Step 2: Running C# VirtualCamStudio..." -ForegroundColor Yellow
$csProcess = Start-Process -FilePath "VirtualCamStudio\bin\Release\net8.0-windows\VirtualCamStudio.exe" -PassThru -NoNewWindow
Start-Sleep -Seconds 5  # Wait for WPF startup and first frame dump
$csProcess.Kill()
Write-Host "  C# sender stopped." -ForegroundColor Green

if (-not (Test-Path "C:\Temp\csharp_sharedmem_dump.txt")) {
	Write-Host "ERROR: C# dump file not created!" -ForegroundColor Red
	exit 1
}

Write-Host ""
Write-Host "Step 3: Comparing dumps..." -ForegroundColor Yellow
Write-Host ""

# Read both dump files
$cppDump = Get-Content "C:\Temp\cpp_sharedmem_dump.txt"
$csharpDump = Get-Content "C:\Temp\csharp_sharedmem_dump.txt"

# Compare line by line
$differences = @()
$maxLines = [Math]::Max($cppDump.Count, $csharpDump.Count)

for ($i = 0; $i -lt $maxLines; $i++) {
	$cppLine = if ($i -lt $cppDump.Count) { $cppDump[$i] } else { "" }
	$csLine = if ($i -lt $csharpDump.Count) { $csharpDump[$i] } else { "" }

	if ($cppLine -ne $csLine) {
		$differences += [PSCustomObject]@{
			Line = $i + 1
			CPP = $cppLine
			CSharp = $csLine
		}
	}
}

if ($differences.Count -eq 0) {
	Write-Host "SUCCESS: Dumps are identical!" -ForegroundColor Green
} else {
	Write-Host "DIFFERENCES FOUND:" -ForegroundColor Red
	Write-Host ""

	foreach ($diff in $differences) {
		Write-Host "Line $($diff.Line):" -ForegroundColor Yellow
		Write-Host "  C++:    $($diff.CPP)" -ForegroundColor Cyan
		Write-Host "  C#:     $($diff.CSharp)" -ForegroundColor Magenta
		Write-Host ""
	}

	# Extract and compare header bytes specifically
	Write-Host "=== HEADER BYTES COMPARISON ===" -ForegroundColor Cyan
	$cppHeaderLines = $cppDump | Select-String -Pattern "^[0-9A-F]{2} " -Context 0
	$csHeaderLines = $csharpDump | Select-String -Pattern "^[0-9A-F]{2} " -Context 0

	Write-Host "C++ Header Bytes:" -ForegroundColor Cyan
	$cppHeaderLines | ForEach-Object { Write-Host "  $_" }
	Write-Host ""
	Write-Host "C# Header Bytes:" -ForegroundColor Magenta
	$csHeaderLines | ForEach-Object { Write-Host "  $_" }
	Write-Host ""

	# Find first differing byte
	Write-Host "=== FIRST DIFFERING BYTE ===" -ForegroundColor Cyan
	$cppBytes = ($cppHeaderLines -join " ").Split(" ", [StringSplitOptions]::RemoveEmptyEntries)
	$csBytes = ($csHeaderLines -join " ").Split(" ", [StringSplitOptions]::RemoveEmptyEntries)

	for ($i = 0; $i -lt [Math]::Min($cppBytes.Count, $csBytes.Count); $i++) {
		if ($cppBytes[$i] -ne $csBytes[$i]) {
			Write-Host "First difference at byte offset $i" -ForegroundColor Yellow
			Write-Host "  C++ byte: 0x$($cppBytes[$i])" -ForegroundColor Cyan
			Write-Host "  C#  byte: 0x$($csBytes[$i])" -ForegroundColor Magenta
			break
		}
	}
}

Write-Host ""
Write-Host "Dump files available at:" -ForegroundColor Cyan
Write-Host "  C:\Temp\cpp_sharedmem_dump.txt"
Write-Host "  C:\Temp\csharp_sharedmem_dump.txt"
