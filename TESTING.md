# Test results: phone audio over USB with scrcpy

**Test date:** 2026-11-23
**Phone:** Samsung Galaxy A36 (SM-A366E), Android 16, Auto Blocker off, USB debugging on
**PC:** Windows 11 Pro desktop, output through FiiO USB DAC set to 48000 Hz (Windows shared mode, not ASIO)
**scrcpy:** 5.0.1 (official win64 release, SHA-256 verified), raw 16-bit PCM, 48 kHz stereo, audio only

## Goal

Media (music, video) plays on the PC, while notifications, ringtones and alarms stay on the phone.

**Result: achieved with media-only capture (`--audio-source=output`), the default.**

## Results by mode

| | `MediaOnly` (`--audio-source=output`) | `Everything` (`--audio-source=playback`) | `Duplicate` (`--audio-dup`) |
|---|---|---|---|
| Music / video | PC | PC | PC and phone |
| Notification, ringtone, alarm | **Phone** | **PC** | PC and phone |
| Phone media volume slider | Controls the PC playback level | No effect | not measured |
| Lowest clean buffer, Amazon Music HD/Ultra HD | 40 ms | 36 ms | not measured |
| Apps that block capture (DRM video apps) | Expected: still forwarded | Expected: silent on PC | Expected: silent on PC |

The last row is based on scrcpy's documentation ("playback: Android apps can opt-out"), not on a test:
no capture-blocking app was installed on the phone.

Interpretation:
- `output` captures through Android's remote-submix device. Android's audio policy keeps alert
  sounds (ringtone, notification, alarm) on the phone's own speaker, so only media is forwarded,
  despite scrcpy's help text saying "forwards the whole audio output".
- `playback` in scrcpy 5.x captures every sound type, and takes the audio before the phone's volume
  setting is applied, so the PC receives a fixed level.

## Buffer (`--audio-buffer`) results

| Buffer | Samples at 48 kHz | YouTube / Twitch | Amazon Music HD |
|---|---|---|---|
| 40 ms | 1,920 | clean | clean (`MediaOnly`) |
| 36 ms | 1,728 | clean | clean (`Everything` only) |
| 30 ms | 1,440 | clean | some clicks |
| 20 ms | 960 | clean | more clicks |
| 15 ms | 720 | barely noticeable clicks | not tested |
| 10 ms / 5 ms | 480 / 240 | noticeable clicks | not tested |

Likely reason for the difference: music apps often hand audio to Android in larger, less frequent
chunks ("deep buffer" or "offload" playback), so it reaches scrcpy in bursts and needs a larger buffer.

Presets chosen from these results (AdbAux's content types; the console script used for the tests has since been removed):
| Preset | Buffer | For |
|---|---|---|
| `Music` (default) | 40 ms | Amazon Music and other music apps; safe in both main modes |
| `Video` | 20 ms | YouTube, Twitch: lowest lip-sync delay without clicks |
| `LowLatency` | 15 ms | experiments; occasional clicks accepted |

## Other findings

- On Windows, `adb devices -l` prints no `usb:` field; AdbAux identifies USB devices as those
  whose ID is not a network address. A TV connected to adb over Wi-Fi was correctly ignored.
- The FiiO's ASIO buffer (256 samples) does not apply: scrcpy plays through SDL3 and Windows'
  shared audio engine. Setting the DAC's Windows default format to 48000 Hz avoids resampling of
  scrcpy's 48 kHz stream.
- scrcpy 5.0.1's `--audio-dup` is an on/off switch; it accepts no sound-type values.

## How to repeat a test

1. In AdbAux, set the buffer on the Advanced tab (click Apply), then choose the content type and
   the alert handling on the Basic tab.
2. Play music, then trigger a notification and a ringtone preview
   (Settings > Sounds and vibration > Notification sound / Ringtone, tap a tone).
   Note where each is heard: PC, Phone, Both or None.
3. For delay, watch a video on the phone screen while listening to the PC (lip-sync gap).
   For stability, listen for clicks over 1–2 minutes.
4. Stop with Ctrl+C, then `Y` at `Terminate batch job (Y/N)?`.

## GUI test (PhoneAudio.exe 0.1.0)

Built with .NET SDK 10.0.404 (target .NET Framework 4.8, 32-bit), no warnings. All checks passed on 2026-11-23:

| # | Check | Result |
|---|---|---|
| 1 | Auto-start on launch; music to PC, notification preview stays on phone | Pass |
| 2 | Switch to Video: restart, status shows 20 ms | Pass |
| 3 | Switch to "send alerts to PC": notification preview plays on PC | Pass |
| 4 | Advanced: change Music buffer, Save, applied on Basic | Pass |
| 5 | Restore tested defaults + Save | Pass |
| 6 | Close window: keeps running in tray, balloon shown | Pass |
| 7 | Unplug and replug USB: stream stops and resumes automatically | Pass |
| 8 | Tray > Exit: program and audio stop | Pass |

## GUI test (AdbAux 0.3.0)

64-bit build (`net48 win-x64`), no warnings. Checked on 2026-11-23 from the log file:

| Check | Result |
|---|---|
| Start-up: log notice, log file path, scrcpy found, adb in use shown | Pass |
| Switch adb to Android SDK platform-tools and back (restart, streaming continues) | Pass |
| Modes: media only, all sounds (`playback`), both (`--audio-dup`; scrcpy confirms the switch to `playback`) | Pass; "both" plays all sounds on PC and phone |
| Every stop made by AdbAux is labelled with its reason in the log | Pass |
| About tab: version, disclaimer, scrcpy credit | Pass |
