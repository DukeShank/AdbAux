# AdbAux

**Your Android phone's audio on your Windows PC, over a USB cable, uncompressed.**

AdbAux works like an aux cable from the phone to the PC, but over USB: music and video play
through the PC's speakers, headphones or DAC as 16-bit PCM at 48 kHz, without lossy compression.
By default, notifications, ringtones and alarms stay on the phone.

It is a small Windows program built around [scrcpy](https://github.com/Genymobile/scrcpy)
(open source), which captures the phone's audio through ADB (Android Debug Bridge).
Nothing is installed on the phone.

## Features

- Uncompressed audio (raw 16-bit PCM, 48 kHz stereo) over USB.
- **Media only** by default: alerts keep sounding on the phone. Optionally send all sounds to the PC.
- Content presets with buffer sizes measured for clean playback: Music (40 ms), Video (20 ms),
  Low latency (15 ms); each adjustable on the Advanced tab.
- Starts streaming automatically when the phone is plugged in, and resumes after a reconnect.
- Optional: pauses the phone's media when you stop streaming, and resumes it when streaming starts.
- Runs in the notification area, optionally starts with Windows, optional notifications.
- Checks for scrcpy updates and installs them from the official releases after asking, verified with SHA-256.
- Back up and restore all settings and tested profiles in one file.
- A single `AdbAux.exe` with no runtime to install (.NET Framework 4.8 is part of Windows).

## Requirements

| | |
|---|---|
| PC | Windows 10 (version 1903 or later) or Windows 11, 64-bit |
| Phone | Android 11 or later (Android 12 or later recommended), USB debugging enabled |
| Cable | A USB data cable (not a charge-only cable) |

## Setup

### Phone (one time)
1. **Settings > About phone > Software information**, then tap **Build number** 7 times.
   This unlocks *Developer options*. Menu names vary by brand.
2. **Settings > Developer options**, then turn on **USB debugging**.
3. **Samsung only:** turn off **Settings > Security and privacy > Auto Blocker** while using AdbAux.
   Auto Blocker blocks commands over USB, which includes ADB.
4. Connect the cable, unlock the phone, tick **Always allow from this computer** and tap **Allow**.

An authorised PC can control the phone over USB. Only authorise your own PC, and switch
Auto Blocker back on when you disconnect. To revoke access: **Developer options > Revoke USB debugging authorisations**.

### PC
1. Get `AdbAux.exe`: build it from source (see below) or download it from the Releases page once available.
2. Put it in a folder of its own and start it. On first start it offers to download scrcpy (about 12 MB)
   into a `scrcpy` folder next to it. If that folder cannot be written to (for example under
   `C:\Program Files`), scrcpy goes to `%LOCALAPPDATA%\AdbAux\scrcpy` instead.
3. Recommended: set your output device's Windows format to 48 kHz, so Windows does not resample the
   stream. Press Win+R, run `mmsys.cpl`, open the device's **Properties > Advanced**, choose **24 bit, 48000 Hz**.

## Using AdbAux

- **Basic tab:** choose what you are playing (Music / Video / Low latency) and what happens to
  notifications, ringtones and alarms. Start / Stop, and *Open volume mixer* (Windows' per-app sound
  settings) to pick the output device.
- **Advanced tab:** buffer size for each content type, what is selected at start-up, *Tested profiles*
  (save the values that work for each phone and audio device combination, switch between them, select one
  automatically when its phone is connected, and export or import them to share tested values), scrcpy
  options, notifications, which adb to use, automatic start, start with Windows, the volume mixer icon,
  app theme (system setting, light or dark), back up and restore of all settings, and the scrcpy folder
  and update check. Changes on this tab take effect when you click **Apply**; AdbAux says beforehand when
  applying restarts the stream.
- **Log tab:** everything scrcpy reports, for troubleshooting.
- **Tray icon:** the badge colour shows the state: grey = waiting for phone, yellow = not authorised,
  blue = connected or starting, green = streaming, red = error. Right-click for Open, Start, Stop,
  Restart streaming, Restart AdbAux and Exit.

Details: [`gui/README.md`](gui/README.md).

### Which sounds go to the PC

| Setting | scrcpy option | Media | Notifications, ringtone, alarm | Phone volume slider |
|---|---|---|---|---|
| Keep them on the phone (default) | `--audio-source=output` | PC | Phone | Controls the PC level |
| Send them to the PC too | `--audio-source=playback` | PC | PC | No effect |
| Play on both | `--audio-dup` | PC and phone | PC and phone | Not measured |

Apps that block audio capture (some DRM video apps) are expected to stay silent on the PC with the
second and third settings. Measurements: [`TESTING.md`](TESTING.md).

> **Hearing safety:** with "Send them to the PC too", the phone's volume buttons have no effect and
> audio arrives on the PC at full level. Turn the PC volume down before switching, especially with
> headphones. AdbAux asks for confirmation the first time this mode is chosen.

### Using Android Studio or another adb tool

Different adb versions on one PC keep restarting each other's background server
(`adb server version ... doesn't match this client; killing...`), which interrupts both tools.
On the Advanced tab, under **adb**, choose *Android SDK platform-tools* (found automatically) or a
specific `adb.exe`, so AdbAux and scrcpy use the same adb as your other tools.

## Building from source

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (any current version), for example
`winget install Microsoft.DotNet.SDK.10`.

```
gui\build.cmd
```
This runs `dotnet build gui\AdbAux.csproj -c Release` and copies `AdbAux.exe` to the repository folder.

```
test.cmd
```
This runs the unit tests in `tests\AdbAux.Tests` (the first run downloads the xUnit test packages from nuget.org).
GitHub Actions builds and tests every push (`.github/workflows/build.yml`).

## How it works

AdbAux runs scrcpy in audio-only mode:
```
scrcpy --serial=<phone> --no-window --require-audio --audio-codec=raw --audio-buffer=40 --audio-output-buffer=10 --audio-source=output
```
| Option | Meaning |
|---|---|
| `--serial` | The USB-connected phone, found with `adb devices -l` (devices on ADB over Wi-Fi are ignored) |
| `--no-window` | No video and no input control; audio only |
| `--require-audio` | Exit with an error if audio capture fails |
| `--audio-codec=raw` | Uncompressed 16-bit PCM |
| `--audio-buffer` | PC-side buffer that absorbs uneven arrival from the phone (set by the content preset) |
| `--audio-output-buffer` | Audio output buffer (scrcpy default 10 ms) |
| `--audio-source` / `--audio-dup` | Which sounds are captured (see the table above) |

scrcpy copies a small helper to the phone for each session; it captures the audio there and sends it
to the PC through ADB. Nothing remains installed on the phone.

## Privacy and logs

AdbAux has no telemetry and collects no data. It connects to the internet only for the following, and only
to GitHub (`api.github.com` for release information, `github.com` for downloads):

- **Automatic update check (optional).** At first start, AdbAux asks whether it may check for updates. If you
  answer Yes, it asks GitHub once a day for the latest published versions of AdbAux and scrcpy, and shows a
  notification when a newer version exists. Turn it on or off on the Advanced tab, under *General*.
- **Manual checks.** *Check for AdbAux updates* (About tab) and *Check for scrcpy updates* (Advanced tab),
  only when you click them.
- **scrcpy download.** At first start, if scrcpy is not found, AdbAux asks whether to download it, and contacts
  GitHub only if you answer OK. Updates of scrcpy are also installed only after you confirm.

Each request sends GitHub this PC's IP address and the program name and version (for example `AdbAux/0.6.0`);
nothing else. AdbAux never downloads or replaces its own exe: for a new AdbAux version, it opens the release
page. Audio travels only over the USB cable between the phone and the PC.

AdbAux keeps a log for **your own troubleshooting** in `%LOCALAPPDATA%\AdbAux\logs\adbaux.log`
(limited to about 1 MB; the previous file is kept as `adbaux.old.log`). It stays on your PC and is never
sent anywhere. It contains your phone's model and serial number, so remove them before sharing a log,
for example in a bug report. Unexpected errors are written to the same file.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| "Waiting for a phone on USB..." | USB debugging off, Auto Blocker on (Samsung), charge-only cable, or a missing USB driver (Samsung: *Samsung USB Driver for Mobile Phones*). |
| "Phone ... is 'unauthorized'" | Unlock the phone and accept the USB debugging prompt. If none appears, revoke authorisations and reconnect. |
| Clicks or short gaps | Increase the buffer for that content type on the Advanced tab. |
| A video app is silent on the PC | It blocks playback capture; use "Keep them on the phone" (`output` mode). |
| Phone calls are not heard on the PC | Call audio is a separate source in scrcpy and is not part of AdbAux. |
| Android Studio loses its device, or AdbAux keeps reconnecting | Two adb versions are fighting; see *Using Android Studio or another adb tool*. |

## Uninstalling

1. On the Advanced tab, untick **Start with Windows** and click **Apply** (removes the registry entry).
2. Right-click the tray icon and choose **Exit**.
3. Delete the folder containing `AdbAux.exe` (with its `scrcpy` folder), `%APPDATA%\AdbAux` (settings)
   and `%LOCALAPPDATA%\AdbAux` (logs, and scrcpy if it was installed there).
4. On the phone, turn off USB debugging if you no longer need it, and switch Auto Blocker back on (Samsung).

## Disclaimer

- **No warranty.** AdbAux is provided "as is", without warranty of any kind (see the [MIT License](LICENSE)).
  Use it at your own risk. It has been tested on one phone and one PC (see [`TESTING.md`](TESTING.md));
  results on other devices may differ.
- **Device security.** USB debugging gives the connected PC full control of the phone. Only authorise
  your own PC, turn USB debugging off when you do not need it, and switch protections such as Samsung's
  Auto Blocker back on when you disconnect.
- **Hearing safety.** Keep the volume at a safe level. In "Send them to the PC too" mode, audio arrives
  at full level and the phone's volume buttons have no effect.
- **Content.** AdbAux plays the phone's audio on the PC in real time; it does not record it. Use it with
  content you are entitled to play, and follow the terms of the apps and services you use. Some apps
  restrict audio capture.
- **Third-party software.** scrcpy and adb are separate projects under their own licences, downloaded
  from their official sources. AdbAux is not affiliated with their developers.
- **Trademarks.** Android and YouTube are trademarks of Google LLC. Samsung, Amazon Music, Twitch, FiiO
  and other names mentioned are trademarks of their respective owners and are used only to identify
  products. AdbAux is an independent project, not affiliated with or endorsed by any of them.

## Licence and credits

AdbAux is released under the [MIT License](LICENSE). It uses scrcpy by Genymobile (Apache License 2.0),
which is downloaded separately; see [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).
