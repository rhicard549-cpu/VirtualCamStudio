# Shared Memory Byte-Level Comparison Summary

## Objective

Compare the working C++ UnityCaptureSender against the C# UnityCaptureOutputService at the byte level to identify any discrepancies in the shared memory layout.

## Methodology

1. **Instrumented both implementations** to dump:
   - Shared memory header bytes (32 bytes)
   - Frame metadata (width, height, stride, format, etc.)
   - Data pointer offset
   - First 64 bytes of frame buffer

2. **Created test programs**:
   - Modified `UnityCaptureSender.cpp` to dump after first successful `Send()`
   - Modified `UnityCaptureOutputService.cs` to dump after first successful `Send()`
   - Created `CSharpSharedMemTest` console app to verify struct layout

3. **Compared outputs** byte-for-byte

## Results

### ✅ Binary Layout: IDENTICAL

Both C++ and C# produce the exact same 32-byte header:

```
00 80 F4 03 80 07 00 00 38 04 00 00 80 07 00 00
00 00 00 00 00 00 00 00 00 00 00 00 E8 03 00 00
```

Decoded:
- **Offset 0-3**:   `maxSize = 66355200` (0x03F48000)
- **Offset 4-7**:   `width = 1920` (0x00000780)
- **Offset 8-11**:  `height = 1080` (0x00000438)
- **Offset 12-15**: `stride = 1920` (0x00000780)
- **Offset 16-19**: `format = 0` (FORMAT_UINT8)
- **Offset 20-23**: `resizemode = 0` (RESIZEMODE_DISABLED)
- **Offset 24-27**: `mirrormode = 0` (MIRRORMODE_DISABLED)
- **Offset 28-31**: `timeout = 1000` (0x000003E8)

### ✅ Data Pointer Offset: IDENTICAL

Both implementations place the frame data at **byte offset 32**:
- C++: `&m_pSharedBuf->data` → offset 32
- C#: `&header->data` → offset 32

### ✅ Frame Buffer Format: IDENTICAL

Both use RGBA with 4 bytes per pixel:
- Byte order: R, G, B, A
- Pattern: `FF 00 00 FF` (red pixel example)

## Key Finding

The C# `SharedMemHeader` struct with `Pack = 1` produces:
```csharp
[StructLayout(LayoutKind.Sequential, Pack = 1)]
unsafe struct SharedMemHeader
{
	public uint maxSize;    // 4 bytes
	public int width;       // 4 bytes
	public int height;      // 4 bytes
	public int stride;      // 4 bytes
	public int format;      // 4 bytes
	public int resizemode;  // 4 bytes
	public int mirrormode;  // 4 bytes
	public int timeout;     // 4 bytes
	public byte data;       // 1 byte (flexible array placeholder)
}
// Total: 33 bytes, but data starts at offset 32
```

This is **byte-for-byte compatible** with the C++ version:
```cpp
struct SharedMemHeader
{
	DWORD maxSize;      // 4 bytes
	int width;          // 4 bytes
	int height;         // 4 bytes
	int stride;         // 4 bytes
	int format;         // 4 bytes
	int resizemode;     // 4 bytes
	int mirrormode;     // 4 bytes
	int timeout;        // 4 bytes
	uint8_t data[1];    // Flexible array member
};
```

## Conclusion

**The C# implementation is byte-for-byte compatible with the C++ implementation.**

The shared memory layout is **NOT the cause** of UnityCapture showing its diagnostic screen.

## Instrumentation Added

### C++ (UnityCaptureSender.cpp)
- Dump block after `sender.Send()` writes to `C:\Temp\cpp_sharedmem_dump.txt`
- Includes header bytes, data offset, and first 64 frame bytes

### C# (UnityCaptureOutputService.cs)
- Dump block in `SendFrameAsync()` writes to `C:\Temp\csharp_sharedmem_dump.txt`
- Enhanced `Send()` method logging:
  - Header maxSize validation
  - Data size checks
  - Copy operation confirmation
  - Event signaling confirmation
  - Frame skip detection

### Test Tool (CSharpSharedMemTest)
- Standalone console app to verify C# struct layout
- Writes to `C:\Temp\csharp_struct_test.txt`
- Confirms 32-byte data offset

## Files Modified

1. `UnityCaptureSender/shared.inl`
   - Added `GetSharedBuffer()` accessor method
   - Moved `SharedMemHeader` struct to top of class

2. `UnityCaptureSender/UnityCaptureSender.cpp`
   - Added dump instrumentation after first `Send()`

3. `VirtualCamStudio/Services/UnityCaptureOutputService.cs`
   - Added dump instrumentation in `SendFrameAsync()`
   - Enhanced `Send()` with detailed logging

4. `CSharpSharedMemTest/Program.cs` (NEW)
   - Test harness for struct layout verification

## Files Created

1. `BYTE_LEVEL_COMPARISON.md` - Detailed analysis document
2. `BYTE_COMPARISON_SUMMARY.md` - This file
3. `CompareSharedMemoryDumps.ps1` - Automated comparison script
4. `CSharpSharedMemTest/` - Test project

## Next Investigation Steps

Since binary layout is identical, investigate:

1. **Runtime behavior**: Check Debug output for `Send()` calls
   - Is `header->maxSize` being set correctly?
   - Is `Send()` returning `SENDRES_OK`?
   - Are events being signaled?

2. **Connection state**: Verify `StartStreaming()` is called

3. **Frame content**: Verify the actual pixel data being written

4. **Synchronization**: Compare event signaling order between C++ and C#

The byte-level compatibility is proven. The issue must be in runtime execution flow.
