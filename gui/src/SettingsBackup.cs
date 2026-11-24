using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace AdbAux
{
    /// <summary>Root element of a settings backup file.</summary>
    [XmlRoot("AdbAuxSettingsBackup")]
    public class SettingsBackupFile
    {
        public const int CurrentFormat = 1;

        public int Format = CurrentFormat;
        public string ExportedBy = "";
        public string Created = "";
        public AppSettings Settings;
    }

    /// <summary>
    /// Back up and restore of all settings and tested profiles in one file, for the user's own use
    /// (a new PC, or before experimenting). Profiles are shared with others through Export… instead.
    /// </summary>
    public static class SettingsBackup
    {
        private const long MaxFileBytes = 1024 * 1024;

        public static void Write(AppSettings settings, string path, string appVersion)
        {
            // The update-check schedule and skipped versions belong to this PC, so they are left out.
            AppSettings copy = settings.Clone();
            copy.Updates = null;
            SafeXml.Write(new SettingsBackupFile
            {
                ExportedBy = "AdbAux " + appVersion,
                Created = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Settings = copy
            }, path);
        }

        /// <summary>
        /// Reads a backup. The file is checked before use (see SafeXml), and every value is brought
        /// into its valid range. Nothing is applied here.
        /// </summary>
        public static AppSettings Read(string path)
        {
            SettingsBackupFile file = SafeXml.Read<SettingsBackupFile>(path, MaxFileBytes, "an AdbAux settings backup");
            if (file == null || file.Settings == null)
            {
                throw new InvalidDataException("The file does not contain settings.");
            }
            if (file.Format > SettingsBackupFile.CurrentFormat)
            {
                throw new InvalidDataException("The backup was made by a newer version of AdbAux. Update AdbAux to restore it.");
            }
            file.Settings.Sanitize();
            return file.Settings;
        }

        /// <summary>
        /// Adds the profiles to the list. A profile with the same name (ignoring case) is replaced.
        /// Afterwards, at most one profile per phone model is selected automatically: the last one merged.
        /// Returns the number of profiles that were replaced.
        /// </summary>
        public static int MergeProfiles(List<TestedProfile> target, IEnumerable<TestedProfile> incoming)
        {
            int replaced = 0;
            foreach (TestedProfile p in incoming)
            {
                int index = target.FindIndex(x => string.Equals(x.Name, p.Name, StringComparison.CurrentCultureIgnoreCase));
                if (index >= 0)
                {
                    target[index] = p;
                    replaced++;
                }
                else
                {
                    target.Add(p);
                }

                if (p.UseWhenPhoneConnected)
                {
                    foreach (TestedProfile other in target)
                    {
                        if (other != p && other.UseWhenPhoneConnected
                            && string.Equals(other.PhoneModel, p.PhoneModel, StringComparison.OrdinalIgnoreCase))
                        {
                            other.UseWhenPhoneConnected = false;
                        }
                    }
                }
            }
            return replaced;
        }
    }
}
