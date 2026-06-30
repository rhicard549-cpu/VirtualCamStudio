// =============================================================================
// FrameIPC.cpp
// Named pipe implementation for receiving rendered frames from VirtualCamStudio
// =============================================================================

#include "FrameIPC.h"
#include <iostream>

namespace FrameIPC
{
	// Internal state
	static HANDLE g_hPipe = INVALID_HANDLE_VALUE;
	static bool g_clientConnected = false;
	static bool g_waitingForClient = false;
	static OVERLAPPED g_overlapped = { 0 };

	bool Initialize()
	{
		if (g_hPipe != INVALID_HANDLE_VALUE)
		{
			std::cout << "[FrameIPC] Already initialized.\n";
			return true;
		}

		// Create named pipe
		g_hPipe = CreateNamedPipeW(
			PIPE_NAME,
			PIPE_ACCESS_INBOUND |          // Server receives data
			FILE_FLAG_OVERLAPPED,           // Non-blocking I/O
			PIPE_TYPE_BYTE |                // Byte stream mode
			PIPE_READMODE_BYTE |
			PIPE_WAIT,
			1,                              // Max instances
			0,                              // No outbound buffer needed
			PIPE_BUFFER_SIZE,               // Inbound buffer size
			0,                              // Default timeout
			nullptr                         // Default security
		);

		if (g_hPipe == INVALID_HANDLE_VALUE)
		{
			std::cout << "[FrameIPC] ERROR: Failed to create named pipe. Error: " << GetLastError() << "\n";
			return false;
		}

		std::cout << "[FrameIPC] Named pipe created: " << PIPE_NAME << "\n";
		std::cout << "[FrameIPC] Waiting for VirtualCamStudio to connect...\n";

		return true;
	}

	bool IsClientConnected()
	{
		if (g_hPipe == INVALID_HANDLE_VALUE)
			return false;

		// Check if we need to wait for a client
		if (!g_clientConnected && !g_waitingForClient)
		{
			// Start async connection wait
			ZeroMemory(&g_overlapped, sizeof(OVERLAPPED));
			g_overlapped.hEvent = CreateEvent(nullptr, TRUE, FALSE, nullptr);

			BOOL connected = ConnectNamedPipe(g_hPipe, &g_overlapped);
			DWORD lastError = GetLastError();

			if (!connected)
			{
				if (lastError == ERROR_IO_PENDING)
				{
					// Connection is pending (normal for async)
					g_waitingForClient = true;
				}
				else if (lastError == ERROR_PIPE_CONNECTED)
				{
					// Client already connected
					g_clientConnected = true;
					g_waitingForClient = false;
					std::cout << "[FrameIPC] VirtualCamStudio connected.\n";
					if (g_overlapped.hEvent) CloseHandle(g_overlapped.hEvent);
					g_overlapped.hEvent = nullptr;
				}
				else
				{
					// Connection failed
					if (g_overlapped.hEvent) CloseHandle(g_overlapped.hEvent);
					g_overlapped.hEvent = nullptr;
				}
			}
			else
			{
				// Immediate connection
				g_clientConnected = true;
				g_waitingForClient = false;
				std::cout << "[FrameIPC] VirtualCamStudio connected.\n";
				if (g_overlapped.hEvent) CloseHandle(g_overlapped.hEvent);
				g_overlapped.hEvent = nullptr;
			}
		}
		else if (g_waitingForClient)
		{
			// Check if connection completed
			DWORD dummy;
			if (GetOverlappedResult(g_hPipe, &g_overlapped, &dummy, FALSE))
			{
				g_clientConnected = true;
				g_waitingForClient = false;
				std::cout << "[FrameIPC] VirtualCamStudio connected.\n";
				if (g_overlapped.hEvent) CloseHandle(g_overlapped.hEvent);
				g_overlapped.hEvent = nullptr;
			}
		}

		return g_clientConnected;
	}

	bool ReceiveFrame(
		uint8_t* outBuffer,
		uint32_t bufferSize,
		uint32_t& outWidth,
		uint32_t& outHeight,
		uint32_t& outStride,
		DWORD timeoutMs)
	{
		if (g_hPipe == INVALID_HANDLE_VALUE || outBuffer == nullptr)
			return false;

		if (!IsClientConnected())
			return false; // No client, no frame available

		// Step 1: Read frame header
		FrameHeader header;
		DWORD bytesRead = 0;

		BOOL success = ReadFile(
			g_hPipe,
			&header,
			sizeof(FrameHeader),
			&bytesRead,
			nullptr
		);

		if (!success || bytesRead != sizeof(FrameHeader))
		{
			DWORD lastError = GetLastError();
			if (lastError == ERROR_NO_DATA || lastError == ERROR_BROKEN_PIPE)
			{
				// Client disconnected
				std::cout << "[FrameIPC] VirtualCamStudio disconnected.\n";
				g_clientConnected = false;
				DisconnectNamedPipe(g_hPipe);
				return false;
			}
			return false; // No data available yet
		}

		// Validate header
		if (header.dataSize > bufferSize)
		{
			std::cout << "[FrameIPC] ERROR: Frame too large. Expected <= " << bufferSize 
					  << " bytes, got " << header.dataSize << " bytes.\n";
			return false;
		}

		if (header.pixelFormat != 0) // Only RGBA32 supported
		{
			std::cout << "[FrameIPC] ERROR: Unsupported pixel format: " << header.pixelFormat << "\n";
			return false;
		}

		// Step 2: Read pixel data
		DWORD totalBytesRead = 0;
		while (totalBytesRead < header.dataSize)
		{
			DWORD chunkBytesRead = 0;
			success = ReadFile(
				g_hPipe,
				outBuffer + totalBytesRead,
				header.dataSize - totalBytesRead,
				&chunkBytesRead,
				nullptr
			);

			if (!success)
			{
				DWORD lastError = GetLastError();
				if (lastError == ERROR_BROKEN_PIPE)
				{
					std::cout << "[FrameIPC] VirtualCamStudio disconnected during frame transfer.\n";
					g_clientConnected = false;
					DisconnectNamedPipe(g_hPipe);
					return false;
				}
				return false;
			}

			totalBytesRead += chunkBytesRead;
		}

		// Success - return frame metadata
		outWidth = header.width;
		outHeight = header.height;
		outStride = header.stride;

		// DIAGNOSTIC: Log received frame info with multiple sample pixels
		static int frameCount = 0;
		if (++frameCount % 30 == 0) // Log every 30 frames
		{
			std::cout << "\n========== IPC RECEIVE DIAGNOSTIC (Frame " << frameCount << ") ==========\n";
			std::cout << "[RECV] Frame dimensions: " << outWidth << "x" << outHeight << "\n";
			std::cout << "[RECV] Stride: " << outStride << ", Data size: " << header.dataSize << " bytes\n";
			std::cout << "[RECV] Pixel format: " << header.pixelFormat << " (0=RGBA)\n";

			// Sample multiple pixels (RGBA format)
			std::cout << "[RECV] Pixel [0,0] (top-left):     RGBA=(" << (int)outBuffer[0] << "," << (int)outBuffer[1] << "," << (int)outBuffer[2] << "," << (int)outBuffer[3] << ")\n";

			// Center pixel
			int centerOffset = ((outHeight / 2) * outWidth + (outWidth / 2)) * 4;
			std::cout << "[RECV] Pixel [center]:            RGBA=(" << (int)outBuffer[centerOffset] << "," << (int)outBuffer[centerOffset+1] << "," << (int)outBuffer[centerOffset+2] << "," << (int)outBuffer[centerOffset+3] << ")\n";

			// Additional samples
			int sample1Offset = (100 * outWidth + 100) * 4;
			std::cout << "[RECV] Pixel [100,100]:           RGBA=(" << (int)outBuffer[sample1Offset] << "," << (int)outBuffer[sample1Offset+1] << "," << (int)outBuffer[sample1Offset+2] << "," << (int)outBuffer[sample1Offset+3] << ")\n";

			int bottomOffset = ((outHeight - 100) * outWidth + (outWidth - 100)) * 4;
			std::cout << "[RECV] Pixel [W-100,H-100]:       RGBA=(" << (int)outBuffer[bottomOffset] << "," << (int)outBuffer[bottomOffset+1] << "," << (int)outBuffer[bottomOffset+2] << "," << (int)outBuffer[bottomOffset+3] << ")\n";

			std::cout << "=========================================================\n\n";
		}

		return true;
	}

	void Shutdown()
	{
		if (g_hPipe != INVALID_HANDLE_VALUE)
		{
			if (g_clientConnected)
			{
				DisconnectNamedPipe(g_hPipe);
				g_clientConnected = false;
			}

			CloseHandle(g_hPipe);
			g_hPipe = INVALID_HANDLE_VALUE;

			if (g_overlapped.hEvent)
			{
				CloseHandle(g_overlapped.hEvent);
				g_overlapped.hEvent = nullptr;
			}
			g_waitingForClient = false;

			std::cout << "[FrameIPC] Shutdown complete.\n";
		}
	}
}
