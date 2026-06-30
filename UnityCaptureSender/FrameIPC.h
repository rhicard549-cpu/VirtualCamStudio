// =============================================================================
// FrameIPC.h
// Named pipe interface for receiving rendered frames from VirtualCamStudio
// =============================================================================

#pragma once

#include <windows.h>
#include <cstdint>

namespace FrameIPC
{
	// Frame header structure (sent before pixel data)
	struct FrameHeader
	{
		uint32_t width;
		uint32_t height;
		uint32_t stride;        // pixels per row
		uint32_t dataSize;      // total bytes of pixel data
		uint32_t pixelFormat;   // 0 = RGBA32 (UnityCapture expects RGBA, then converts internally to BGRA)
	};

	// Named pipe configuration
	constexpr const wchar_t* PIPE_NAME = L"\\\\.\\pipe\\VirtualCamStudio_Frames";
	constexpr DWORD PIPE_BUFFER_SIZE = 10 * 1024 * 1024; // 10 MB buffer

	/// <summary>
	/// Initializes the named pipe server for receiving frames
	/// </summary>
	/// <returns>True if pipe created successfully, false otherwise</returns>
	bool Initialize();

	/// <summary>
	/// Attempts to receive a frame from VirtualCamStudio via named pipe
	/// </summary>
	/// <param name="outBuffer">Pre-allocated buffer to receive pixel data</param>
	/// <param name="bufferSize">Size of the output buffer in bytes</param>
	/// <param name="outWidth">Receives the frame width</param>
	/// <param name="outHeight">Receives the frame height</param>
	/// <param name="outStride">Receives the frame stride</param>
	/// <param name="timeoutMs">Timeout in milliseconds (0 = non-blocking)</param>
	/// <returns>True if frame received, false if no frame available or error</returns>
	bool ReceiveFrame(
		uint8_t* outBuffer,
		uint32_t bufferSize,
		uint32_t& outWidth,
		uint32_t& outHeight,
		uint32_t& outStride,
		DWORD timeoutMs = 0
	);

	/// <summary>
	/// Closes the named pipe and cleans up resources
	/// </summary>
	void Shutdown();

	/// <summary>
	/// Checks if a client is connected to the pipe
	/// </summary>
	/// <returns>True if client connected, false otherwise</returns>
	bool IsClientConnected();
}
