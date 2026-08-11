using System.Collections.Generic;
using System.Text;

namespace TheExtensionOfLong
{
    public static class CsvTextUtility
    {
        public static List<string[]> Parse(string text)
        {
            List<string[]> rows = new List<string[]>();
            if (string.IsNullOrEmpty(text)) return rows;

            List<string> row = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(cell.ToString());
                    cell.Length = 0;
                    rows.Add(row.ToArray());
                    row.Clear();
                }
                else
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }

        public static string Serialize(List<string[]> rows)
        {
            if (rows == null || rows.Count == 0) return string.Empty;

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) builder.AppendLine();
                string[] row = rows[i] ?? new string[0];
                for (int j = 0; j < row.Length; j++)
                {
                    if (j > 0) builder.Append(',');
                    builder.Append(EscapeCell(row[j]));
                }
            }

            return builder.ToString();
        }

        private static string EscapeCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needQuotes = value.IndexOf(',') >= 0
                || value.IndexOf('"') >= 0
                || value.IndexOf('\r') >= 0
                || value.IndexOf('\n') >= 0;
            if (!needQuotes) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
