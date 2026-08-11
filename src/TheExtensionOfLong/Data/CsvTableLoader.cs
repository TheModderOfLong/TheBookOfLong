using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TheExtensionOfLong
{
    public static class CsvTableLoader
    {
        public static string GetDataTablePath(ModProjectInfo project, CsvTableDefinition definition)
        {
            if (project == null || definition == null) return string.Empty;
            return Path.Combine(project.DataDirectory ?? string.Empty, definition.FileName ?? string.Empty);
        }

        public static bool TryLoad(ModProjectInfo project, CsvTableDefinition definition, out CsvTable table)
        {
            table = null;
            string path = GetDataTablePath(project, definition);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                if (definition != null && definition.Required)
                    LoggerManager.Warning("CsvTableLoader: 必需表不存在: " + path);
                return false;
            }

            try
            {
                string text = ReadAllTextSharedWithRetry(path);
                List<string[]> records = CsvTextUtility.Parse(text);
                if (records.Count == 0)
                {
                    LoggerManager.Warning("CsvTableLoader: CSV为空: " + path);
                    return false;
                }

                string[] headers = records[0];
                records.RemoveAt(0);
                table = new CsvTable(project, path, headers, records);
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("CsvTableLoader: 读取CSV失败: " + path + " - " + ex.Message);
                return false;
            }
        }

        private static string ReadAllTextSharedWithRetry(string path)
        {
            Exception last = null;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    byte[] bytes;
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        bytes = new byte[stream.Length];
                        int offset = 0;
                        while (offset < bytes.Length)
                        {
                            int read = stream.Read(bytes, offset, bytes.Length - offset);
                            if (read <= 0) break;
                            offset += read;
                        }
                    }

                    return DecodeText(bytes);
                }
                catch (IOException ex)
                {
                    last = ex;
                    System.Threading.Thread.Sleep(30 * (i + 1));
                }
            }

            throw last ?? new IOException("读取失败");
        }

        private static string DecodeText(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                return new UTF8Encoding(false, true).GetString(bytes, 3, bytes.Length - 3);

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.Default.GetString(bytes);
            }
        }

    }
}
