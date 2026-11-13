using System;
using System.IO;
using System.Text;

namespace NoteZero
{
    /// <summary>
    /// A UTF-8 text file on disk: remembers BOM and line-ending style, and writes safely
    /// (temp file in the same folder, then atomic replace).
    /// </summary>
    internal sealed class TextFile
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly byte[] Bom = { 0xEF, 0xBB, 0xBF };

        public string FullPath { get; private set; }
        public bool HasBom { get; private set; }
        /// <summary>"\r\n", "\n" or "\r".</summary>
        public string NewLine { get; private set; }

        public string FileName
        {
            get { return Path.GetFileName(FullPath); }
        }

        private TextFile() { }

        /// <summary>Reads the file. Throws <see cref="InvalidDataException"/> if it is not valid UTF-8.</summary>
        public static TextFile Load(string fullPath, out string text)
        {
            byte[] bytes = File.ReadAllBytes(fullPath);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            int offset = bom ? 3 : 0;
            try
            {
                text = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                throw new InvalidDataException("Not valid UTF-8.");
            }

            var file = new TextFile();
            file.FullPath = fullPath;
            file.HasBom = bom;
            file.NewLine = DetectNewLine(text);
            return file;
        }

        /// <summary>Most frequent line ending in the text; CRLF when there is none.</summary>
        private static string DetectNewLine(string s)
        {
            int crlf = 0, lf = 0, cr = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\r')
                {
                    if (i + 1 < s.Length && s[i + 1] == '\n') { crlf++; i++; }
                    else cr++;
                }
                else if (c == '\n')
                {
                    lf++;
                }
            }
            if (lf > crlf && lf >= cr) return "\n";
            if (cr > crlf && cr > lf) return "\r";
            return "\r\n";
        }

        /// <summary>Normalizes every line ending (CRLF, CR, LF) to LF.</summary>
        public static string ToLf(string s)
        {
            if (s.IndexOf('\r') < 0) return s;
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        /// <summary>Writes LF-normalized text using the file's original line ending and BOM.</summary>
        public void Save(string lfText)
        {
            string body = NewLine == "\n" ? lfText : lfText.Replace("\n", NewLine);
            byte[] data = StrictUtf8.GetBytes(body);

            bool exists = File.Exists(FullPath);
            if (exists && (File.GetAttributes(FullPath) & FileAttributes.ReadOnly) != 0)
                throw new UnauthorizedAccessException("檔案設為唯讀。");

            string dir = Path.GetDirectoryName(FullPath);
            string temp = Path.Combine(dir,
                "." + FileName + "." + Guid.NewGuid().ToString("N").Substring(0, 8) + ".notezero.tmp");
            try
            {
                using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                               4096, FileOptions.WriteThrough))
                {
                    if (HasBom) fs.Write(Bom, 0, Bom.Length);
                    fs.Write(data, 0, data.Length);
                    fs.Flush(true);
                }

                if (exists)
                    File.Replace(temp, FullPath, null, true); // keeps attributes, ACL, creation time
                else
                    File.Move(temp, FullPath);                // original was deleted meanwhile
            }
            catch
            {
                try { if (File.Exists(temp)) File.Delete(temp); }
                catch { }
                throw;
            }
        }

        /// <summary>Human-readable reason for an I/O failure.</summary>
        public static string Describe(Exception ex)
        {
            const int ERROR_SHARING_VIOLATION = unchecked((int)0x80070020);
            const int ERROR_LOCK_VIOLATION = unchecked((int)0x80070021);
            const int ERROR_DISK_FULL = unchecked((int)0x80070070);

            if (ex is UnauthorizedAccessException)
                return "沒有寫入權限，或檔案為唯讀。\n" + ex.Message;
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException)
                return "找不到檔案或資料夾。\n" + ex.Message;
            if (ex is PathTooLongException)
                return "路徑太長。";
            if (ex is IOException)
            {
                if (ex.HResult == ERROR_SHARING_VIOLATION || ex.HResult == ERROR_LOCK_VIOLATION)
                    return "檔案正被其他程式使用。";
                if (ex.HResult == ERROR_DISK_FULL)
                    return "磁碟空間不足。";
            }
            return ex.Message;
        }
    }
}
