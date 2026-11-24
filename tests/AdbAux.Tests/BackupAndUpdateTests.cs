using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace AdbAux.Tests
{
    /// <summary>Settings backup and restore, and reading scrcpy version numbers for the update check.</summary>
    public class BackupAndUpdateTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "AdbAuxTests-" + Guid.NewGuid().ToString("N"));

        public BackupAndUpdateTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        [Fact]
        public void BackupThenRestoreKeepsSettingsAndProfiles()
        {
            var settings = new AppSettings
            {
                MusicBufferMs = 36,
                ExtraArgs = "audio-output-buffer=5",
                Theme = ThemeChoice.Dark,
                NotifyConnection = true
            };
            settings.Profiles.Add(new TestedProfile { Name = "A36 + FiiO", MusicBufferMs = 36, PhoneModel = "SM_A366E", UseWhenPhoneConnected = true });
            string path = Path.Combine(folder, "backup.xml");

            SettingsBackup.Write(settings, path, "0.5.1");
            AppSettings restored = SettingsBackup.Read(path);

            Assert.True(restored.SameAdvancedSettings(settings));
            Assert.Single(restored.Profiles);
            Assert.Equal("A36 + FiiO", restored.Profiles[0].Name);
            Assert.True(restored.Profiles[0].UseWhenPhoneConnected);   // own backup: the link is kept
        }

        [Fact]
        public void BackupKeepsTheUpdateAnswerButNotThisPcsSchedule()
        {
            var settings = new AppSettings { CheckForUpdates = UpdateCheckChoice.Yes };
            settings.Updates.SkippedAdbAuxVersion = "v0.7.0";
            settings.Updates.Failures = 2;
            string path = Path.Combine(folder, "backup-updates.xml");

            SettingsBackup.Write(settings, path, "0.6.0");
            AppSettings restored = SettingsBackup.Read(path);

            Assert.Equal(UpdateCheckChoice.Yes, restored.CheckForUpdates);
            Assert.Equal("", restored.Updates.SkippedAdbAuxVersion);
            Assert.Equal(0, restored.Updates.Failures);
            Assert.Equal("v0.7.0", settings.Updates.SkippedAdbAuxVersion);   // the settings in use are not changed
        }

        [Fact]
        public void RestoreRefusesOtherFilesAndDocumentTypeDefinitions()
        {
            string profilePath = Path.Combine(folder, "profile.xml");
            ProfileFiles.Export(new TestedProfile { Name = "x" }, profilePath, "0.5.1");
            Assert.Throws<InvalidDataException>(() => SettingsBackup.Read(profilePath));

            string evil = Path.Combine(folder, "evil.xml");
            File.WriteAllText(evil, "<?xml version=\"1.0\"?><!DOCTYPE a [<!ENTITY x \"y\">]><AdbAuxSettingsBackup/>", new UTF8Encoding(false));
            Assert.Throws<InvalidDataException>(() => SettingsBackup.Read(evil));
        }

        [Fact]
        public void RestoredValuesAreBroughtIntoRange()
        {
            string path = Path.Combine(folder, "edited.xml");
            File.WriteAllText(path,
                "<AdbAuxSettingsBackup><Format>1</Format><Settings><MusicBufferMs>99999</MusicBufferMs>"
                + "<OutputBufferMs>0</OutputBufferMs></Settings></AdbAuxSettingsBackup>", new UTF8Encoding(false));

            AppSettings restored = SettingsBackup.Read(path);

            Assert.Equal(Limits.MaxBufferMs, restored.MusicBufferMs);
            Assert.Equal(Limits.MinOutputBufferMs, restored.OutputBufferMs);
        }

        [Fact]
        public void MergeReplacesSameNameAndKeepsOneAutomaticProfilePerPhone()
        {
            var target = new List<TestedProfile>
            {
                new TestedProfile { Name = "Old", PhoneModel = "SM_A366E", UseWhenPhoneConnected = true },
                new TestedProfile { Name = "Shared name", MusicBufferMs = 40 }
            };
            var incoming = new[]
            {
                new TestedProfile { Name = "shared NAME", MusicBufferMs = 36 },
                new TestedProfile { Name = "New", PhoneModel = "SM_A366E", UseWhenPhoneConnected = true }
            };

            int replaced = SettingsBackup.MergeProfiles(target, incoming);

            Assert.Equal(1, replaced);
            Assert.Equal(3, target.Count);
            Assert.Equal(36, target[1].MusicBufferMs);
            Assert.False(target[0].UseWhenPhoneConnected);   // "New" is now the automatic profile for this phone
            Assert.True(target[2].UseWhenPhoneConnected);
        }

        [Theory]
        [InlineData("scrcpy 5.0.1 <https://github.com/Genymobile/scrcpy>", "5.0.1")]
        [InlineData("v5.1", "5.1")]
        [InlineData("v3.10.2", "3.10.2")]
        public void ReadsVersionNumbers(string text, string expected)
        {
            Assert.Equal(new Version(expected), ScrcpyInstaller.ParseVersion(text));
        }

        [Fact]
        public void NoVersionNumber()
        {
            Assert.Null(ScrcpyInstaller.ParseVersion("scrcpy"));
            Assert.Null(ScrcpyInstaller.ParseVersion(null));
        }

        [Fact]
        public void DescribesManagedAndChosenScrcpyFolders()
        {
            // Both folders AdbAux installs into count as managed, with or without a trailing backslash.
            Assert.Equal("folder managed by AdbAux", ScrcpyLocator.Describe(Path.Combine(ScrcpyLocator.AppDir, "scrcpy")));
            Assert.Equal("folder managed by AdbAux", ScrcpyLocator.Describe(ScrcpyLocator.LocalDir + "\\"));
            Assert.Equal("folder you chose", ScrcpyLocator.Describe(@"C:\Tools\scrcpy"));
        }

        [Fact]
        public void NewerVersionComparesHigher()
        {
            Assert.True(ScrcpyInstaller.ParseVersion("v3.10") > ScrcpyInstaller.ParseVersion("scrcpy 3.9.2"));
        }
    }
}
