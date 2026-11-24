# AdbAux (Windows program)

Source of `AdbAux.exe`: C# WinForms, .NET Framework 4.8 (part of Windows 10/11), 64-bit, one exe without
extra DLLs. This file is for building and changing the code. How to use AdbAux is in [`../README.md`](../README.md);
each setting is explained in the program itself (the short line under it and its (i) button), and the
detailed behaviour is described in the comments of the source files listed below.

## Building

Requires the .NET SDK (any current version; it can build for .NET Framework 4.8).

1. Check whether an SDK is installed:
   ```
   dotnet --list-sdks
   ```
   An empty result or an error means none is installed. Install one with
   `winget install Microsoft.DotNet.SDK.10`, or with the Visual Studio Installer's ".NET desktop development" workload.
2. Run `build.cmd` in this folder. It runs `dotnet build AdbAux.csproj -c Release` and copies
   the result to `..\AdbAux.exe`. The first build downloads .NET Framework reference files from nuget.org (once).

The version number is set only in `AdbAux.csproj`. GitHub Actions builds and tests every push
(`../.github/workflows/build.yml`).

## Tests

`..\test.cmd` runs the unit tests in `..\tests\AdbAux.Tests` (xUnit). They cover the parts without a window:
parsing of `adb devices -l`, the scrcpy command line, profiles and settings files (including files that must be
refused), the update-check schedule, the built-in documents, and a check that Apply compares and copies every
Advanced-tab setting.

## How the program works

- **Streaming.** `StreamController` finds the phone and runs scrcpy; the window only shows what it reports.
  While no stream runs, it asks adb every 2 seconds which phone is connected; while a stream runs, adb is not
  asked. Stops made by AdbAux are recorded with their reason; any other end of scrcpy is retried after 5 seconds.
- **Settings.** Basic-tab choices apply at once; every Advanced-tab setting waits for Apply. `settings.xml` is
  written to a temporary file first and then replaced, and values read from it are limited to their valid ranges.
- **Files from outside** (imported profiles, settings backups) are read by `SafeXml`: size limit, no DTD
  processing, every value limited to its range. Extra scrcpy options are never taken from a profile.
- **Network.** Only GitHub is contacted (see `../PRIVACY.md`). `Updates.cs` makes the requests, sorts failures
  into types and holds the rules of the automatic check. scrcpy downloads are checked against the release's
  `SHA256SUMS.txt` and installed only into a folder AdbAux manages, with the old copy kept until the new one
  is in place.
- **Volume mixer icon.** `MixerIcon.cs` makes `scrcpy-adbaux.exe`, a copy of `scrcpy.exe` with only the icon
  resources replaced; `scrcpy.exe` is not changed.
- **Appearance.** WinForms' classic controls cannot be restyled, so `Theme.cs` draws Windows 11 style controls in
  light and dark mode, with colours checked against the WCAG 2.2 contrast minimums. `InfoNote.cs` is the short
  description with its (i) button.
- **Built-in documents.** `../PRIVACY.md` and `../LICENSE` are embedded in the exe, so About can show them offline.

Where AdbAux stores data, and how to remove it: `../PRIVACY.md` and the README section *Uninstalling*.

## Source files

| File | Contents |
|---|---|
| `AdbAux.csproj` | Project file: target framework, 64-bit, icon, references, version, embedded documents |
| `app.manifest` | Normal user rights, Windows 10/11 compatibility, high-DPI awareness |
| `app.ico` | Program icon (16–256 px), made by `tools/make_icon.py` |
| `src/Program.cs` | Entry point; one running copy only (a second start shows the window); unexpected-error handling |
| `src/AppLog.cs` | Log file for the user's own troubleshooting |
| `src/AppSettings.cs` | Settings and their XML file; version; valid ranges of the audio values |
| `src/Profiles.cs` | Tested profiles: values, checks, export and import; safe reading of XML files from outside |
| `src/SettingsBackup.cs` | Back up and restore of all settings and profiles |
| `src/Adb.cs` | Chooses the adb.exe, runs `adb devices -l` and finds the USB phone; media keys |
| `src/Scrcpy.cs` | Finds scrcpy, gets the latest release and installs it, runs a scrcpy process |
| `src/ScrcpyArgs.cs` | The scrcpy command line, including extra options with quotes |
| `src/StreamController.cs` | Phone checks and the scrcpy session: start, stop, restart, retry, status |
| `src/MixerIcon.cs` | Copy of scrcpy.exe with the AdbAux icon, for the Windows volume mixer |
| `src/Updates.cs` | GitHub release requests and their errors; schedule and messages of the automatic update check |
| `src/Theme.cs` | Fonts, light/dark colours, and the Windows 11 style controls |
| `src/InfoNote.cs` | Short description with an (i) button and its details panel |
| `src/MainForm.cs` | Window, tray icon, status, notifications, Basic-tab selections |
| `src/MainForm.Pages.cs` | Basic, Log and About tabs; layout helpers |
| `src/MainForm.Advanced.cs` | Advanced tab: Apply / Discard, adb and scrcpy settings, scrcpy download |
| `src/MainForm.Profiles.cs` | Tested profiles section |
| `src/MainForm.Updates.cs` | Update question at first start, automatic check, Basic-tab line, About-tab result |
| `src/ProfileDialog.cs` | Name, comment and phone link of a profile |
| `src/UpdateDialog.cs` | Choices offered when a newer version is found |
| `src/TextDialog.cs` | Window for the built-in privacy statement and licence |
| `tools/make_icon.py` | Draws the icon (Python with Pillow) |
