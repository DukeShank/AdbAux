# AdbAux (Windows program)

Source of `AdbAux.exe`: C# WinForms, .NET Framework 4.8 (built into Windows 10/11), 64-bit.
The result is a single exe with no extra DLLs. It uses the `scrcpy` folder next to it.

## Building

Requires the .NET SDK (any current version; it can build for .NET Framework 4.8).

1. Check whether an SDK is installed:
   ```
   dotnet --list-sdks
   ```
   An empty result or an error means none is installed. Install one with
   `winget install Microsoft.DotNet.SDK.10`, or with the Visual Studio Installer's ".NET desktop development" workload.
2. Run `build.cmd` in this folder. It runs `dotnet build AdbAux.csproj -c Release` and copies
   the result to `..\AdbAux.exe`, next to the `scrcpy` folder.
   The first build downloads .NET Framework reference files from nuget.org (once).

## Tabs

**Basic** (changes apply at once; a running stream restarts, with a gap of about one second)
- *What are you playing?* Music / Video / Low latency: each uses its own buffer size.
- *Notifications, ringtones and alarms*: keep them on the phone (scrcpy `--audio-source=output`),
  send them to the PC too (`--audio-source=playback`), or play on both (`--audio-dup`).
  The first time one of the last two is chosen, AdbAux shows a volume warning: in those modes the
  phone's volume buttons have no effect and audio arrives at full level.
- Start / Stop, *Open volume mixer* (Windows' per-app sound settings, where scrcpy's output device can be
  chosen), and *How to set up the phone*.

**Advanced** (every setting takes effect when you click **Apply**)
- The bar at the top has **Apply**, **Discard changes** and **Reset to defaults**. Apply and Discard are
  available only while there are changes; the line under them says when applying restarts the stream.
  *Reset to defaults* fills in the built-in audio values (40 / 20 / 15 / 10 ms, Music, media only, no extra
  options), measured as described in `TESTING.md`.
- *Buffer for each content type*: the values the Basic tab uses (slider and exact value, 5–500 ms).
- *When AdbAux starts*: the default content type and alert handling.
- *Tested profiles*: named sets of values for a phone and audio device combination (buffers, output buffer,
  start-up selections, a comment, the phone model and the date). Choosing a profile fills in its values;
  Apply uses them. *Save as new…*, *Update…* (stores the values shown, and edits the name, comment and
  link), *Delete* (asks first), *Import…* and *Export…* change the stored profiles at once.
  A profile can be linked to the connected phone's model: when that phone is connected, AdbAux applies the
  profile's values (only one profile per phone model; the current Basic selections are not changed).
  Exported files are small XML files without the phone's serial number and without the link; imported
  files are size-limited (64 KB), read without DTD processing, and every value is limited to its valid
  range. Extra scrcpy options are never part of a profile, so an imported profile cannot add options to
  the scrcpy command.
- *scrcpy options*: output buffer, extra command-line options (a value with spaces goes in double quotes).
- *Notifications*: phone connected/disconnected/not authorised; streaming started; streaming stopped
  unexpectedly (only reported while the phone is still connected, so unplugging is not an error).
- *adb*: the adb included with scrcpy (default), the Android SDK platform-tools adb (as used by
  Android Studio, found automatically), or a specific `adb.exe`. Use the same adb as your other tools,
  otherwise the two adb versions keep restarting each other's server.
- *General*: start streaming automatically when the phone is connected; optionally pause the phone's media
  when you stop streaming (Stop or Exit in AdbAux; only if an app is playing; not when the cable is unplugged
  or for Restart AdbAux) and, as a separate choice, resume it when streaming starts (with Start or
  automatically on connect, not after a restart for changed settings; if nothing is playing, AdbAux presses the
  phone's Play key, which resumes the app that played last). Both send Android media keys over adb
  (`input keyevent` 127 / 126) and read the playback state with `dumpsys media_session`; both are off by
  default. Keep running in the notification
  area when the window is closed; start with Windows (the entry is updated automatically if `AdbAux.exe`
  is moved); show the AdbAux icon in the Windows volume mixer; **App theme**: Use system setting
  (default), Light or Dark. "Use system setting" follows Settings > Personalisation > Colours >
  *Choose your app mode* and switches when that setting changes.
- *Back up and restore*: **Back up settings…** saves the settings in use and all tested profiles to one XML
  file (changes not applied yet are not included). **Restore settings…** reads a backup with the same checks
  as an imported profile (here up to 1 MB), then fills in the Advanced tab for you to check and **Apply**, and
  adds the backup's profiles at once (a profile with the same name is replaced; links to phones are kept, one
  automatic profile per phone model). Paths that do not exist on this PC (a custom adb.exe or scrcpy folder)
  keep their current value. If the backup contains extra scrcpy options different from yours, the
  confirmation shows them, because they are passed to scrcpy unchanged, and No is the default answer. The
  window position, the volume warning confirmation and Start with Windows are not restored.
- *scrcpy*: version and folder in use; **Check for scrcpy updates** compares the installed version with the
  latest official release on GitHub and says whether an update is available (or offers to install the same
  version again, for repairs); it installs only after you confirm. The download is SHA-256 checked, only ever
  installed into a folder AdbAux manages (never into a folder you chose), and the old copy is kept until the
  new one is in place. adb is included in scrcpy and updated with it; an Android SDK adb is left to Android
  Studio. **Choose folder…** and **Find automatically** (used after Apply); **Open folder**. If scrcpy is not
  found at start-up, AdbAux checks for the latest release and offers to install it.

**Volume mixer icon.** The volume mixer shows the icon of the program that plays the audio, which is
scrcpy. With the option on, AdbAux runs `scrcpy-adbaux.exe`, a copy of `scrcpy.exe` in the same folder in
which only the icon resources are replaced with the AdbAux icon (`MixerIcon.cs`); `scrcpy.exe` itself is not
changed. The SHA-256 of the `scrcpy.exe` it was made from is stored in `scrcpy-adbaux.sha256`, so the copy is
made again after an update. It is made only in a scrcpy folder AdbAux manages; if it cannot be made, AdbAux
runs `scrcpy.exe` and logs why. The volume mixer's name for the stream stays "scrcpy".

**Appearance.** AdbAux follows the Windows 11 design in light and dark mode: settings are grouped in
rounded cards under 16 px Semibold headings; field labels sit above their field, in sentence case without
a colon; buttons use sentence case, and an ellipsis (…) marks a button that asks for more input before it
acts. Body text is 14 px (10.5 pt at 100 % scaling) in Segoe UI Variable (Windows 11) or Segoe UI
(Windows 10); the log uses Cascadia Mono or Consolas. WinForms' classic controls cannot be restyled, so
tabs, buttons, checkboxes, radio buttons, drop-down lists, text and number fields and sliders are drawn
by AdbAux (`Theme.cs`). The main action of a tab (Start, Apply) is an accent-coloured button. Colours meet
the WCAG 2.2 contrast minimums in both modes (text 4.5:1 or more; outlines of controls, ticked and
selected states and focus outlines 3:1 or more). Dark mode also covers the title bar, scroll bars and the
tray menu; message boxes and file dialogs keep Windows' standard style.

**Log**: everything scrcpy and AdbAux report, with *Copy log*, *Clear view* and *Open log folder*. The log is
for your own troubleshooting: it is also saved to `%LOCALAPPDATA%\AdbAux\logs\adbaux.log` (about 1 MB,
previous file kept as `adbaux.old.log`), stays on your PC, and contains the phone's model and serial
number. Stops made by AdbAux itself are labelled with their reason; anything else is logged as
"exited unexpectedly". Unexpected program errors are written to the same file, and AdbAux closes
with a message pointing to it.

**About**: version, licence, short disclaimer, scrcpy credit and folder, settings file location.

Starting `AdbAux.exe` while it is already running brings the running window to the front.

The window can be resized, maximised and snapped (drag to a screen edge, Win+arrow keys, Windows 11
snap layouts). Its size, position and maximised state are remembered; a saved position on a monitor that
is no longer connected is ignored.

**Tray icon**: the AdbAux icon with a status badge. Grey = waiting for phone, yellow = phone not
authorised, blue = connected or starting, green = streaming, red = error.
Right-click for Open / Start / Stop / Restart streaming / Restart AdbAux / Exit. *Restart streaming*
restarts only scrcpy; *Restart AdbAux* closes the program and starts it again (the new copy waits until
the old one has closed).

## How the program works

- `StreamController` finds the phone and runs scrcpy; it has no controls, and the window shows what it
  reports. While no stream runs, it asks adb every 2 seconds which phone is connected. While a stream runs,
  adb is not asked: scrcpy ends by itself when the phone is unplugged, and then the checks resume.
- A stream is in one of three phases: idle, starting (scrcpy started, its helper on the phone has not
  reported yet) or streaming. When AdbAux stops scrcpy itself, it records why (shown in the log) and
  whether to start again; any other end is unexpected and retried after 5 seconds.
- Stopping scrcpy (`taskkill /T /F`) runs in the background, so the window never waits for it.
- Settings are written to a temporary file first, which then replaces `settings.xml`, so an interrupted
  write never leaves a damaged file. Values read from the file are limited to their valid ranges.

## Where things are stored

| What | Where |
|---|---|
| Settings and tested profiles | `%APPDATA%\AdbAux\settings.xml` |
| "Start with Windows" entry | Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `AdbAux` |
| scrcpy | `scrcpy` folder next to `AdbAux.exe`, a folder you choose, or `%LOCALAPPDATA%\AdbAux\scrcpy` |
| Log | `%LOCALAPPDATA%\AdbAux\logs\adbaux.log` (and `adbaux.old.log`) |

## Source files

| File | Contents |
|---|---|
| `AdbAux.csproj` | Project file: target framework, 64-bit, icon, references, version (the only place the version is set) |
| `app.manifest` | Normal user rights, Windows 10/11 compatibility, high-DPI awareness |
| `app.ico` | Program icon (16–256 px), made by `tools/make_icon.py` |
| `src/Program.cs` | Entry point; one running copy only (a second start shows the window); unexpected-error handling |
| `src/AppLog.cs` | Log file for the user's own troubleshooting |
| `src/AppSettings.cs` | Settings and their XML file; version; valid ranges of the audio values |
| `src/Profiles.cs` | Tested profiles: values, checks, export and import; safe reading of XML files from outside |
| `src/SettingsBackup.cs` | Back up and restore of all settings and profiles |
| `src/Adb.cs` | Chooses the adb.exe, runs `adb devices -l` and finds the USB phone |
| `src/Scrcpy.cs` | Finds scrcpy, checks for the latest release and installs it, runs a scrcpy process |
| `src/ScrcpyArgs.cs` | The scrcpy command line, including extra options with quotes |
| `src/StreamController.cs` | Phone checks and the scrcpy session: start, stop, restart, retry, status |
| `src/MixerIcon.cs` | Copy of scrcpy.exe with the AdbAux icon, for the Windows volume mixer |
| `src/Theme.cs` | Fonts, light/dark colours, dark title bar and scroll bars, and the Windows 11 style controls |
| `src/MainForm.cs` | Window, tray icon, status, notifications, Basic-tab selections |
| `src/MainForm.Pages.cs` | Basic, Log and About tabs; layout helpers |
| `src/MainForm.Advanced.cs` | Advanced tab: Apply / Discard, adb and scrcpy settings, scrcpy download |
| `src/MainForm.Profiles.cs` | Tested profiles section |
| `src/ProfileDialog.cs` | Name, comment and phone link of a profile |
| `tools/make_icon.py` | Draws the icon (Python with Pillow) |

## Tests

`..\test.cmd` runs the unit tests in `..\tests\AdbAux.Tests` (xUnit): parsing of `adb devices -l`, the scrcpy
command line and quoting, profile checks, export and import (including files that must be refused), settings
backup and restore, reading scrcpy version numbers for the update check, and a check that every
Advanced-tab setting is compared and copied by Apply.
