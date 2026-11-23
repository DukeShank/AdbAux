using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AdbAux
{
    /// <summary>
    /// Shows the AdbAux icon in the Windows volume mixer.
    ///
    /// The volume mixer labels an audio stream with the icon of the program that plays it, and that
    /// program is scrcpy.exe (AdbAux only starts it). So AdbAux keeps a copy of scrcpy.exe named
    /// scrcpy-adbaux.exe in the same folder, in which only the icon resources are replaced with the
    /// AdbAux icon, and runs that copy. scrcpy.exe itself is never changed, and the copy uses the same
    /// DLLs and scrcpy-server file next to it.
    ///
    /// The copy is made only in a scrcpy folder AdbAux manages (never in a folder the user chose).
    /// The SHA-256 of the scrcpy.exe it was made from is kept in scrcpy-adbaux.sha256; when scrcpy.exe
    /// changes, the copy is made again. If anything fails, scrcpy.exe is used.
    /// </summary>
    internal static class MixerIcon
    {
        public const string CopyName = "scrcpy-adbaux.exe";
        private const string SourceHashName = "scrcpy-adbaux.sha256";

        private const int RtIcon = 3;
        private const int RtGroupIcon = 14;
        private const uint LoadLibraryAsDatafile = 0x00000002;
        private const uint LoadLibraryAsImageResource = 0x00000020;

        /// <summary>The scrcpy program to run: the copy with the AdbAux icon if wanted and possible, otherwise scrcpy.exe.</summary>
        public static string ExeToRun(string scrcpyDir, bool wanted, Action<string> log)
        {
            string original = Path.Combine(scrcpyDir, "scrcpy.exe");
            if (!wanted || !ScrcpyLocator.IsManagedFolder(scrcpyDir))
            {
                return original;
            }

            string copy = Path.Combine(scrcpyDir, CopyName);
            string hashFile = Path.Combine(scrcpyDir, SourceHashName);
            try
            {
                string hash = Sha256(original);
                if (File.Exists(copy) && File.Exists(hashFile) && File.ReadAllText(hashFile).Trim() == hash)
                {
                    return copy;   // made earlier from the same scrcpy.exe
                }

                byte[] ico = ReadAppIcon();
                string temp = copy + ".tmp";
                File.Copy(original, temp, true);
                try
                {
                    ReplaceIcon(temp, ico);
                    if (File.Exists(copy))
                    {
                        File.Delete(copy);
                    }
                    File.Move(temp, copy);
                    File.WriteAllText(hashFile, hash);
                }
                finally
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                log("Made " + CopyName + " (a copy of scrcpy.exe with the AdbAux icon, for the Windows volume mixer).");
                return copy;
            }
            catch (Exception ex)
            {
                log("Could not make " + CopyName + " (" + ex.Message + "); using scrcpy.exe, so the volume mixer shows the scrcpy icon.");
                return original;
            }
        }

        private static string Sha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
        }

        private static byte[] ReadAppIcon()
        {
            using (Stream stream = typeof(MixerIcon).Assembly.GetManifestResourceStream("AdbAux.app.ico"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("the AdbAux icon is missing from the program");
                }
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
        }

        /// <summary>A resource name: a number (ID) or a text name.</summary>
        private sealed class ResName
        {
            public int Id = -1;
            public string Text;
        }

        /// <summary>Replaces all icon resources of an exe with the images of an .ico file.</summary>
        private static void ReplaceIcon(string exePath, byte[] ico)
        {
            // 1. Read which icon resources the exe has (names and languages).
            var groups = new List<KeyValuePair<ResName, ushort>>();
            var icons = new List<KeyValuePair<ResName, ushort>>();
            IntPtr module = LoadLibraryEx(exePath, IntPtr.Zero, LoadLibraryAsDatafile | LoadLibraryAsImageResource);
            if (module == IntPtr.Zero)
            {
                throw new IOException("could not open the copy (error " + Marshal.GetLastWin32Error() + ")");
            }
            try
            {
                ListResources(module, RtGroupIcon, groups);
                ListResources(module, RtIcon, icons);
            }
            finally
            {
                FreeLibrary(module);
            }

            // The new icon takes the name and language of the exe's first icon group, which is the one
            // Windows shows for the file.
            ResName groupName = groups.Count > 0 ? groups[0].Key : new ResName { Id = 1 };
            ushort language = groups.Count > 0 ? groups[0].Value : (ushort)0;

            // 2. Parse the .ico file: ICONDIR header (6 bytes), then one 16-byte entry per image.
            int count = BitConverter.ToUInt16(ico, 4);
            var group = new MemoryStream();
            var writer = new BinaryWriter(group);
            writer.Write((ushort)0);       // reserved
            writer.Write((ushort)1);       // type: icon
            writer.Write((ushort)count);
            var images = new List<byte[]>();
            for (int i = 0; i < count; i++)
            {
                int e = 6 + i * 16;
                int size = BitConverter.ToInt32(ico, e + 8);
                int offset = BitConverter.ToInt32(ico, e + 12);
                var image = new byte[size];
                Buffer.BlockCopy(ico, offset, image, 0, size);
                images.Add(image);
                writer.Write(ico, e, 12);     // width, height, colours, reserved, planes, bit count, size
                writer.Write((ushort)(i + 1)); // resource ID of the image
            }
            writer.Flush();

            // 3. Remove the old icon resources and add the new ones, in one update.
            IntPtr update = BeginUpdateResource(exePath, false);
            if (update == IntPtr.Zero)
            {
                throw new IOException("could not change the copy (error " + Marshal.GetLastWin32Error() + ")");
            }
            bool ok = false;
            try
            {
                foreach (KeyValuePair<ResName, ushort> old in groups)
                {
                    Update(update, RtGroupIcon, old.Key, old.Value, null);
                }
                foreach (KeyValuePair<ResName, ushort> old in icons)
                {
                    Update(update, RtIcon, old.Key, old.Value, null);
                }
                for (int i = 0; i < images.Count; i++)
                {
                    Update(update, RtIcon, new ResName { Id = i + 1 }, language, images[i]);
                }
                Update(update, RtGroupIcon, groupName, language, group.ToArray());
                ok = true;
            }
            finally
            {
                if (!EndUpdateResource(update, !ok) && ok)
                {
                    throw new IOException("could not save the copy (error " + Marshal.GetLastWin32Error() + ")");
                }
            }
        }

        private static void ListResources(IntPtr module, int type, List<KeyValuePair<ResName, ushort>> found)
        {
            var names = new List<ResName>();
            EnumResNameProc nameProc = (m1, t1, n1, p1) =>
            {
                names.Add(ToResName(n1));
                return true;
            };
            EnumResourceNames(module, (IntPtr)type, nameProc, IntPtr.Zero);   // false = none of this type
            GC.KeepAlive(nameProc);

            foreach (ResName name in names)
            {
                ResName current = name;
                EnumResLangProc langProc = (m2, t2, n2, lang, p2) =>
                {
                    found.Add(new KeyValuePair<ResName, ushort>(current, lang));
                    return true;
                };
                IntPtr namePtr = NamePointer(current);
                try
                {
                    EnumResourceLanguages(module, (IntPtr)type, namePtr, langProc, IntPtr.Zero);
                }
                finally
                {
                    FreeName(current, namePtr);
                }
                GC.KeepAlive(langProc);
            }
        }

        private static void Update(IntPtr update, int type, ResName name, ushort language, byte[] data)
        {
            IntPtr namePtr = NamePointer(name);
            try
            {
                if (!UpdateResource(update, (IntPtr)type, namePtr, language, data, data != null ? (uint)data.Length : 0))
                {
                    throw new IOException("could not write an icon resource (error " + Marshal.GetLastWin32Error() + ")");
                }
            }
            finally
            {
                FreeName(name, namePtr);
            }
        }

        // A resource name is either a number below 65536 passed as the pointer value itself, or a
        // pointer to a text.
        private static ResName ToResName(IntPtr name)
        {
            long value = name.ToInt64();
            return value >= 0 && value <= 0xFFFF
                ? new ResName { Id = (int)value }
                : new ResName { Text = Marshal.PtrToStringUni(name) };
        }

        private static IntPtr NamePointer(ResName name)
        {
            return name.Text == null ? (IntPtr)name.Id : Marshal.StringToHGlobalUni(name.Text);
        }

        private static void FreeName(ResName name, IntPtr pointer)
        {
            if (name.Text != null)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        private delegate bool EnumResNameProc(IntPtr module, IntPtr type, IntPtr name, IntPtr param);
        private delegate bool EnumResLangProc(IntPtr module, IntPtr type, IntPtr name, ushort language, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string fileName, IntPtr reserved, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr module);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumResourceNames(IntPtr module, IntPtr type, EnumResNameProc callback, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumResourceLanguages(IntPtr module, IntPtr type, IntPtr name, EnumResLangProc callback, IntPtr param);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr BeginUpdateResource(string fileName, bool deleteExisting);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool EndUpdateResource(IntPtr update, bool discard);
    }
}
