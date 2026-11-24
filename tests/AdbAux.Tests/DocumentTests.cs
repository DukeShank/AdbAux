using System.IO;
using Xunit;

namespace AdbAux.Tests
{
    /// <summary>The privacy statement and licence built into AdbAux.exe, and their conversion to plain text.</summary>
    public class DocumentTests
    {
        [Theory]
        [InlineData("AdbAux.PRIVACY.md", "privacy statement")]
        [InlineData("AdbAux.LICENSE", "MIT License")]
        public void DocumentsAreBuiltIntoTheExe(string resourceName, string expectedText)
        {
            using (Stream stream = typeof(AppSettings).Assembly.GetManifestResourceStream(resourceName))
            {
                Assert.NotNull(stream);
                string text = new StreamReader(stream).ReadToEnd();
                Assert.Contains(expectedText, text);
            }
        }

        [Fact]
        public void MarkdownBecomesPlainText()
        {
            string markdown = "# Title\n\nFirst line\ncontinues **here** with `code`.\n\n- Bullet one\n  wraps on.\n- [Link](https://example.org)\n";

            string text = PlainText.FromMarkdown(markdown);

            Assert.Equal("Title\r\n\r\nFirst line continues here with code.\r\n\r\n• Bullet one wraps on.\r\n• Link (https://example.org)", text);
        }
    }
}
