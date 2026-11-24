using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AdbAux
{
    /// <summary>Finds the scrcpy folder (scrcpy.exe + adb.exe) and reads its version.</summary>
    public static class ScrcpyLocator
    {
        /// <summary>Folder containing AdbAux.exe.</summary>
        public static string AppDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        /// <summary>Fallback install location when the program's folder is not writable.</summary>
        public static string LocalDir
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AdbAux", "scrcpy");
            }
        }

        /// <summary>Returns the first folder that contains scrcpy.exe and adb.exe, or null.</summary>
        public static string Find(AppSettings settings)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(settings.ScrcpyFolder))
            {
                candidates.Add(settings.ScrcpyFolder);
            }
            candidates.Add(Path.Combine(AppDir, "scrcpy"));   // e.g. phone-audio-scrcpy\scrcpy
            candidates.Add(LocalDir);

            foreach (string dir in candidates)
            {
                if (File.Exists(Path.Combine(dir, "scrcpy.exe")) && File.Exists(Path.Combine(dir, "adb.exe")))
                {
                    return Path.GetFullPath(dir);
                }
            }
            return null;
        }

        /// <summary>First line of "scrcpy --version", e.g. "scrcpy 5.0.1 &lt;https://...&gt;".</summary>
        public static string GetVersion(string scrcpyDir)
        {
            string output = ProcessRunner.Run(Path.Combine(scrcpyDir, "scrcpy.exe"), "--version", 5000);
            if (output == null)
            {
                return null;
            }
            foreach (string line in output.Split('\n'))
            {
                if (line.Trim().Length > 0)
                {
                    return line.Trim();
                }
            }
            return null;
        }

        public static bool CanWriteTo(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// True for the two folders AdbAux owns: "scrcpy" next to AdbAux.exe, and
        /// %LOCALAPPDATA%\AdbAux\scrcpy. Only these may be replaced by an update.
        /// </summary>
        public static bool IsManagedFolder(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir))
            {
                return false;
            }
            string full = Normalize(dir);
            return string.Equals(full, Normalize(Path.Combine(AppDir, "scrcpy")), StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, Normalize(LocalDir), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// What a scrcpy folder in use is, for the window and the log. Find only returns a chosen folder
        /// or one of the two managed folders, so any other folder is the one the user chose.
        /// </summary>
        public static string Describe(string dir)
        {
            return IsManagedFolder(dir) ? "folder managed by AdbAux" : "folder you chose";
        }

        /// <summary>The folder a download goes to: the managed folder in use, or a new managed folder.</summary>
        public static string ManagedInstallTarget(string currentDir)
        {
            if (IsManagedFolder(currentDir))
            {
                return Normalize(currentDir);
            }
            string nextToExe = Path.Combine(AppDir, "scrcpy");
            return CanWriteTo(AppDir) ? Normalize(nextToExe) : Normalize(LocalDir);
        }

        private static string Normalize(string dir)
        {
            return Path.GetFullPath(dir).TrimEnd('\\');
        }
    }

    /// <summary>The latest scrcpy release for 64-bit Windows, as reported by GitHub.</summary>
    public sealed class ScrcpyRelease
    {
        public string Tag;          // e.g. v5.0.1
        public Version Version;     // e.g. 5.0.1 (null if the tag has no version number)
        public string ZipName;
        public string ZipUrl;
        public string SumsUrl;
    }

    /// <summary>
    /// Finds the latest official scrcpy release for 64-bit Windows on GitHub, and installs it: the
    /// download is verified against the developer's SHA256SUMS.txt before anything is replaced.
    /// </summary>
    public static class ScrcpyInstaller
    {
        /// <summary>
        /// The first version number in a text: "scrcpy 5.0.1 &lt;https://...&gt;" or "v5.0.1" give 5.0.1.
        /// Returns null if there is none.
        /// </summary>
        public static Version ParseVersion(string text)
        {
            Match m = Regex.Match(text ?? "", @"\d+\.\d+(\.\d+){0,2}");
            Version version;
            return m.Success && Version.TryParse(m.Value, out version) ? version : null;
        }

        /// <summary>
        /// Asks GitHub for the latest release and the two files AdbAux needs from it.
        /// Throws UpdateCheckException with the type of failure.
        /// </summary>
        public static async Task<ScrcpyRelease> GetLatestAsync()
        {
            Dictionary<string, object> data = await GitHub.GetLatestReleaseAsync(GitHub.ScrcpyRepository).ConfigureAwait(false);
            if (data == null)
            {
                throw new UpdateCheckException(UpdateFailure.UnexpectedReply, "GitHub lists no published scrcpy release.", null);
            }
            var release = new ScrcpyRelease { Tag = Convert.ToString(data["tag_name"]) };
            release.Version = ParseVersion(release.Tag);

            var assets = data.ContainsKey("assets") ? data["assets"] as IEnumerable : null;
            foreach (object item in assets ?? new object[0])
            {
                var asset = item as IDictionary<string, object>;
                if (asset == null || !asset.ContainsKey("name") || !asset.ContainsKey("browser_download_url"))
                {
                    continue;
                }
                string name = Convert.ToString(asset["name"]);
                string url = Convert.ToString(asset["browser_download_url"]);
                if (release.ZipName == null
                    && name.StartsWith("scrcpy-win64-", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    release.ZipName = name;
                    release.ZipUrl = url;
                }
                else if (name == "SHA256SUMS.txt")
                {
                    release.SumsUrl = url;
                }
            }
            if (release.ZipUrl == null)
            {
                throw new UpdateCheckException(UpdateFailure.UnexpectedReply, "Release " + release.Tag + " has no scrcpy-win64 zip.", null);
            }
            if (release.SumsUrl == null)
            {
                throw new UpdateCheckException(UpdateFailure.UnexpectedReply,
                    "Release " + release.Tag + " has no SHA256SUMS.txt, so the download cannot be verified.", null);
            }
            return release;
        }

        /// <param name="targetDir">Must be a folder AdbAux manages (it is replaced completely).</param>
        /// <param name="stopAdbServer">Stop the adb server first, when it runs from the folder being replaced.</param>
        public static async Task InstallAsync(ScrcpyRelease release, string targetDir, bool stopAdbServer, Action<string> log)
        {
            // Safety check: the target folder is replaced, so it must never be a folder the user chose
            // (it might contain other files).
            if (!ScrcpyLocator.IsManagedFolder(targetDir))
            {
                throw new InvalidOperationException(
                    "Refusing to replace a folder that AdbAux does not manage: " + targetDir);
            }

            using (HttpClient http = GitHub.NewClient())
            {
                string tag = release.Tag;
                string zipName = release.ZipName;

                // 1. Download the zip and the checksum list.
                log("Downloading " + zipName + " (" + tag + ")...");
                byte[] zip = await http.GetByteArrayAsync(release.ZipUrl).ConfigureAwait(false);
                string sums = await http.GetStringAsync(release.SumsUrl).ConfigureAwait(false);

                // 2. Verify: our SHA-256 of the zip must equal the published one.
                //    Each SHA256SUMS.txt line is "<hash>  <file name>".
                string expected = null;
                foreach (string line in sums.Split('\n'))
                {
                    string[] parts = line.Trim().Split(new[] { ' ', '\t', '*' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[parts.Length - 1] == zipName)
                    {
                        expected = parts[0];
                        break;
                    }
                }
                if (expected == null)
                {
                    throw new InvalidOperationException(zipName + " is not listed in SHA256SUMS.txt.");
                }
                string actual;
                using (SHA256 sha = SHA256.Create())
                {
                    actual = BitConverter.ToString(sha.ComputeHash(zip)).Replace("-", "");
                }
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Checksum mismatch: expected " + expected + ", got " + actual + ". Nothing was installed.");
                }
                log("Checksum OK: " + actual);

                // 3. Extract next to the target folder (same drive, so the final move is a quick rename).
                string fullTarget = Path.GetFullPath(targetDir).TrimEnd('\\');
                string parent = Path.GetDirectoryName(fullTarget);
                Directory.CreateDirectory(parent);
                string staging = fullTarget + ".download";
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, true);
                }
                Directory.CreateDirectory(staging);
                string zipPath = Path.Combine(staging, zipName);
                File.WriteAllBytes(zipPath, zip);
                string extractDir = Path.Combine(staging, "extract");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // The zip holds one folder (e.g. scrcpy-win64-v5.0.1) with the program inside.
                string inner = null;
                foreach (string dir in Directory.GetDirectories(extractDir))
                {
                    if (File.Exists(Path.Combine(dir, "scrcpy.exe")))
                    {
                        inner = dir;
                        break;
                    }
                }
                if (inner == null && File.Exists(Path.Combine(extractDir, "scrcpy.exe")))
                {
                    inner = extractDir;
                }
                if (inner == null)
                {
                    throw new InvalidOperationException("scrcpy.exe was not found inside the downloaded zip.");
                }

                // 4. Stop the adb server of the old copy; while it runs, adb.exe is locked and cannot be replaced.
                string oldAdb = Path.Combine(fullTarget, "adb.exe");
                if (stopAdbServer && File.Exists(oldAdb))
                {
                    try { ProcessRunner.Run(oldAdb, "kill-server", 10000); } catch (Exception) { }
                }

                // 5. Replace the old copy. The old folder is renamed first, not deleted, so it can be put
                //    back if the new one cannot be moved in. Renaming fails while a file in the folder is
                //    in use, and then nothing has been changed yet.
                string backup = fullTarget + ".old";
                if (Directory.Exists(backup))
                {
                    Directory.Delete(backup, true);
                }
                bool hadOld = Directory.Exists(fullTarget);
                if (hadOld)
                {
                    Directory.Move(fullTarget, backup);
                }
                try
                {
                    Directory.Move(inner, fullTarget);
                }
                catch (Exception)
                {
                    if (hadOld && !Directory.Exists(fullTarget))
                    {
                        Directory.Move(backup, fullTarget);   // restore the old copy
                    }
                    throw;
                }

                // 6. Clean up. Failures here are harmless: the next update removes the leftovers.
                try
                {
                    if (hadOld)
                    {
                        Directory.Delete(backup, true);
                    }
                    Directory.Delete(staging, true);
                }
                catch (Exception)
                {
                }

                log("scrcpy " + tag + " installed in " + fullTarget);
            }
        }
    }

    /// <summary>One running scrcpy process, with its output lines and exit reported as events.</summary>
    public sealed class ScrcpySession : IDisposable
    {
        private Process process;

        /// <summary>Raised (on a background thread) for each line scrcpy prints.</summary>
        public event EventHandler<string> OutputLine;

        /// <summary>Raised (on a background thread) when scrcpy has exited; the value is its exit code.</summary>
        public event EventHandler<int> Exited;

        /// <param name="adbPath">adb.exe for scrcpy to use (passed in the ADB environment variable, which scrcpy reads).</param>
        public void Start(string scrcpyExe, string arguments, string adbPath)
        {
            var psi = new ProcessStartInfo(scrcpyExe, arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(scrcpyExe)   // scrcpy finds its DLLs here
            };
            if (!string.IsNullOrEmpty(adbPath))
            {
                psi.EnvironmentVariables["ADB"] = adbPath;   // only for this scrcpy process, not system-wide
            }

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (s, e) => RaiseLine(e.Data);
            p.ErrorDataReceived += (s, e) => RaiseLine(e.Data);
            p.Exited += (s, e) =>
            {
                int code = -1;
                try { code = p.ExitCode; } catch (Exception) { }
                EventHandler<int> handler = Exited;
                if (handler != null)
                {
                    handler(this, code);
                }
            };

            p.Start();
            p.BeginOutputReadLine();   // read output line by line in the background
            p.BeginErrorReadLine();
            process = p;
        }

        /// <summary>
        /// Stops scrcpy and waits (up to 5 s) until it has ended, so its files are no longer in use.
        /// taskkill /T ends scrcpy together with the adb.exe it started for its helper on the phone;
        /// /F forces it, because scrcpy has no window to receive a close request.
        /// Can be called from any thread.
        /// </summary>
        public void Stop()
        {
            Process p = process;
            if (p == null)
            {
                return;
            }
            try
            {
                if (p.HasExited)
                {
                    return;
                }
                ProcessRunner.Run(Path.Combine(Environment.SystemDirectory, "taskkill.exe"),
                                  "/PID " + p.Id + " /T /F", 5000);
                if (!p.HasExited)
                {
                    p.Kill();
                }
                p.WaitForExit(5000);
            }
            catch (InvalidOperationException) { }              // already ended or cleaned up (ObjectDisposedException is a subtype)
            catch (System.ComponentModel.Win32Exception) { }   // could not be stopped (access denied)
        }

        public void Dispose()
        {
            Stop();
            if (process != null)
            {
                process.Dispose();
                process = null;
            }
        }

        private void RaiseLine(string line)
        {
            if (line == null)
            {
                return;   // end of stream
            }
            EventHandler<string> handler = OutputLine;
            if (handler != null)
            {
                handler(this, line);
            }
        }
    }
}
