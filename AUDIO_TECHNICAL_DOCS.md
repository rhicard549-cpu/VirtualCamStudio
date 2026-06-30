# Audio Device Selection - Technical Overview

## 🔧 What Was Added

### 1. AudioDevice Class
```csharp
public class AudioDevice
{
	public int DeviceNumber { get; set; }  // -1 = default, 0+ = specific device
	public string Name { get; set; }
	public string FriendlyName { get; }
}
```

### 2. AudioPlayerService Updates
- `GetAvailableDevices()`: Enumerates all WaveOut devices on the system
- `SetOutputDevice(int deviceNumber)`: Changes output device (can switch while playing)
- Automatic device re-initialization when switching devices
- Preserves playback position when changing devices

### 3. UI Updates
- **AudioDeviceComboBox**: Dropdown showing all available audio devices
- **PopulateAudioDevices()**: Loads devices on startup
- **AudioDeviceComboBox_SelectionChanged**: Handles device switching

---

## 🎯 Audio Routing Architecture

### Standard Output (Default):
```
VirtualCam Studio
	↓
AudioPlayerService (NAudio)
	↓
Selected Audio Device (e.g., Speakers)
	↓
Your Speakers/Headphones 🔊
```

### CloudPhone Routing (With Virtual Cable):
```
VirtualCam Studio
	↓
AudioPlayerService (NAudio)
	↓
Selected: "CABLE Input" (VB-Audio)
	↓
Windows Audio Routing
	↓
CABLE Output (appears as microphone)
	↓
Windows Default Microphone
	↓
CloudPhone (Android emulator) 🎤
```

### CloudPhone Routing (With Stereo Mix):
```
VirtualCam Studio
	↓
AudioPlayerService (NAudio)
	↓
Selected: Your Speakers
	↓
Windows Audio Output
	↓
Stereo Mix (captures output as input)
	↓
Windows Default Microphone
	↓
CloudPhone (Android emulator) 🎤
```

---

## 📝 Device Selection Logic

### Device Numbers in NAudio/WaveOut:
- **-1**: System default audio device (default behavior)
- **0, 1, 2...**: Physical/virtual audio devices in enumeration order

### Enumeration Order:
1. Physical sound cards (Realtek, etc.)
2. USB audio devices
3. Virtual audio cables
4. HDMI audio outputs
5. Other virtual devices

---

## 🎮 User Workflow

### First Time Setup:
1. User opens VirtualCam Studio
2. `Window_Loaded` → `PopulateAudioDevices()` runs
3. AudioDeviceComboBox fills with available devices
4. Default device is pre-selected

### Changing Devices:
1. User clicks AudioDeviceComboBox dropdown
2. Sees list: "Default Audio Device", "Speakers", "CABLE Input", etc.
3. Selects device (e.g., "CABLE Input")
4. `AudioDeviceComboBox_SelectionChanged` fires
5. `_audioPlayer.SetOutputDevice(deviceNumber)` called
6. If audio is playing, it seamlessly switches to new device

### Loading & Playing Audio:
1. User clicks "📂 Load"
2. Selects audio file (MP3, WAV, etc.)
3. `AudioPlayerService.LoadAudio()` creates WaveOutEvent with selected device
4. User clicks "▶ Play"
5. Audio plays through selected device
6. If device is routed to Windows mic → CloudPhone hears it

---

## 🛠️ Code Flow

### Initialization:
```
MainWindow.Window_Loaded()
	→ PopulateAudioDevices()
		→ AudioPlayerService.GetAvailableDevices()
			→ WaveOut.DeviceCount (NAudio)
			→ WaveOut.GetCapabilities(i) for each device
		→ AudioDeviceComboBox.ItemsSource = devices
		→ AudioDeviceComboBox.SelectedIndex = 0 (default)
```

### Device Change:
```
User clicks AudioDeviceComboBox
	→ AudioDeviceComboBox_SelectionChanged()
		→ _audioPlayer.SetOutputDevice(deviceNumber)
			→ _selectedDeviceNumber = deviceNumber
			→ IF audio loaded:
				→ Remember: wasPlaying, position
				→ LoadAudio(_currentFilePath) // Recreates with new device
				→ Restore: position
				→ IF wasPlaying: Play()
```

### Audio Playback:
```
LoadAudioButton_Click()
	→ OpenFileDialog
	→ _audioPlayer.LoadAudio(filePath)
		→ Create AudioFileReader (NAudio)
		→ Create WaveOutEvent
		→ Set WaveOutEvent.DeviceNumber = _selectedDeviceNumber
		→ WaveOutEvent.Init(audioFile)
	→ Enable playback controls

PlayAudioButton_Click()
	→ _audioPlayer.Play()
		→ WaveOutEvent.Play()
			→ NAudio → Windows Audio API
			→ Selected Device receives audio stream
```

---

## 🔍 Why This Works for CloudPhone

### The Problem:
- CloudPhone uses **Windows' default microphone**
- No UI in CloudPhone to select audio input device
- Need to route Studio's audio output → Windows microphone input

### The Solution:
1. **Virtual Audio Cable** creates a virtual loopback device
2. Studio outputs to "CABLE Input" (virtual speaker)
3. "CABLE Output" appears as a microphone
4. Set "CABLE Output" as Windows default mic
5. CloudPhone automatically uses Windows default mic
6. **Result**: CloudPhone hears Studio's audio! 🎉

### Why Device Selection Matters:
- User needs to tell Studio: "Output to CABLE Input"
- Without selection dropdown, Studio would use default speakers
- Default speakers don't route to CloudPhone
- Device dropdown lets user choose CABLE Input explicitly

---

## 🎨 UI Layout

### Toolbar Audio Section:
```
[Audio: ▼] [Select Device Dropdown] [📂 Load] [▶] [⏸] [⏹] [☑ Loop]
   Label        Device Selection      Controls      Playback    Loop
```

### Device Dropdown Shows:
```
╔═══════════════════════════════════╗
║ Default Audio Device              ║  ← Windows default
║ Speakers (Realtek High Def...)    ║  ← Physical device
║ CABLE Input (VB-Audio Virtual...) ║  ← Virtual cable ✅
║ HDMI Audio                         ║  ← HDMI output
╚═══════════════════════════════════╝
```

**Tooltip**: "Select audio output device (must be set to Windows default mic for CloudPhone)"

---

## ✅ Testing Checklist

### Verify Device Enumeration:
- [ ] Dropdown shows "Default Audio Device"
- [ ] Dropdown shows physical sound cards
- [ ] Dropdown shows virtual audio cables (if installed)
- [ ] Default device is pre-selected on startup

### Verify Device Switching:
- [ ] Load an audio file
- [ ] Start playback
- [ ] Switch device dropdown
- [ ] Audio continues on new device without interruption

### Verify CloudPhone Integration:
- [ ] Install VB-Audio Virtual Cable
- [ ] Set CABLE Output as Windows default mic
- [ ] Select CABLE Input in Studio dropdown
- [ ] Play audio in Studio
- [ ] Verify CloudPhone hears audio (e.g., test in voice recorder app)

---

## 📚 References

- **NAudio Documentation**: https://github.com/naudio/NAudio
- **WaveOut Class**: https://github.com/naudio/NAudio/wiki/WaveOut
- **VB-Audio Virtual Cable**: https://vb-audio.com/Cable/
- **Windows Audio API**: https://docs.microsoft.com/en-us/windows/win32/coreaudio/
