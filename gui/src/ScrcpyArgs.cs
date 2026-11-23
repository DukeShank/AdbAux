using System;
using System.Collections.Generic;
using System.Text;

namespace AdbAux
{
    /// <summary>Builds the scrcpy command line (options explained in README.md).</summary>
    public static class ScrcpyArgs
    {
        public static string Build(string serial, int bufferMs, int outputBufferMs, CaptureMode mode, string extraOptions)
        {
            var args = new List<string>
            {
                "--serial=" + serial,          // this exact phone (devices on adb over Wi-Fi are ignored)
                "--no-window",                 // audio only: no video, no input control
                "--require-audio",             // exit with an error if audio capture fails
                "--audio-codec=raw",           // uncompressed 16-bit PCM, 48 kHz stereo
                "--audio-buffer=" + bufferMs,
                "--audio-output-buffer=" + outputBufferMs
            };

            switch (mode)
            {
                case CaptureMode.Everything:
                    args.Add("--audio-source=playback");
                    break;
                case CaptureMode.Duplicate:
                    args.Add("--audio-dup");
                    break;
                default:
                    args.Add("--audio-source=output");
                    break;
            }

            foreach (string option in SplitOptions(extraOptions))
            {
                args.Add(option.StartsWith("-", StringComparison.Ordinal) ? option : "--" + option);
            }

            var line = new StringBuilder();
            foreach (string arg in args)
            {
                if (line.Length > 0)
                {
                    line.Append(' ');
                }
                line.Append(Quote(arg));
            }
            return line.ToString();
        }

        /// <summary>
        /// Splits the "Extra options" text at spaces and tabs. Text in double quotes stays together
        /// (the quotes are removed), so a value can contain spaces: record="C:\My Music\a.wav".
        /// </summary>
        public static List<string> SplitOptions(string text)
        {
            var options = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;
            foreach (char c in text ?? "")
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if ((c == ' ' || c == '\t') && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        options.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            if (current.Length > 0)
            {
                options.Add(current.ToString());
            }
            return options;
        }

        /// <summary>
        /// Puts an argument in double quotes when it contains a space or tab, following the Windows
        /// command-line rules: backslashes just before the closing quote are doubled, so they do not
        /// cancel it. Arguments from SplitOptions contain no double quotes.
        /// </summary>
        public static string Quote(string arg)
        {
            if (arg.IndexOfAny(new[] { ' ', '\t' }) < 0)
            {
                return arg;
            }
            int trailingBackslashes = 0;
            for (int i = arg.Length - 1; i >= 0 && arg[i] == '\\'; i--)
            {
                trailingBackslashes++;
            }
            return "\"" + arg + new string('\\', trailingBackslashes) + "\"";
        }
    }
}
