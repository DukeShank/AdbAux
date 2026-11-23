using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdbAux
{
    /// <summary>
    /// Decides which adb.exe to use. Different adb versions on one PC keep restarting each other's
    /// background server ("adb server version doesn't match this client; killing..."), so users of
    /// Android Studio can choose its adb instead of the one included with scrcpy.
    /// </summary>
    public static class AdbLocator
    {
        /// <summary>adb.exe from the Android SDK platform-tools (as used by Android Studio), or null.</summary>
        public static string FindAndroidSdkAdb()
        {
            var roots = new List<string>();
            foreach (string variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
            {
                string value = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    roots.Add(value);
                }
            }
            // Android Studio's default SDK location on Windows.
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk"));

            foreach (string root in roots)
            {
                string adb = Path.Combine(root, "platform-tools", "adb.exe");
                if (File.Exists(adb))
                {
                    return adb;
                }
            }
            return null;
        }

        /// <summary>
        /// Returns the adb.exe to use (null if scrcpy is not found either). If the chosen adb is
        /// missing, falls back to scrcpy's and explains why in note.
        /// </summary>
        public static string Resolve(AppSettings settings, string scrcpyDir, out string note)
        {
            note = null;
            string bundled = scrcpyDir != null ? Path.Combine(scrcpyDir, "adb.exe") : null;
            switch (settings.Adb)
            {
                case AdbChoice.AndroidSdk:
                    string sdkAdb = FindAndroidSdkAdb();
                    if (sdkAdb != null)
                    {
                        return sdkAdb;
                    }
                    note = "Android SDK adb not found; using the adb included with scrcpy.";
                    return bundled;

                case AdbChoice.Custom:
                    if (!string.IsNullOrWhiteSpace(settings.AdbCustomPath) && File.Exists(settings.AdbCustomPath))
                    {
                        return settings.AdbCustomPath;
                    }
                    note = "The chosen adb.exe was not found; using the adb included with scrcpy.";
                    return bundled;

                default:
                    return bundled;
            }
        }
    }

    /// <summary>Runs a command-line program and returns its text output.</summary>
    public static class ProcessRunner
    {
        /// <summary>
        /// Runs exe with args, waits up to timeoutMs, and returns standard output.
        /// Returns null if the program did not finish in time (it is then stopped).
        /// </summary>
        public static string Run(string exe, string args, int timeoutMs)
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,         // start the exe directly, so output can be captured
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true            // no console window flashing up
            };
            using (var p = new Process { StartInfo = psi })
            {
                p.Start();
                // Read both streams in the background; reading them one after the other can deadlock
                // when a program fills one stream while we wait on the other.
                Task<string> stdout = p.StandardOutput.ReadToEndAsync();
                Task<string> stderr = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs) || !stdout.Wait(2000))
                {
                    try { if (!p.HasExited) p.Kill(); } catch (Exception) { }
                    return null;
                }
                stderr.Wait(500);
                return stdout.Result;
            }
        }
    }

    /// <summary>A device line from "adb devices -l".</summary>
    public sealed class PhoneInfo
    {
        public string Serial;   // e.g. R58N12ABCDE
        public string State;    // device, unauthorized, offline, ...
        public string Model;    // e.g. SM_A366E

        public bool IsReady { get { return State == "device"; } }

        public string DisplayModel { get { return (Model ?? "phone").Replace('_', '-'); } }
    }

    /// <summary>Finds a phone connected over USB using adb (the copy shipped with scrcpy).</summary>
    public static class AdbClient
    {
        // Android key codes for the media keys (android.view.KeyEvent).
        public const int KeyMediaPlay = 126;
        public const int KeyMediaPause = 127;

        // "state=PlaybackState {state=PLAYING(3), ..." (Android 13+) or "state=PlaybackState {state=3, ..." (older).
        private static readonly Regex PlaybackStateField = new Regex(@"PlaybackState \{state=(?:\w+\()?(\d+)");
        private const int PlaybackStatePlaying = 3;

        /// <summary>True if "adb shell dumpsys media_session" lists a media session that is playing.</summary>
        public static bool IsMediaPlaying(string dumpsysOutput)
        {
            foreach (Match m in PlaybackStateField.Matches(dumpsysOutput ?? ""))
            {
                if (m.Groups[1].Value == PlaybackStatePlaying.ToString())
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Asks the phone whether an app is playing media. False if the phone does not answer.</summary>
        public static bool QueryMediaPlaying(string adbPath, string serial)
        {
            string output = ProcessRunner.Run(adbPath, "-s " + serial + " shell dumpsys media_session", 4000);
            return output != null && IsMediaPlaying(output);
        }

        /// <summary>Sends a key press to the phone (for example a media key), as if a headset button was pressed.</summary>
        public static void SendKey(string adbPath, string serial, int keyCode)
        {
            ProcessRunner.Run(adbPath, "-s " + serial + " shell input keyevent " + keyCode, 4000);
        }

        // "SERIAL   STATE ..." lines. The header "List of devices attached" does not match.
        private static readonly Regex DeviceLine = new Regex(
            @"^(\S+)\s+(device|unauthorized|offline|no permissions|authorizing|connecting)\b",
            RegexOptions.IgnoreCase);

        private static readonly Regex ModelField = new Regex(@"\bmodel:(\S+)");

        /// <summary>
        /// Starts the adb background server. Its output is NOT captured on purpose: the server keeps
        /// running after this command ends, and if it inherited a captured output pipe, later reads
        /// of that pipe would never finish.
        /// </summary>
        public static void StartServer(string adbPath)
        {
            var psi = new ProcessStartInfo(adbPath, "start-server")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (Process p = Process.Start(psi))
            {
                p.WaitForExit(15000);
            }
        }

        /// <summary>Returns the first USB-connected device, or null if none is connected.</summary>
        public static PhoneInfo FindUsbPhone(string adbPath)
        {
            string output = ProcessRunner.Run(adbPath, "devices -l", 5000);
            if (output == null)
            {
                throw new TimeoutException("adb did not respond within 5 seconds.");
            }
            return Parse(output);
        }

        /// <summary>
        /// Parses "adb devices -l". On Windows adb prints no "usb:" field, so a device counts as USB
        /// when its ID is not a network address ("ip:port", "...adb-tls-connect...") or an emulator.
        /// </summary>
        public static PhoneInfo Parse(string adbDevicesOutput)
        {
            foreach (string raw in adbDevicesOutput.Split('\n'))
            {
                string line = raw.Trim();
                Match m = DeviceLine.Match(line);
                if (!m.Success)
                {
                    continue;
                }

                string serial = m.Groups[1].Value;
                if (serial.Contains(":")
                    || serial.StartsWith("emulator-", StringComparison.OrdinalIgnoreCase)
                    || serial.Contains("adb-tls-connect"))
                {
                    continue;   // network device (e.g. a TV) or emulator
                }

                Match model = ModelField.Match(line);
                return new PhoneInfo
                {
                    Serial = serial,
                    State = m.Groups[2].Value.ToLowerInvariant(),
                    Model = model.Success ? model.Groups[1].Value : "unknown model"
                };
            }
            return null;
        }
    }
}
