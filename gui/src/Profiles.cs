using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace AdbAux
{
    /// <summary>
    /// Values a user tested on one phone and audio device combination ("tested profile"): the buffer
    /// for each content type, the output buffer and what the Basic page selects at start-up.
    /// Extra scrcpy options are deliberately not part of a profile, so an imported profile cannot add
    /// command-line options (such as recording to a file) to scrcpy.
    /// </summary>
    public class TestedProfile
    {
        public const int MaxNameLength = 60;
        public const int MaxCommentLength = 300;

        public string Name = "";
        public string Comment = "";
        public int MusicBufferMs = AppSettings.DefaultMusicBufferMs;
        public int VideoBufferMs = AppSettings.DefaultVideoBufferMs;
        public int LowLatencyBufferMs = AppSettings.DefaultLowLatencyBufferMs;
        public int OutputBufferMs = AppSettings.DefaultOutputBufferMs;
        public ContentType DefaultContent = ContentType.Music;
        public CaptureMode DefaultMode = CaptureMode.MediaOnly;

        // Phone model reported by adb (for example SM_A366E); "" = not recorded. Never the serial number.
        public string PhoneModel = "";

        // Select this profile automatically when a phone of that model is connected.
        public bool UseWhenPhoneConnected = false;

        // Date saved, yyyy-MM-dd.
        public string Saved = "";

        /// <summary>The phone model as shown to the user (SM-A366E instead of SM_A366E).</summary>
        public string PhoneDisplay
        {
            get { return (PhoneModel ?? "").Replace('_', '-'); }
        }

        public TestedProfile Clone()
        {
            return (TestedProfile)MemberwiseClone();
        }

        /// <summary>True if the settings use exactly this profile's values.</summary>
        public bool MatchesSettings(AppSettings s)
        {
            return s.MusicBufferMs == MusicBufferMs
                && s.VideoBufferMs == VideoBufferMs
                && s.LowLatencyBufferMs == LowLatencyBufferMs
                && s.OutputBufferMs == OutputBufferMs
                && s.DefaultContent == DefaultContent
                && s.DefaultMode == DefaultMode;
        }

        /// <summary>Copies this profile's values into the settings (they still need to be saved).</summary>
        public void ApplyTo(AppSettings s)
        {
            s.MusicBufferMs = MusicBufferMs;
            s.VideoBufferMs = VideoBufferMs;
            s.LowLatencyBufferMs = LowLatencyBufferMs;
            s.OutputBufferMs = OutputBufferMs;
            s.DefaultContent = DefaultContent;
            s.DefaultMode = DefaultMode;
        }

        /// <summary>
        /// Brings every value into its valid range. Used for imported files and the settings file, which
        /// can be edited by hand.
        /// </summary>
        public void Sanitize()
        {
            Name = CleanText(Name, MaxNameLength);
            if (Name.Length == 0)
            {
                Name = "Imported profile";
            }
            Comment = CleanText(Comment, MaxCommentLength);
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
            // Model names from adb contain only letters, digits, '_', '-' and '.'.
            PhoneModel = PhoneModel != null && Regex.IsMatch(PhoneModel, @"^[A-Za-z0-9_.\-]{1,40}$") ? PhoneModel : "";
            if (PhoneModel.Length == 0)
            {
                UseWhenPhoneConnected = false;
            }
            DateTime date;
            if (!DateTime.TryParseExact(Saved ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                Saved = "";
            }
        }

        /// <summary>One line of text without control characters, at most maxLength characters.</summary>
        public static string CleanText(string text, int maxLength)
        {
            var sb = new StringBuilder();
            foreach (char ch in text ?? "")
            {
                sb.Append(char.IsControl(ch) ? ' ' : ch);
            }
            string clean = sb.ToString().Trim();
            return clean.Length > maxLength ? clean.Substring(0, maxLength).Trim() : clean;
        }
    }

    /// <summary>Root element of an exported profile file.</summary>
    [XmlRoot("AdbAuxProfile")]
    public class ProfileFile
    {
        public const int CurrentFormat = 1;

        public int Format = CurrentFormat;
        public string ExportedBy = "";
        public TestedProfile Profile;
    }

    /// <summary>
    /// Reads small XML files from outside the program (shared profiles, settings backups) safely:
    /// limited size, no DTD (document type definition) processing and no external references.
    /// </summary>
    public static class SafeXml
    {
        /// <param name="what">What the file should be, for messages, e.g. "an AdbAux profile".</param>
        public static T Read<T>(string path, long maxBytes, string what) where T : class
        {
            if (new FileInfo(path).Length > maxBytes)
            {
                throw new InvalidDataException("The file is larger than " + (maxBytes / 1024) + " KB, so it is not " + what + ".");
            }
            var readerSettings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            try
            {
                using (XmlReader reader = XmlReader.Create(path, readerSettings))
                {
                    var serializer = new XmlSerializer(typeof(T));
                    if (!serializer.CanDeserialize(reader))
                    {
                        throw new InvalidDataException("The file is not " + what + ".");
                    }
                    return (T)serializer.Deserialize(reader);
                }
            }
            catch (InvalidOperationException ex)
            {
                // XmlSerializer reports damaged content this way; the inner exception has the details.
                throw new InvalidDataException("The file could not be read: "
                    + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException("The file could not be read: " + ex.Message);
            }
        }

        public static void Write<T>(T content, string path)
        {
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (XmlWriter writer = XmlWriter.Create(path, settings))
            {
                new XmlSerializer(typeof(T)).Serialize(writer, content);
            }
        }
    }

    /// <summary>Export and import of single profiles as small XML files, to share tested values.</summary>
    public static class ProfileFiles
    {
        private const long MaxFileBytes = 64 * 1024;

        /// <summary>Writes the profile to a file. The link to a phone is not exported.</summary>
        public static void Export(TestedProfile profile, string path, string appVersion)
        {
            TestedProfile copy = profile.Clone();
            copy.UseWhenPhoneConnected = false;
            SafeXml.Write(new ProfileFile { ExportedBy = "AdbAux " + appVersion, Profile = copy }, path);
        }

        /// <summary>
        /// Reads a profile file. The file is checked before use (see SafeXml), and every value is
        /// brought into its valid range.
        /// </summary>
        public static TestedProfile Import(string path)
        {
            ProfileFile file = SafeXml.Read<ProfileFile>(path, MaxFileBytes, "an AdbAux profile");
            if (file == null || file.Profile == null)
            {
                throw new InvalidDataException("The file does not contain a profile.");
            }
            if (file.Format > ProfileFile.CurrentFormat)
            {
                throw new InvalidDataException("The profile was exported by a newer version of AdbAux. Update AdbAux to import it.");
            }
            TestedProfile profile = file.Profile;
            profile.UseWhenPhoneConnected = false;   // linking to a phone is a choice made on this PC
            profile.Sanitize();
            return profile;
        }
    }
}
