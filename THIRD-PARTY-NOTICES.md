# Third-party notices

AdbAux's own code is released under the MIT License (see `LICENSE`).
AdbAux does **not** include or redistribute the software below. It downloads scrcpy from its
official GitHub releases (with the program's *Check for scrcpy updates* button)
and runs it as a separate program.

## scrcpy

- Project: https://github.com/Genymobile/scrcpy
- Copyright: Genymobile and Romain Vimont
- Licence: Apache License 2.0

The scrcpy release archive for Windows also contains other components, including **adb**
(Android SDK Platform-Tools, Apache License 2.0) and libraries such as SDL and FFmpeg, each under
its own licence. Their licence files are inside the downloaded `scrcpy` folder.

With the option *Show the AdbAux icon in the Windows volume mixer* (on by default), AdbAux makes
`scrcpy-adbaux.exe` on the user's PC: a copy of `scrcpy.exe` in which only the icon resources are
replaced with the AdbAux icon. No other part of scrcpy is changed, and the copy is not distributed.

## .NET Framework

AdbAux targets .NET Framework 4.8, which is part of Windows. It is not redistributed.

## Icon

The AdbAux icon (`gui/app.ico`) was created for this project by `gui/tools/make_icon.py` and is
covered by the MIT License like the rest of the code.

## Trademarks

Android and YouTube are trademarks of Google LLC. Samsung, Amazon Music, Twitch, FiiO and other
product names mentioned in this repository are trademarks of their respective owners and are used only
to identify products. AdbAux is an independent project and is not affiliated with or endorsed by Google,
Genymobile, or any phone, app or audio-device maker mentioned here.
