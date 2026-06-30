// =============================================================================
// UnityCaptureSender.cpp
// Frame sender for UnityCapture virtual camera
// Receives frames from VirtualCamStudio via IPC, falls back to diagnostic frame
// =============================================================================

#include <windows.h>
#include <gdiplus.h>
#include <iostream>
#include <chrono>
#include <thread>

#pragma comment(lib, "gdiplus.lib")

using namespace std;
using namespace std::chrono;
using namespace Gdiplus;

// Include SharedImageMemory protocol and FrameIPC
#include "shared.inl"
#include "FrameIPC.h"

// Frame configuration
constexpr int FRAME_WIDTH = 1920;
constexpr int FRAME_HEIGHT = 1080;
constexpr int FRAME_SIZE = FRAME_WIDTH * FRAME_HEIGHT * 4; // RGBA32
constexpr int TARGET_FPS = 30;
constexpr int FRAME_DELAY_MS = 1000 / TARGET_FPS;

// =============================================================================
// Frame Generation Functions
// =============================================================================

/// <summary>
/// Fills the frame buffer with green screen (chroma key green: R=0, G=177, B=64, A=255)
/// </summary>
void FillGreenScreen(uint8_t* buffer)
{
	for (int i = 0; i < FRAME_WIDTH * FRAME_HEIGHT; i++)
	{
		buffer[i * 4 + 0] = 0;     // R
		buffer[i * 4 + 1] = 177;   // G (chroma key green)
		buffer[i * 4 + 2] = 64;    // B
		buffer[i * 4 + 3] = 255;   // A
	}
}

/// <summary>
/// Fills the frame buffer with solid blue background (R=0, G=0, B=255, A=255)
/// </summary>
void FillBlueBackground(uint8_t* buffer)
{
	for (int i = 0; i < FRAME_WIDTH * FRAME_HEIGHT; i++)
	{
		buffer[i * 4 + 0] = 0;     // R
		buffer[i * 4 + 1] = 0;     // G
		buffer[i * 4 + 2] = 255;   // B (blue)
		buffer[i * 4 + 3] = 255;   // A
	}
}

/// <summary>
/// Renders white text overlay using GDI+
/// </summary>
void RenderText(uint8_t* buffer, const wchar_t* text, const wchar_t* subtext)
{
	// Create GDI+ bitmap from buffer
	Bitmap bitmap(FRAME_WIDTH, FRAME_HEIGHT, FRAME_WIDTH * 4, PixelFormat32bppARGB, buffer);
	Graphics graphics(&bitmap);

	// Set rendering quality
	graphics.SetTextRenderingHint(TextRenderingHintAntiAlias);

	// Create font and brush
	FontFamily fontFamily(L"Arial");
	Font font(&fontFamily, 72, FontStyleBold, UnitPixel);
	Font subfont(&fontFamily, 48, FontStyleRegular, UnitPixel);
	SolidBrush brush(Color(255, 255, 255, 255)); // White

	// Measure text for centering
	RectF layoutRect(0, 0, (REAL)FRAME_WIDTH, (REAL)FRAME_HEIGHT);
	RectF boundingBox;
	graphics.MeasureString(text, -1, &font, layoutRect, &boundingBox);

	// Calculate centered position
	float x = (FRAME_WIDTH - boundingBox.Width) / 2.0f;
	float y = (FRAME_HEIGHT - boundingBox.Height) / 2.0f - 100;

	// Draw main text
	graphics.DrawString(text, -1, &font, PointF(x, y), &brush);

	// Draw subtext below
	graphics.MeasureString(subtext, -1, &subfont, layoutRect, &boundingBox);
	x = (FRAME_WIDTH - boundingBox.Width) / 2.0f;
	y += 150;
	graphics.DrawString(subtext, -1, &subfont, PointF(x, y), &brush);
}

/// <summary>
/// Converts ARGB (GDI+ format) to RGBA (UnityCapture format)
/// </summary>
void ConvertARGBtoRGBA(uint8_t* buffer)
{
	for (int i = 0; i < FRAME_WIDTH * FRAME_HEIGHT; i++)
	{
		uint8_t b = buffer[i * 4 + 0];
		uint8_t g = buffer[i * 4 + 1];
		uint8_t r = buffer[i * 4 + 2];
		uint8_t a = buffer[i * 4 + 3];

		buffer[i * 4 + 0] = r;
		buffer[i * 4 + 1] = g;
		buffer[i * 4 + 2] = b;
		buffer[i * 4 + 3] = a;
	}
}

// =============================================================================
// Main Program
// =============================================================================

int main()
{
	// Initialize GDI+
	std::cout << "[DEBUG] Starting UnityCaptureSender...\n" << std::flush;
	std::cout << "[DEBUG] Initializing GDI+...\n" << std::flush;
	GdiplusStartupInput gdiplusStartupInput;
	ULONG_PTR gdiplusToken;
	GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, nullptr);
	std::cout << "[DEBUG] GDI+ initialized\n" << std::flush;

	// Print header
	std::cout << "[DEBUG] Printing header...\n" << std::flush;
	cout << "=============================================================\n";
	cout << "  UnityCaptureSender - Frame Forwarder\n";
	cout << "  Receives from VirtualCamStudio, sends to Unity Video Capture\n";
	cout << "=============================================================\n\n";

	cout << "Configuration:\n";
	cout << "  Resolution: " << FRAME_WIDTH << "x" << FRAME_HEIGHT << "\n";
	cout << "  Frame Rate: " << TARGET_FPS << " FPS\n";
	cout << "  Fallback: Green screen (until Studio connects)\n\n";
	std::cout << "[DEBUG] Header printed\n" << std::flush;

	// Initialize FrameIPC for receiving from VirtualCamStudio
	std::cout << "[DEBUG] Initializing FrameIPC...\n" << std::flush;
	if (!FrameIPC::Initialize())
	{
		cout << "ERROR: Failed to initialize FrameIPC. Exiting.\n";
		GdiplusShutdown(gdiplusToken);
		return 1;
	}
	std::cout << "[DEBUG] FrameIPC initialized\n" << std::flush;

	// Allocate frame buffer
	std::cout << "[DEBUG] Allocating frame buffer...\n" << std::flush;
	uint8_t* frameBuffer = new uint8_t[FRAME_SIZE];
	std::cout << "[DEBUG] Frame buffer allocated\n" << std::flush;

	// Initialize SharedImageMemory sender for UnityCapture (capture device 0)
	std::cout << "[DEBUG] Creating SharedImageMemory...\n" << std::flush;
	SharedImageMemory sender(0);
	std::cout << "[DEBUG] SharedImageMemory created\n" << std::flush;

	// Note: We don't try to connect to UnityCapture yet - it will auto-connect on first frame send
	// This allows the sender to start even if no app is using Unity Video Capture yet
	std::cout << "[DEBUG] Initializing counters...\n" << std::flush;
	bool senderInitialized = false;

	// Diagnostic counters
	int framesAttempted = 0;
	int framesSent = 0;
	int framesFailed = 0;
	int totalAttempted = 0;
	int totalSent = 0;
	int totalFailed = 0;
	int consecutiveFailures = 0;
	bool failureWarningShown = false;
	int ipcFramesReceived = 0;
	int diagnosticFramesUsed = 0;

	// Timing
	auto lastReportTime = high_resolution_clock::now();
	auto firstFailureTime = high_resolution_clock::now();
	int frameNumber = 0;
	std::cout << "[DEBUG] Counters initialized\n" << std::flush;

	cout << "Starting frame transmission...\n";
	cout << "Waiting for VirtualCamStudio connection or using diagnostic frames...\n";
	cout << "Press Ctrl+C to stop.\n\n";
	std::cout << "[DEBUG] About to enter main loop...\n" << std::flush;

	// Main transmission loop
	while (true)
	{
		frameNumber++;

		// Try to receive frame from VirtualCamStudio via IPC
		uint32_t ipcWidth = 0;
		uint32_t ipcHeight = 0;
		uint32_t ipcStride = 0;
		bool ipcFrameReceived = FrameIPC::ReceiveFrame(
			frameBuffer,
			FRAME_SIZE,
			ipcWidth,
			ipcHeight,
			ipcStride,
			0  // Non-blocking
		);

		// If no IPC frame, generate green screen as fallback
		if (!ipcFrameReceived)
		{
			diagnosticFramesUsed++;

			// Generate green screen frame (no Studio connection yet)
			FillGreenScreen(frameBuffer);

			// No text overlay - just pure green screen
			// Convert ARGB to RGBA (though green screen is already in correct format)
			// This is a no-op but keeps the code structure consistent
			ConvertARGBtoRGBA(frameBuffer);

			// Use default dimensions for diagnostic frame
			ipcWidth = FRAME_WIDTH;
			ipcHeight = FRAME_HEIGHT;
			ipcStride = FRAME_WIDTH;
		}
		else
		{
			ipcFramesReceived++;
			// Accept any frame dimensions from VirtualCamStudio
		}

		// Increment attempted counter
		framesAttempted++;
		totalAttempted++;

		// Ensure sender is initialized before sending
		if (!senderInitialized)
		{
			if (sender.SendIsReady())
			{
				cout << "[UnityCapture] Connected to Unity Video Capture filter!\n";
				senderInitialized = true;
			}
			else
			{
				// Still not ready, skip this frame and wait
				this_thread::sleep_for(milliseconds(FRAME_DELAY_MS));
				continue;
			}
		}

		// DIAGNOSTIC: Log what we're about to send with multiple sample pixels
		static int sendFrameCount = 0;
		if (++sendFrameCount % 30 == 0) // Log every 30 frames
		{
			std::cout << "\n========== SENDER PIXEL DIAGNOSTIC (Frame " << sendFrameCount << ") ==========\n";
			std::cout << "[SEND] Frame dimensions: " << ipcWidth << "x" << ipcHeight << "\n";
			std::cout << "[SEND] Stride: " << ipcStride << ", Data size: " << (ipcWidth * ipcHeight * 4) << " bytes\n";

			// Sample multiple pixels (RGBA format)
			std::cout << "[SEND] Pixel [0,0] (top-left):     RGBA=(" << (int)frameBuffer[0] << "," << (int)frameBuffer[1] << "," << (int)frameBuffer[2] << "," << (int)frameBuffer[3] << ")\n";

			// Center pixel
			int centerOffset = ((ipcHeight / 2) * ipcWidth + (ipcWidth / 2)) * 4;
			std::cout << "[SEND] Pixel [center]:            RGBA=(" << (int)frameBuffer[centerOffset] << "," << (int)frameBuffer[centerOffset+1] << "," << (int)frameBuffer[centerOffset+2] << "," << (int)frameBuffer[centerOffset+3] << ")\n";

			// Sample a few more pixels to see if there's any color variation
			int sample1Offset = (100 * ipcWidth + 100) * 4;
			std::cout << "[SEND] Pixel [100,100]:           RGBA=(" << (int)frameBuffer[sample1Offset] << "," << (int)frameBuffer[sample1Offset+1] << "," << (int)frameBuffer[sample1Offset+2] << "," << (int)frameBuffer[sample1Offset+3] << ")\n";

			// Bottom-right area
			int bottomOffset = ((ipcHeight - 100) * ipcWidth + (ipcWidth - 100)) * 4;
			std::cout << "[SEND] Pixel [W-100,H-100]:       RGBA=(" << (int)frameBuffer[bottomOffset] << "," << (int)frameBuffer[bottomOffset+1] << "," << (int)frameBuffer[bottomOffset+2] << "," << (int)frameBuffer[bottomOffset+3] << ")\n";

			std::cout << "[SEND] Format being sent to UnityCapture: FORMAT_UINT8 (RGBA byte order)\n";
			std::cout << "=========================================================\n\n";
		}

		// Send frame via SharedImageMemory to UnityCapture
		SharedImageMemory::ESendResult result = sender.Send(
			ipcWidth,
			ipcHeight,
			ipcStride,
			ipcWidth * ipcHeight * 4,  // data size in bytes
			SharedImageMemory::FORMAT_UINT8,
			SharedImageMemory::RESIZEMODE_LINEAR,  // ← ENABLE RESIZE to handle resolution mismatch!
			SharedImageMemory::MIRRORMODE_DISABLED,
			1000,  // 1 second timeout
			frameBuffer
		);

		// Handle result and update counters
		bool sendSuccessful = false;
		switch (result)
		{
		case SharedImageMemory::SENDRES_OK:
			framesSent++;
			totalSent++;
			sendSuccessful = true;
			consecutiveFailures = 0;
			failureWarningShown = false;
			break;

		case SharedImageMemory::SENDRES_WARN_FRAMESKIP:
			framesSent++;
			totalSent++;
			sendSuccessful = true;
			consecutiveFailures = 0;
			failureWarningShown = false;
			break;

		case SharedImageMemory::SENDRES_TOOLARGE:
			framesFailed++;
			totalFailed++;
			cout << "FATAL ERROR: Frame too large for UnityCapture buffer.\n";
			cout << "Frame size: " << FRAME_SIZE << " bytes\n";
			delete[] frameBuffer;
			GdiplusShutdown(gdiplusToken);
			return 1;

		default:
			framesFailed++;
			totalFailed++;
			break;
		}

		// Track continuous failures
		if (!sendSuccessful)
		{
			if (consecutiveFailures == 0)
			{
				firstFailureTime = high_resolution_clock::now();
			}
			consecutiveFailures++;

			// Check if failures have continued for more than 1 second
			auto failureDuration = duration_cast<milliseconds>(high_resolution_clock::now() - firstFailureTime);
			if (failureDuration.count() >= 1000 && !failureWarningShown)
			{
				cout << "WARNING: UnityCapture not receiving frames (inactive or not running).\n";
				failureWarningShown = true;
			}
		}

		// Report FPS statistics once per second
		auto now = high_resolution_clock::now();
		auto timeSinceReport = duration_cast<milliseconds>(now - lastReportTime);

		if (timeSinceReport.count() >= 1000)
		{
			cout << "FPS Attempted: " << framesAttempted
				<< " | Sent: " << framesSent
				<< " | Failed: " << framesFailed
				<< " | IPC: " << ipcFramesReceived
				<< " | Diagnostic: " << diagnosticFramesUsed << "\n";

			// Reset per-second counters
			framesAttempted = 0;
			framesSent = 0;
			framesFailed = 0;
			ipcFramesReceived = 0;
			diagnosticFramesUsed = 0;
			lastReportTime = now;
		}

		// Sleep to maintain target FPS
		this_thread::sleep_for(milliseconds(FRAME_DELAY_MS));
	}

	// Cleanup (never reached in current implementation)
	FrameIPC::Shutdown();
	delete[] frameBuffer;
	GdiplusShutdown(gdiplusToken);

	return 0;
}
