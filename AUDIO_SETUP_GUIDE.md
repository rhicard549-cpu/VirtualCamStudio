# VirtualCam Studio - Audio Setup Guide

## 🎵 Audio to CloudPhone Setup

VirtualCam Studio now supports feeding audio to CloudPhone's microphone input. This guide explains how to configure your system for audio routing.

---

## 📋 How It Works

### The Audio Chain:
```
Studio Audio File → Selected Audio Device → Windows System → CloudPhone Microphone
```

**Key Concept**: CloudPhone automatically uses **Windows' default microphone/recording device**. You need to route Studio's audio output to appear as a microphone input.

---

## ⚙️ Setup Methods

### **Method 1: Windows Stereo Mix** (Built-in, No Extra Software)

#### Step 1: Enable Stereo Mix
1. **Right-click** the speaker icon in Windows taskbar
2. Select **"Sounds"** or **"Open Sound Settings"**
3. Click **"Sound Control Panel"** (or go to Recording tab)
4. **Right-click** in the empty space → **"Show Disabled Devices"**
5. Find **"Stereo Mix"** or **"What U Hear"**
6. **Right-click** → **"Enable"**

#### Step 2: Set as Default Recording Device
1. **Right-click** on **"Stereo Mix"**
2. Select **"Set as Default Device"**
3. Click **"OK"**

#### Step 3: Configure Studio
1. In VirtualCam Studio, select your **speakers/headphones** in the Audio device dropdown
2. Load and play your audio file
3. CloudPhone will now hear the audio as microphone input! ✅

**Pros**: Built into Windows, no installation needed  
**Cons**: Not all audio drivers support Stereo Mix

---

### **Method 2: Virtual Audio Cable** (More Reliable)

#### Step 1: Install Virtual Audio Cable
1. Download [VB-Audio Virtual Cable](https://vb-audio.com/Cable/) (FREE)
2. Install and restart your computer

#### Step 2: Configure Windows Audio
1. Open **Windows Sound Settings**
2. **Playback** tab: Set **"CABLE Input"** as default
3. **Recording** tab: Set **"CABLE Output"** as default microphone

#### Step 3: Configure Studio
1. In VirtualCam Studio Audio dropdown, select **"CABLE Input (VB-Audio Virtual Cable)"**
2. Load and play your audio file
3. CloudPhone will pick up audio through CABLE Output! ✅

**Pros**: Works reliably on all systems, more control  
**Cons**: Requires additional software

---

### **Method 3: VoiceMeeter** (Professional, Advanced)

#### For Advanced Users:
1. Download [VoiceMeeter](https://vb-audio.com/Voicemeeter/) (FREE)
2. Route Studio output → VoiceMeeter input
3. Route VoiceMeeter Virtual Output → Windows default mic
4. Gives you mixing capabilities and volume control

**Pros**: Professional audio routing, mixing capabilities  
**Cons**: More complex setup, learning curve

---

## 🎮 Using the Studio Audio Controls

### Audio Device Dropdown
- Shows all available audio output devices
- **"Default Audio Device"**: Uses Windows default speakers
- **Hardware devices**: Your physical sound cards
- **Virtual devices**: Virtual Audio Cables, VoiceMeeter, etc.

💡 **Tip**: For CloudPhone, select a device that's routed to Windows' default microphone!

### Control Buttons

| Button | Function |
|--------|----------|
| **📂 Load** | Open file dialog to select audio file (MP3, WAV, M4A, AAC, OGG, FLAC) |
| **▶ Play** | Start or resume audio playback |
| **⏸ Pause** | Pause audio playback (maintains position) |
| **⏹ Stop** | Stop playback and reset to beginning |
| **☑ Loop** | Enable continuous audio looping |

---

## 🔍 Troubleshooting

### CloudPhone Not Hearing Audio?

**Check 1**: Windows Default Microphone
- Open **Sound Settings** → **Recording**
- Verify your virtual audio device is set as **Default**

**Check 2**: Studio Device Selection
- Make sure Studio's audio dropdown matches your routing strategy
- For Stereo Mix: Select your actual speakers
- For Virtual Cable: Select "CABLE Input"

**Check 3**: Volume Levels
- Ensure Studio audio isn't muted
- Check Windows volume mixer
- Verify microphone input isn't muted in CloudPhone

**Check 4**: Test Your Setup
1. Play audio in Studio
2. Open **Windows Sound Settings** → **Recording**
3. You should see the microphone level meter moving
4. If meter moves = CloudPhone can hear it! ✅

### Audio File Won't Load?

**Supported formats**: MP3, WAV, M4A, AAC, OGG, FLAC

If file won't load:
- Verify the file isn't corrupted
- Try converting to MP3 or WAV
- Check file isn't locked by another program

---

## 📌 Quick Reference Card

### Recommended Setup for CloudPhone:

1. **Install**: VB-Audio Virtual Cable
2. **Windows Recording**: Set "CABLE Output" as default mic
3. **Studio Audio Dropdown**: Select "CABLE Input"
4. **Load & Play**: Your audio file
5. **Result**: CloudPhone hears audio as microphone! 🎉

---

## 🆘 Need More Help?

- VB-Audio Virtual Cable: https://vb-audio.com/Cable/
- VoiceMeeter: https://vb-audio.com/Voicemeeter/
- Windows Audio Settings: Search "Sound Settings" in Start Menu

---

**Note**: Audio routing happens at the system level. VirtualCam Studio outputs audio to your selected device—Windows handles routing it to CloudPhone's microphone input.
