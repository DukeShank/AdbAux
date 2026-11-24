# AdbAux privacy statement

AdbAux has no telemetry, no account and no advertising. It does not collect personal data, and it sends
nothing about you or your phone to anyone. This statement is part of the program: AdbAux shows it under
About > Privacy, and it applies to the AdbAux version it came with.

## Internet connections

AdbAux connects to the internet only in the cases below, and only to GitHub: `api.github.com` for release
information and `github.com` for downloads.

- **Automatic update check (optional).** At first start, AdbAux asks whether it may check for updates. If you
  answer Yes, it asks GitHub once a day for the latest published versions of AdbAux and scrcpy, and shows a
  notification when a newer version exists. Turn it on or off on the Advanced tab, under General.
- **Manual checks.** Check for AdbAux updates (About tab) and Check for scrcpy updates (Advanced tab), only
  when you click them.
- **scrcpy download.** At first start, if scrcpy is not found, AdbAux asks whether to download it, and contacts
  GitHub only if you answer OK. scrcpy updates are also installed only after you confirm.

Each request sends GitHub this PC's IP address and the program name and version (for example `AdbAux/0.6.0`);
nothing else. AdbAux never downloads or replaces its own exe: for a new AdbAux version, it opens the release
page in your browser. How GitHub handles requests is described in the GitHub General Privacy Statement:
https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement

## Audio

Audio travels only over the USB cable between the phone and the PC. AdbAux plays it in real time and does
not record or store it.

## Data stored on this PC

- **Settings:** `%APPDATA%\AdbAux\settings.xml`. Your settings, your tested profiles (which can include phone
  model names) and the schedule of the update check.
- **Log:** `%LOCALAPPDATA%\AdbAux\logs\adbaux.log`, about 1 MB, and the previous file `adbaux.old.log`. It is
  for your own troubleshooting and is never sent anywhere. It contains your phone's model and serial number,
  so remove them before sharing a log, for example in a bug report.
- **scrcpy:** the scrcpy folder next to `AdbAux.exe`, or `%LOCALAPPDATA%\AdbAux\scrcpy`.
- **adb keys:** adb, which is part of scrcpy, keeps the key your phone authorises in `%USERPROFILE%\.android`.
- **Files you save:** exported profiles (without the phone's serial number) and settings backups, in the
  folders you choose.

The README section "Uninstalling" lists how to remove all of these.

## On the phone

scrcpy copies a small helper program to the phone for each session; it captures the audio there, and nothing
remains installed. The phone stores the authorisation of this PC for USB debugging until you revoke it
(Developer options > Revoke USB debugging authorisations).

## Questions

Open an issue at https://github.com/DukeShank/AdbAux/issues. Do not post logs with your phone's serial
number.
