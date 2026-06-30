# Format Test Plan

## The Purple Mystery

We're sending what we believe is RGBA `(255,0,0,255)` for red, but seeing purple.

Purple = Red + Blue, which suggests channels are being misinterpreted.

## DirectShow ARGB32 Format

The filter uses `MEDIASUBTYPE_ARGB32`. In DirectShow, this typically means:
- **Byte order in memory**: B, G, R, A (little-endian)
- **Logical format name**: ARGB (big-endian uint32 interpretation)

This is confusing because:
- Memory bytes: `[B][G][R][A]`
- As uint32: `0xAARRGGBB`

So if we want red:
- Memory should be: `[0, 0, 255, 255]` = BGRA bytes
- As uint32: `0xFF0000FF` = ARGB value

## Current State

We're converting:
1. BGR `[0,0,255]` (red in OpenCV)
2. → BGRA `[0,0,255,255]` (red with alpha)
3. → RGBA `[255,0,0,255]` (swapped to RGBA)

And sending bytes `[255,0,0,255]` which UnityCapture reads...

**UnityCapture's RGBA8toBGRA8 expects:**
- Input bytes: `[R][G][B][A]` = `[255][0][0][255]` for red
- Converts to: `[B][G][R][A]` = `[0][0][255][255]` for output

**But DirectShow ARGB32 expects:**
- Memory bytes: `[B][G][R][A]` directly!

**THE BUG:** We're converting BGRA→RGBA, then UnityCapture converts RGBA→BGRA, ending up with... the original BGRA! But DirectShow expects BGRA in memory for ARGB32!

**So why purple?**

If we send RGBA `[255,0,0,255]`:
- UnityCapture converts to BGRA: `[0,0,255,255]`
- DirectShow reads as ARGB32 (bytes B,G,R,A): Blue=0, Green=0, Red=255 ✓
- Should show RED, not purple!

Unless... **the conversion isn't happening!** Or there's a path that bypasses it!

## Next Step

Check if there's a resize/mirror mode that bypasses the RGBA→BGRA conversion in UnityCapture!

The sender calls:
```cpp
SharedImageMemory::RESIZEMODE_DISABLED,
SharedImageMemory::MIRRORMODE_DISABLED,
```

Let me check if DISABLED modes skip the conversion...
