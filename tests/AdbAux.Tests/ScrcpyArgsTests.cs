using Xunit;

namespace AdbAux.Tests
{
    /// <summary>The scrcpy command line built by AdbAux.</summary>
    public class ScrcpyArgsTests
    {
        private const string Base = "--serial=ABC --no-window --require-audio --audio-codec=raw --audio-buffer=40 --audio-output-buffer=10";

        [Fact]
        public void MediaOnly()
        {
            Assert.Equal(Base + " --audio-source=output", ScrcpyArgs.Build("ABC", 40, 10, CaptureMode.MediaOnly, ""));
        }

        [Fact]
        public void AllSoundsAndBoth()
        {
            Assert.Equal(Base + " --audio-source=playback", ScrcpyArgs.Build("ABC", 40, 10, CaptureMode.Everything, null));
            Assert.Equal(Base + " --audio-dup", ScrcpyArgs.Build("ABC", 40, 10, CaptureMode.Duplicate, "  "));
        }

        [Fact]
        public void ExtraOptionsGetLeadingDashes()
        {
            string line = ScrcpyArgs.Build("ABC", 40, 10, CaptureMode.MediaOnly, "audio-output-buffer=5  --no-clipboard-autosync");
            Assert.EndsWith(" --audio-source=output --audio-output-buffer=5 --no-clipboard-autosync", line);
        }

        [Fact]
        public void QuotedValueKeepsItsSpaces()
        {
            Assert.Equal(new[] { "record=C:\\My Music\\a.wav", "x" }, ScrcpyArgs.SplitOptions("record=\"C:\\My Music\\a.wav\" x").ToArray());

            string line = ScrcpyArgs.Build("ABC", 40, 10, CaptureMode.MediaOnly, "record=\"C:\\My Music\\a.wav\"");
            Assert.EndsWith(" \"--record=C:\\My Music\\a.wav\"", line);
        }

        [Fact]
        public void QuoteDoublesBackslashesBeforeTheClosingQuote()
        {
            Assert.Equal("plain", ScrcpyArgs.Quote("plain"));
            Assert.Equal("\"C:\\My Dir\\\\\"", ScrcpyArgs.Quote("C:\\My Dir\\"));
        }
    }
}
