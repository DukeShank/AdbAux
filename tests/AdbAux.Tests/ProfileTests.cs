using System;
using System.IO;
using System.Text;
using Xunit;

namespace AdbAux.Tests
{
    /// <summary>Tested profiles: checks, export and import (including files that must be refused).</summary>
    public class ProfileTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "AdbAuxTests-" + Guid.NewGuid().ToString("N"));

        public ProfileTests()
        {
            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            Directory.Delete(folder, true);
        }

        [Fact]
        public void SanitizeBringsValuesIntoRange()
        {
            var p = new TestedProfile
            {
                Name = "  \t ",
                Comment = "line1\nline2",
                MusicBufferMs = 1,
                VideoBufferMs = 99999,
                LowLatencyBufferMs = 15,
                OutputBufferMs = 0,
                DefaultContent = (ContentType)42,
                DefaultMode = (CaptureMode)(-1),
                PhoneModel = "bad model!",
                UseWhenPhoneConnected = true,
                Saved = "yesterday"
            };

            p.Sanitize();

            Assert.Equal("Imported profile", p.Name);
            Assert.Equal("line1 line2", p.Comment);
            Assert.Equal(Limits.MinBufferMs, p.MusicBufferMs);
            Assert.Equal(Limits.MaxBufferMs, p.VideoBufferMs);
            Assert.Equal(15, p.LowLatencyBufferMs);
            Assert.Equal(Limits.MinOutputBufferMs, p.OutputBufferMs);
            Assert.Equal(ContentType.Music, p.DefaultContent);
            Assert.Equal(CaptureMode.MediaOnly, p.DefaultMode);
            Assert.Equal("", p.PhoneModel);
            Assert.False(p.UseWhenPhoneConnected);
            Assert.Equal("", p.Saved);
        }

        [Fact]
        public void CleanTextShortensLongText()
        {
            Assert.Equal(TestedProfile.MaxNameLength, TestedProfile.CleanText(new string('a', 500), TestedProfile.MaxNameLength).Length);
        }

        [Fact]
        public void ExportThenImportKeepsValuesButNotTheLink()
        {
            var original = new TestedProfile
            {
                Name = "Galaxy A36 + FiiO",
                Comment = "Amazon Music HD, YouTube",
                MusicBufferMs = 36,
                VideoBufferMs = 20,
                LowLatencyBufferMs = 15,
                OutputBufferMs = 10,
                DefaultContent = ContentType.Video,
                DefaultMode = CaptureMode.Everything,
                PhoneModel = "SM_A366E",
                UseWhenPhoneConnected = true,
                Saved = "2026-11-24"
            };
            string path = Path.Combine(folder, "profile.xml");

            ProfileFiles.Export(original, path, "0.5.0");
            TestedProfile imported = ProfileFiles.Import(path);

            Assert.Equal(original.Name, imported.Name);
            Assert.Equal(original.Comment, imported.Comment);
            Assert.Equal(36, imported.MusicBufferMs);
            Assert.Equal(ContentType.Video, imported.DefaultContent);
            Assert.Equal(CaptureMode.Everything, imported.DefaultMode);
            Assert.Equal("SM_A366E", imported.PhoneModel);
            Assert.Equal("2026-11-24", imported.Saved);
            Assert.False(imported.UseWhenPhoneConnected);
            Assert.True(original.UseWhenPhoneConnected);   // exporting does not change the profile itself
        }

        [Fact]
        public void ImportRefusesDocumentTypeDefinitions()
        {
            string path = Write("evil.xml",
                "<?xml version=\"1.0\"?><!DOCTYPE a [<!ENTITY x \"y\">]><AdbAuxProfile><Format>1</Format></AdbAuxProfile>");
            Assert.Throws<InvalidDataException>(() => ProfileFiles.Import(path));
        }

        [Fact]
        public void ImportRefusesLargeFiles()
        {
            string path = Write("large.xml", "<AdbAuxProfile>" + new string(' ', 70000) + "</AdbAuxProfile>");
            Assert.Throws<InvalidDataException>(() => ProfileFiles.Import(path));
        }

        [Fact]
        public void ImportRefusesOtherXmlAndNewerFormats()
        {
            Assert.Throws<InvalidDataException>(() => ProfileFiles.Import(Write("other.xml", "<settings/>")));
            Assert.Throws<InvalidDataException>(() => ProfileFiles.Import(Write("newer.xml",
                "<AdbAuxProfile><Format>99</Format><Profile><Name>x</Name></Profile></AdbAuxProfile>")));
        }

        private string Write(string name, string content)
        {
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            return path;
        }
    }
}
