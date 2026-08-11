using System;
using System.Collections.Generic;

namespace TheExtensionOfLong
{
    public sealed class CsvTable
    {
        public CsvTable(ModProjectInfo project, string filePath, string[] headers, List<string[]> rows)
        {
            Project = project;
            FilePath = filePath;
            Headers = headers ?? new string[0];
            Rows = rows ?? new List<string[]>();
            HeaderMap = BuildHeaderMap(Headers);
        }

        public ModProjectInfo Project { get; private set; }
        public string FilePath { get; private set; }
        public string[] Headers { get; private set; }
        public List<string[]> Rows { get; private set; }
        public Dictionary<string, int> HeaderMap { get; private set; }

        public int FindColumn(params string[] names)
        {
            if (names == null) return -1;

            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                if (string.IsNullOrWhiteSpace(name)) continue;

                int index;
                if (HeaderMap.TryGetValue(name.Trim(), out index))
                    return index;
            }

            return -1;
        }

        public static string GetCell(string[] row, int index)
        {
            if (row == null || index < 0 || index >= row.Length)
                return string.Empty;

            return row[index] == null ? string.Empty : row[index].Trim();
        }

        public static bool IsEmptyRow(string[] row)
        {
            if (row == null || row.Length == 0) return true;

            for (int i = 0; i < row.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i]))
                    return false;
            }

            return true;
        }

        private static Dictionary<string, int> BuildHeaderMap(string[] headers)
        {
            Dictionary<string, int> map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                string header = headers[i];
                if (string.IsNullOrWhiteSpace(header)) continue;

                header = header.Trim();
                if (!map.ContainsKey(header))
                    map.Add(header, i);
            }

            return map;
        }
    }
}
