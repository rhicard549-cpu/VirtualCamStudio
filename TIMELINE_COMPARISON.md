# Runtime Behavior Timeline - Side-by-Side Comparison

## Objective

Compare the exact runtime sequence between the working C++ UnityCaptureSender and the C# UnityCaptureOutputService to identify the first behavioral difference.

## Instrumentation Complete

Both implementations now log:

1. **Every Win32 API call** with return values
2. **Exact operation order**: Open → Send → Mutex → Copy → Signal
3. **All handle values and error codes**
4. **Complete memory operations**

## C++ Runtime Sequence (VERIFIED WORKING)

### Open() - First Call
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
```

**Key Points:**
- Error 183 = ERROR_ALREADY_EXISTS (normal for sender opening existing receiver objects)
- All handles are non-zero (success)
- maxSize = 66355200 ✅
- Buffer mapped at 0x000002B4C5CD0000

### Send() - First Call
```
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

**Key Points:**
- memcpy destination: 0x000002B4C5CD0020 (buffer + 0x20 = buffer + 32 bytes) ✅
- WaitForSingleObject(mutex) returns 0 (WAIT_OBJECT_0) = immediate lock
- ReleaseMutex returns 1 = success
- SetEvent returns 1 = success
- WaitForSingleObject(WantFrameEvent, 0) returns 0 = receiver is waiting (frame consumed)
- Result: OK (frame successfully sent and consumed)

## C# Runtime Sequence (TO BE CAPTURED)

### Expected Format

```
[C# Open] === FIRST CALL === forReceiving=False
[C# Open] Creating/opening shared memory objects for device 0
[C# Open] Named objects: Mutex='UnityCapture_Mutx' Want='UnityCapture_Want' Sent='UnityCapture_Sent' Data='UnityCapture_Data'

[C# Open] OpenMutexA('UnityCapture_Mutx')...
[C# Open] Mutex handle: 0x??? (Error=???)

... (rest of Open sequence)

[C# Send #1] === BEGIN ===
[C# Send #1] Parameters: w=1920 h=1080 stride=1920 size=8294400 fmt=0

... (rest of Send sequence)
```

## Comparison Checklist

### Open() Comparison

| Operation | C++ Value | C# Value | Match? |
|-----------|-----------|----------|--------|
| Device index | 0 | ? | ? |
| Mutex name | `UnityCapture_Mutx` | ? | ? |
| Want event name | `UnityCapture_Want` | ? | ? |
| Sent event name | `UnityCapture_Sent` | ? | ? |
| Data mapping name | `UnityCapture_Data` | ? | ? |
| **OpenMutexA result** | Non-zero handle | ? | ? |
| **Error code** | 183 (normal) | ? | ? |
| **CreateEventA (Want) result** | Non-zero handle | ? | ? |
| **OpenEventA (Sent) result** | Non-zero handle | ? | ? |
| **OpenFileMappingA result** | Non-zero handle | ? | ? |
| **MapViewOfFile result** | Non-zero pointer | ? | ? |
| **maxSize value** | 66355200 | ? | ? |

### Send() Comparison

| Operation | C++ Value | C# Value | Match? |
|-----------|-----------|----------|--------|
| Width | 1920 | ? | ? |
| Height | 1080 | ? | ? |
| Stride | 1920 | ? | ? |
| DataSize | 8294400 | ? | ? |
| Format | 0 | ? | ? |
| **Buffer pointer** | Non-null | ? | ? |
| **Shared buffer pointer** | Non-null | ? | ? |
| **maxSize >= DataSize** | Yes | ? | ? |
| **WaitForSingleObject(mutex)** | 0 (success) | ? | ? |
| **memcpy/CopyMemory destination** | buffer + 0x20 | ? | ? |
| **ReleaseMutex result** | 1 (success) | ? | ? |
| **SetEvent result** | 1 (success) | ? | ? |
| **WaitForSingleObject(WantEvent, 0)** | 0 (consumed) | ? | ? |
| **Result** | OK | ? | ? |

## Critical Questions to Answer

1. **Does C# Open() succeed?**
   - Are all handles non-zero?
   - What are the error codes?
   - Is maxSize set to 66355200?

2. **Does C# Send() get called?**
   - Is streaming enabled?
   - Are frames being pushed to the output service?

3. **If Send() is called, does it complete?**
   - Does size check pass?
   - Does mutex lock succeed?
   - Does CopyMemory execute?
   - Does SetEvent succeed?

4. **What is the FIRST difference?**
   - First Win32 API that returns different value?
   - First operation that fails in C# but succeeds in C++?
   - First field value that doesn't match?

## How to Capture C# Log

### Method 1: Visual Studio Output Window (Recommended)

1. Open `VirtualCamStudio.sln` in Visual Studio
2. Set `VirtualCamStudio` as startup project
3. Press F5 to run in Debug mode
4. View → Output window (Ctrl+Alt+O)
5. Select "Debug" from "Show output from:" dropdown
6. Wait for app to load
7. Click "Start Streaming" button in the UI
8. Copy all output from the Output window
9. Save to `C:\Temp\csharp_runtime.log`

### Method 2: DebugView (Alternative)

1. Download Sysinternals DebugView
2. Run DebugView as Administrator
3. Capture → Capture Win32
4. Run `VirtualCamStudio.exe`
5. Click "Start Streaming"
6. Save log from DebugView

## Analysis Instructions

1. **Open both logs side by side**
   - C++: `C:\Temp\cpp_runtime.log`
   - C#: `C:\Temp\csharp_runtime.log`

2. **Start from the beginning**
   - Compare line 1 (FIRST CALL)
   - Compare named objects
   - Compare API calls in order

3. **Mark the first difference**
   - Different handle value? (Different values OK, zero vs non-zero is critical)
   - Different error code? (Different codes may indicate issue)
   - Different maxSize? (Must be 66355200)
   - Operation that doesn't appear? (Indicates early failure)

4. **Document the root cause**
   - What is the first operation that fails in C# but succeeds in C++?
   - What Win32 error code does it return?
   - What does that error code mean?

## Expected Outcome

This instrumentation will reveal:
- Whether C# Open() succeeds or fails
- Which specific Win32 API call behaves differently
- The exact error code that indicates the root cause
- Whether the issue is in initialization or runtime sending

---

**Timeline comparison ready. Waiting for C# runtime log to complete analysis.**
