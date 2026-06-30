# Byte-Level Comparison: C++ vs C# SharedMemHeader

## Executive Summary

**RESULT**: The C# and C++ shared memory header layouts are **BYTE-FOR-BYTE IDENTICAL**.

**Key Finding**: Both implementations place the frame data at **offset 32 bytes** from the header start.

## Detailed Analysis

### Header Structure (32 bytes)

Both C++ and C# produce identical header bytes:

```
Offset | C++ Bytes                                      | C# Bytes                                       | Field
-------|-----------------------------------------------|-----------------------------------------------|------------------
0-3    | 00 80 F4 03                                   | 00 80 F4 03                                   | maxSize (66355200)
4-7    | 80 07 00 00                                   | 80 07 00 00                                   | width (1920)
8-11   | 38 04 00 00                                   | 38 04 00 00                                   | height (1080)
12-15  | 80 07 00 00                                   | 80 07 00 00                                   | stride (1920)
16-19  | 00 00 00 00                                   | 00 00 00 00                                   | format (0)
20-23  | 00 00 00 00                                   | 00 00 00 00                                   | resizemode (0)
24-27  | 00 00 00 00                                   | 00 00 00 00                                   | mirrormode (0)
28-31  | E8 03 00 00                                   | E8 03 00 00                                   | timeout (1000)
32+    | FF 00 00 FF... (frame data)                   | 00 (data field, frame copied separately)     | data[1]
```

### Data Pointer Offset

- **C++**: `data` field is at offset **32 bytes** from header start
- **C#**: `data` field is at offset **32 bytes** from header start

✅ **IDENTICAL**

### Frame Buffer Layout

Both implementations use RGBA format with 4 bytes per pixel:
- Byte order: R, G, B, A
- Test frame: Red pixels (R=255, G=0, B=0, A=255)
- Hex pattern: `FF 00 00 FF FF 00 00 FF ...`

✅ **IDENTICAL**

### Difference in Bytes 32-35

The apparent difference at offset 32 is expected:

- **C++ dump**: Shows `FF 00 00 FF` because it dumps **after** `memcpy(m_pSharedBuf->data, buffer, DataSize)`
  - This is the first pixel of the actual frame data in shared memory

- **C# test**: Shows `00 00 00 00` because it's a stack-allocated struct **before** frame data is copied
  - The `data` field is just a placeholder byte in the struct definition
  - In actual usage, `Send()` uses `&header->data` as the target for `Marshal.Copy()`

## Conclusion

The C# `UnityCaptureOutputService` implementation is **correctly aligned** with the C++ `SharedImageMemory` protocol at the byte level.

### Verified Identical Aspects:
1. ✅ Header size: 32 bytes (8 int fields × 4 bytes)
2. ✅ Field order and values match exactly
3. ✅ Data pointer offset: 32 bytes
4. ✅ Frame buffer format: RGBA, 4 bytes per pixel
5. ✅ Endianness: Little-endian (Windows standard)

### What the Dumps Confirm:

The C++ sender writes:
```cpp
memcpy(m_pSharedBuf->data, buffer, DataSize);  // Offset 32
```

The C# sender writes:
```csharp
byte* dataPtr = &header->data;                  // Offset 32
Marshal.Copy(frameData, 0, (IntPtr)dataPtr, (int)dataSize);
```

Both write frame data to the **exact same offset (32 bytes)** in shared memory.

## Next Steps

Since the binary layout is identical, the issue with UnityCapture showing its diagnostic screen must be caused by:

1. **Synchronization timing**: Event signaling order or mutex handling
2. **Frame size validation**: maxSize field not being set correctly in the actual running C# app
3. **Connection state**: The C# app may not be calling `StartStreaming()` automatically
4. **Data content**: The actual frame data being written might differ (wrong pixel format, stride, etc.)

**Recommendation**: Add runtime logging to the C# `Send()` method to verify:
- `header->maxSize` value before the size check
- The actual `dataSize` being passed
- The return value of `Send()`
- Whether `SetEvent(_hSentFrameEvent)` is being called

The byte-level structure is proven correct. The problem lies in runtime behavior, not memory layout.
