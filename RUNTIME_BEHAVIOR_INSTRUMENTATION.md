# Runtime Behavior Instrumentation Complete

## Status

✅ **Build successful - ZERO ERRORS**

Both C++ and C# implementations have been instrumented with comprehensive runtime logging.

## Instrumentation Added

### C++ UnityCaptureSender (shared.inl)

#### Open() Method Logging
- First call detection
- Named object creation/opening for all handles:
  - Mutex: `CreateMutexA` / `OpenMutexA`
  - WantFrameEvent: `CreateEventA` / `OpenEventA`
  - SentFrameEvent: `CreateEventA` / `OpenEventA`
  - SharedFile: `CreateFileMappingA` / `OpenFileMappingA`
- Win32 API return values (handle addresses and `GetLastError()`)
- `MapViewOfFile` result and mapped buffer address
- `maxSize` initialization/verification

#### Send() Method Logging
- Call counter (#1, #2, #3, ...)
- Input parameters (width, height, stride, dataSize, format)
- Buffer pointer validity checks
- maxSize validation
- `WaitForSingleObject(mutex)` - entry and result
- Header field writes
- `memcpy()` operation with source/dest/size
- `ReleaseMutex()` result
- `SetEvent(hSentFrameEvent)` result
- `WaitForSingleObject(hWantFrameEvent, 0)` - frame skip check
- Final result (OK / WARN_FRAMESKIP)

### C# UnityCaptureOutputService.cs

#### Open() Method Logging
- First call detection (`_openFirstCall` field)
- Named object creation/opening for all handles:
  - Mutex: `CreateMutexA` / `OpenMutexA`
  - WantFrameEvent: `CreateEventA` / `OpenEventA`
  - SentFrameEvent: `CreateEventA` / `OpenEventA`
  - SharedFile: `CreateFileMappingA` / `OpenFileMappingA`
- Win32 API return values (handle addresses and `Marshal.GetLastWin32Error()`)
- `MapViewOfFile` result and mapped buffer address
- `maxSize` initialization/verification

#### Send() Method Logging
- Call counter (`_sendCallCount` field)
- Input parameters (width, height, stride, dataSize, format)
- Buffer pointer validity checks
- maxSize validation
- `WaitForSingleObject(mutex)` - entry and result
- Header field writes
- `CopyMemory()` operation with source/dest/size
- `ReleaseMutex()` result
- `SetEvent(hSentFrameEvent)` result
- `WaitForSingleObject(hWantFrameEvent, 0)` - frame skip check
- Final result (OK / WARN_FRAMESKIP)

## C++ Runtime Log Sample

Captured to `C:\Temp\cpp_runtime.log`:

```
[C++ Open] === FIRST CALL === ForReceiving=0
[C++ Open] Creating/opening shared memory objects for device 0
[C++ Open] Named objects: Mutex='UnityCapture_Mutx' Want='UnityCapture_Want' Sent='UnityCapture_Sent' Data='UnityCapture_Data'
[C++ Open] OpenMutexA('UnityCapture_Mutx')...
[C++ Open] Mutex handle: 0000000000000174 (Error=183)
[C++ Open] WaitForSingleObject(m_hMutex, INFINITE)...
[C++ Open] Mutex acquired
[C++ Open] CreateEventA('UnityCapture_Want')...
[C++ Open] WantFrameEvent handle: 0000000000000178 (Error=183)
[C++ Open] OpenEventA('UnityCapture_Sent')...
[C++ Open] SentFrameEvent handle: 000000000000017C (Error=183)
[C++ Open] OpenFileMappingA('UnityCapture_Data')...
[C++ Open] SharedFile handle: 0000000000000180 (Error=183)
[C++ Open] MapViewOfFile(m_hSharedFile, FILE_MAP_WRITE, 0, 0, 0)...
[C++ Open] Mapped buffer: 000002B4C5CD0000 (Error=183)
[C++ Open] Current maxSize: 66355200
[C++ Open] Open complete, returning true

[C++ Send #1] === BEGIN ===
[C++ Send #1] Parameters: w=1920 h=1080 stride=1920 size=8294400 fmt=0
[C++ Send #1] Checking buffer validity: buffer=000002B4C54DC070 m_pSharedBuf=000002B4C5CD0000
[C++ Send #1] Size check passed: maxSize=66355200 >= DataSize=8294400
[C++ Send #1] WaitForSingleObject(m_hMutex, INFINITE)...
[C++ Send #1] WaitForSingleObject returned: 0 (WAIT_OBJECT_0=0)
[C++ Send #1] Writing header fields...
[C++ Send #1] memcpy(000002B4C5CD0020, 000002B4C54DC070, 8294400)...
[C++ Send #1] memcpy complete
[C++ Send #1] ReleaseMutex returned: 1
[C++ Send #1] SetEvent(m_hSentFrameEvent)...
[C++ Send #1] SetEvent returned: 1
[C++ Send #1] WaitForSingleObject(m_hWantFrameEvent, 0)...
[C++ Send #1] WaitForSingleObject returned: 0 (WAIT_OBJECT_0=0, WAIT_TIMEOUT=258)
[C++ Send #1] Result: OK (DidSkipFrame=0)
[C++ Send #1] === END ===
```

**Key Observations from C++ Log:**
- Error 183 = `ERROR_ALREADY_EXISTS` - normal when opening existing shared objects
- All Win32 API calls succeed (non-zero handles, return codes indicate success)
- maxSize correctly set to 66355200
- Data pointer offset is 0x20 (32 bytes) as verified by memcpy destination
- All frames return Result: OK (not skipped)

## C# Runtime Log Capture

The C# application writes to `Debug.WriteLine()`, which goes to the Visual Studio Output window.

### To Capture C# Logs:

1. **Option A: Run from Visual Studio**
   - Open solution in Visual Studio
   - Set VirtualCamStudio as startup project
   - Run with F5 (Debug mode)
   - View → Output window
   - Select "Debug" from the "Show output from" dropdown
   - Click "Start Streaming" button in the app
   - Copy log output

2. **Option B: Use DebugView**
   - Download Sysinternals DebugView
   - Run DebugView as Administrator
   - Capture → Capture Win32
   - Run `VirtualCamStudio.exe`
   - Click "Start Streaming"
   - View logs in DebugView

3. **Option C: Console Redirect (requires code change)**
   - Add `Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));` to startup
   - Run from terminal: `.\VirtualCamStudio.exe > C:\Temp\csharp_runtime.log 2>&1`

## Comparison Checklist

When comparing C++ vs C# runtime logs, look for:

### 1. Open() Sequence
- ✅ Named objects match exactly
- ✅ Same device index (0)
- ❓ API return codes (handle values will differ, but success/failure must match)
- ❓ Error codes match pattern (183 for existing objects is normal)
- ❓ maxSize value (must be 66355200)

### 2. Send() Sequence
- ❓ All Send() calls succeed
- ❓ Parameters match (1920×1080, stride 1920, size 8294400, format 0)
- ❓ WaitForSingleObject(mutex) returns 0 (WAIT_OBJECT_0)
- ❓ memcpy/CopyMemory destination offset is 0x20 (32 bytes)
- ❓ ReleaseMutex returns success
- ❓ SetEvent returns success
- ❓ Frame skip check result

### 3. First Difference
Document the **FIRST** line where C++ and C# diverge:
- Different API return code?
- Different error value?
- Different maxSize?
- Different handle pattern?
- Different success/failure?

## Expected Output

If both implementations are equivalent, the logs should show:
1. Same API call sequence
2. Same success/failure pattern
3. Same maxSize (66355200)
4. Same data offset (32 bytes)
5. Same frame results (OK when receiver is active)

## Files Created

- ✅ `RUNTIME_BEHAVIOR_INSTRUMENTATION.md` - This file
- ✅ `RuntimeBehaviorComparison.ps1` - PowerShell comparison script
- ✅ `C:\Temp\cpp_runtime.log` - C++ runtime log (auto-generated)
- ⏳ `C:\Temp\csharp_runtime.log` - C# runtime log (user must capture from Debug output)

## Next Steps

1. Run C# application and capture Debug output
2. Compare first 50 lines of C++ vs C# logs
3. Identify first divergence
4. Document the exact Win32 API call or return code that differs
5. That difference is the root cause of the UnityCapture diagnostic screen issue

---

**Instrumentation complete. Ready for side-by-side runtime comparison.**
