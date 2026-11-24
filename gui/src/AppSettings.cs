using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace AdbAux
{
    /// <summary>Which phone sounds go to the PC (measurements in TESTING.md).</summary>
    public enum CaptureMode
    {
        MediaOnly,   // scrcpy --audio-source=output   : media to PC, alerts stay on phone
        Everything,  // scrcpy --audio-source=playback : all sounds to PC
        Duplicate    // scrcpy --audio-dup             : PC and phone
    }

    /// <summary>Which adb.exe AdbAux and scrcpy use.</summary>
    public enum AdbChoice
    {
        Scrcpy,      // the adb.exe included in the scrcpy folder
        AndroidSdk,  // Android SDK platform-tools (the adb Android Studio uses)
        Custom       // a specific adb.exe chosen by the user
    }

    /// <summary>Content types offered on the Basic page. Each has its own buffer size.</summary>
    public enum ContentType
    {
        Music,
        Video,
        LowLatency
    }

    /// <summary>Program version, read from the exe (set once, in AdbAux.csproj).</summary>
    public static class AppInfo
    {
        public static string Version
        {
            get
            {
                string v = Application.ProductVersion;   // e.g. "0.5.0"
                int plus = v.IndexOf('+');
                return plus > 0 ? v.Substring(0, plus) : v;
            }
        }
    }

    /// <summary>Valid ranges of the audio values, used by the controls, the settings file and imported profiles.</summary>
    public static class Limits
    {
        public const int MinBufferMs = 5;
        public const int MaxBufferMs = 500;
        public const int MinOutputBufferMs = 1;
        public const int MaxOutputBufferMs = 100;

        public static int Clamp(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }

    /// <summary>
    /// User settings, stored as XML in %APPDATA%\AdbAux\settings.xml.
    /// XmlSerializer reads and writes the public fields below.
    /// </summary>
    public class AppSettings
    {
        // AdbAux defaults: values measured on 2026-11-23 (see TESTING.md). "Reset to defaults" uses these.
        public const int DefaultMusicBufferMs = 40;
        public const int DefaultVideoBufferMs = 20;
        public const int DefaultLowLatencyBufferMs = 15;
        public const int DefaultOutputBufferMs = 10;   // scrcpy's own default

        // Buffer used for each content type on the Basic page (milliseconds).
        public int MusicBufferMs = DefaultMusicBufferMs;
        public int VideoBufferMs = DefaultVideoBufferMs;
        public int LowLatencyBufferMs = DefaultLowLatencyBufferMs;

        // What the Basic page selects when the program starts.
        public ContentType DefaultContent = ContentType.Music;
        public CaptureMode DefaultMode = CaptureMode.MediaOnly;

        // Advanced scrcpy options.
        public int OutputBufferMs = DefaultOutputBufferMs;
        public string ExtraArgs = "";

        // Program behaviour.
        public bool AutoStart = true;        // start streaming when the phone is connected
        public bool PauseMediaOnStop = false;   // Stop / Exit in AdbAux pauses media playing on the phone
        public bool ResumeMediaOnStart = false; // when streaming starts, press Play on the phone if nothing plays
        public bool MinimizeToTray = true;   // closing the window keeps the program in the tray
        public string ScrcpyFolder = "";     // optional: a specific scrcpy folder to use

        // Which adb to use (avoids version conflicts with Android Studio's adb).
        public AdbChoice Adb = AdbChoice.Scrcpy;
        public string AdbCustomPath = "";

        // Run a copy of scrcpy.exe with the AdbAux icon, so the Windows volume mixer shows AdbAux (see MixerIcon.cs).
        public bool ShowIconInVolumeMixer = true;

        // Appearance: follow Windows' app mode, or always light / always dark.
        public ThemeChoice Theme = ThemeChoice.System;

        // Tray notifications (Windows shows them as pop-ups / in the notification centre).
        public bool NotifyConnection = false;  // phone connected, disconnected, not authorised
        public bool NotifyStreaming = true;    // streaming started (not after a settings restart)
        public bool NotifyErrors = true;       // scrcpy stopped unexpectedly while the phone is connected

        // Automatic update check for AdbAux and scrcpy (asked once at first start; see Updates.cs).
        public UpdateCheckChoice CheckForUpdates = UpdateCheckChoice.NotAsked;

        // Schedule and results of the automatic update check on this PC (not included in a settings backup).
        public UpdateState Updates = new UpdateState();

        // Tested profiles: values the user tested on a phone and audio device combination (see Profiles.cs).
        public List<TestedProfile> Profiles = new List<TestedProfile>();

        // Set once the user has confirmed the full-volume warning for "all sounds" mode.
        public bool LoudModeWarningShown = false;

        // Window size and position from the last run (0 = not saved yet: centred, default size).
        public int WindowLeft = 0;
        public int WindowTop = 0;
        public int WindowWidth = 0;
        public int WindowHeight = 0;
        public bool WindowMaximized = false;

        public static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AdbAux");
                return Path.Combine(dir, "settings.xml");
            }
        }

        public int GetBuffer(ContentType content)
        {
            switch (content)
            {
                case ContentType.Video: return VideoBufferMs;
                case ContentType.LowLatency: return LowLatencyBufferMs;
                default: return MusicBufferMs;
            }
        }

        /// <summary>A copy for editing. The profile list is shared, not copied.</summary>
        public AppSettings Clone()
        {
            return (AppSettings)MemberwiseClone();
        }

        // The next two methods list the settings shown on the Advanced tab. Keep them in step;
        // a unit test checks that a copied object compares equal and a changed one does not.

        /// <summary>True if the Advanced-tab settings of both objects are the same.</summary>
        public bool SameAdvancedSettings(AppSettings o)
        {
            return MusicBufferMs == o.MusicBufferMs
                && VideoBufferMs == o.VideoBufferMs
                && LowLatencyBufferMs == o.LowLatencyBufferMs
                && DefaultContent == o.DefaultContent
                && DefaultMode == o.DefaultMode
                && OutputBufferMs == o.OutputBufferMs
                && SameText(ExtraArgs, o.ExtraArgs)
                && AutoStart == o.AutoStart
                && PauseMediaOnStop == o.PauseMediaOnStop
                && ResumeMediaOnStart == o.ResumeMediaOnStart
                && MinimizeToTray == o.MinimizeToTray
                && SameText(ScrcpyFolder, o.ScrcpyFolder)
                && Adb == o.Adb
                && SameText(AdbCustomPath, o.AdbCustomPath)
                && ShowIconInVolumeMixer == o.ShowIconInVolumeMixer
                && Theme == o.Theme
                && NotifyConnection == o.NotifyConnection
                && NotifyStreaming == o.NotifyStreaming
                && NotifyErrors == o.NotifyErrors
                && CheckForUpdates == o.CheckForUpdates;
        }

        /// <summary>Copies the Advanced-tab settings from another object.</summary>
        public void CopyAdvancedSettings(AppSettings o)
        {
            MusicBufferMs = o.MusicBufferMs;
            VideoBufferMs = o.VideoBufferMs;
            LowLatencyBufferMs = o.LowLatencyBufferMs;
            DefaultContent = o.DefaultContent;
            DefaultMode = o.DefaultMode;
            OutputBufferMs = o.OutputBufferMs;
            ExtraArgs = o.ExtraArgs;
            AutoStart = o.AutoStart;
            PauseMediaOnStop = o.PauseMediaOnStop;
            ResumeMediaOnStart = o.ResumeMediaOnStart;
            MinimizeToTray = o.MinimizeToTray;
            ScrcpyFolder = o.ScrcpyFolder;
            Adb = o.Adb;
            AdbCustomPath = o.AdbCustomPath;
            ShowIconInVolumeMixer = o.ShowIconInVolumeMixer;
            Theme = o.Theme;
            NotifyConnection = o.NotifyConnection;
            NotifyStreaming = o.NotifyStreaming;
            NotifyErrors = o.NotifyErrors;
            CheckForUpdates = o.CheckForUpdates;
        }

        private static bool SameText(string a, string b)
        {
            return string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);
        }

        public TestedProfile FindProfile(string name)
        {
            return Profiles.Find(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));
        }

        /// <summary>Brings values read from the file (which can be edited by hand) into their valid ranges.</summary>
        public void Sanitize()
        {
            MusicBufferMs = Limits.Clamp(MusicBufferMs, Limits.MinBufferMs, Limits.MaxBufferMs);
            VideoBufferMs = Limits.Clamp(VideoBufferMs, Limits.MinBufferMs, Limits.MaxBufferMs);
            LowLatencyBufferMs = Limits.Clamp(LowLatencyBufferMs, Limits.MinBufferMs, Limits.MaxBufferMs);
            OutputBufferMs = Limits.Clamp(OutputBufferMs, Limits.MinOutputBufferMs, Limits.MaxOutputBufferMs);
            if (!Enum.IsDefined(typeof(ContentType), DefaultContent))
            {
                DefaultContent = ContentType.Music;
            }
            if (!Enum.IsDefined(typeof(CaptureMode), DefaultMode))
            {
                DefaultMode = CaptureMode.MediaOnly;
            }
            if (!Enum.IsDefined(typeof(AdbChoice), Adb))
            {
                Adb = AdbChoice.Scrcpy;
            }
            if (!Enum.IsDefined(typeof(ThemeChoice), Theme))
            {
                Theme = ThemeChoice.System;
            }
            if (!Enum.IsDefined(typeof(UpdateCheckChoice), CheckForUpdates))
            {
                CheckForUpdates = UpdateCheckChoice.NotAsked;
            }
            if (Updates == null)
            {
                Updates = new UpdateState();
            }
            Updates.Sanitize();
            if (Profiles == null)
            {
                Profiles = new List<TestedProfile>();
            }
            Profiles.RemoveAll(p => p == null);
            foreach (TestedProfile p in Profiles)
            {
                p.Sanitize();
            }
        }

        /// <summary>Reads the settings file, or returns the defaults if it is missing or damaged.</summary>
        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    AppSettings loaded;
                    using (FileStream stream = File.OpenRead(SettingsPath))
                    {
                        loaded = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(stream);
                    }
                    loaded.Sanitize();
                    return loaded;
                }
            }
            catch (Exception)
            {
                // Damaged file: fall through to defaults.
            }
            return new AppSettings();
        }

        /// <summary>
        /// Writes the settings. They go to a temporary file first, which then replaces the old file,
        /// so an interrupted write (for example a power cut) never leaves a half-written settings file.
        /// </summary>
        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            string temp = SettingsPath + ".tmp";
            using (FileStream stream = File.Create(temp))
            {
                new XmlSerializer(typeof(AppSettings)).Serialize(stream, this);
            }
            if (File.Exists(SettingsPath))
            {
                File.Replace(temp, SettingsPath, null);
            }
            else
            {
                File.Move(temp, SettingsPath);
            }
        }
    }
}
