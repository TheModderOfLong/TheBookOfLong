using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace TheResourceOfLong
{
    public static class MappingRuleRegistry
    {
        private const string MappingDirectoryName = "Mapping";

        private static readonly Dictionary<string, MappingRuleEntry> EntriesByKey = new Dictionary<string, MappingRuleEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, MappingRuleEntry> EntriesById = new Dictionary<string, MappingRuleEntry>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<MappingRuleEntry> Entries = new List<MappingRuleEntry>();
        private static bool _initialized;

        public static int EntryCount { get { return Entries.Count; } }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            string modsOfLongRoot = ModResourceRegistry.ModsOfLongRoot;
            if (string.IsNullOrEmpty(modsOfLongRoot))
            {
                ModResourceRegistry.Initialize();
                modsOfLongRoot = ModResourceRegistry.ModsOfLongRoot;
            }

            List<ModProjectInfo> projects = ModDiscovery.DiscoverProjects(modsOfLongRoot);
            int resourceOrder = 0;
            foreach (ModProjectInfo project in projects)
            {
                LoadProjectRules(project, ref resourceOrder);
            }

            LoggerManager.Info("Registered " + Entries.Count + " mapping rule(s), resource id(s): " + EntriesById.Count + ".");
        }

        public static void Reload()
        {
            EntriesByKey.Clear();
            EntriesById.Clear();
            Entries.Clear();
            _initialized = false;
            Initialize();
        }

        public static bool TryGet(MappingOverrideType type, string target, out MappingRuleEntry entry)
        {
            Initialize();
            return EntriesByKey.TryGetValue(BuildKey(type, target), out entry);
        }

        public static bool TryGetById(string id, out MappingRuleEntry entry)
        {
            Initialize();
            entry = null;

            string normalizedId = NormalizeId(id);
            return !string.IsNullOrEmpty(normalizedId) && EntriesById.TryGetValue(normalizedId, out entry);
        }

        public static List<MappingRuleEntry> GetEntries(MappingOverrideType type)
        {
            Initialize();
            List<MappingRuleEntry> result = new List<MappingRuleEntry>();
            foreach (MappingRuleEntry entry in Entries)
            {
                if (entry.OverrideType == type && !string.IsNullOrWhiteSpace(entry.Target)) result.Add(entry);
            }

            return result;
        }

        private static void LoadProjectRules(ModProjectInfo project, ref int resourceOrder)
        {
            string mappingRoot = MappingRulesPathResolver.GetMappingRoot(project);
            string fallbackPath;
            string rulesPath;
            if (!MappingRulesPathResolver.TryResolveExisting(project, out rulesPath, out fallbackPath)) return;
            if (!string.Equals(rulesPath, fallbackPath, StringComparison.OrdinalIgnoreCase))
            {
                LoggerManager.Debug("MappingRules.csv loaded from external path for " + project.ModId + ", skipped fallback: " + fallbackPath);
            }
            if (!File.Exists(rulesPath)) return;

            List<string[]> rows;
            try
            {
                rows = ParseCsv(TextEncodingDetector.DecodeBest(ReadAllBytesSharedWithRetry(rulesPath)));
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to read MappingRules.csv: " + rulesPath + " - " + ex.Message);
                return;
            }

            if (rows.Count == 0) return;

            Dictionary<string, int> headers = BuildHeaderMap(rows[0]);
            int idIndex = FindColumn(headers, "\u7f16\u53f7", "id", "key", "resourceId", "resourceKey");
            int enabledIndex = FindColumn(headers, "\u542f\u7528", "enabled");
            int resourcePathIndex = FindColumn(headers, "\u8d44\u6e90\u8def\u5f84", "resourcePath", "path");
            int typeIndex = FindColumn(headers, "\u8986\u76d6\u7c7b\u578b", "type", "overrideType");
            int targetIndex = FindColumn(headers, "\u76ee\u6807", "target");
            int parametersIndex = FindColumn(headers, "\u53c2\u6570", "parameters", "params");
            int remarkIndex = FindColumn(headers, "\u5907\u6ce8", "remark", "comment");

            if (enabledIndex < 0 || resourcePathIndex < 0 || typeIndex < 0)
            {
                LoggerManager.Warning("Skipped MappingRules.csv because required columns are missing: " + rulesPath);
                return;
            }

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                if (IsEmptyRow(row)) continue;
                if (!IsEnabled(GetCell(row, enabledIndex))) continue;

                MappingOverrideType overrideType;
                if (!TryParseOverrideType(GetCell(row, typeIndex), out overrideType))
                {
                    LoggerManager.Warning("Skipped MappingRules row with invalid override type in " + rulesPath + " line " + (i + 1));
                    continue;
                }

                string id = NormalizeId(GetCell(row, idIndex));
                string target = (GetCell(row, targetIndex) ?? string.Empty).Trim();
                string resourcePath = PathUtility.NormalizeResourcePath(GetCell(row, resourcePathIndex));
                if (string.IsNullOrWhiteSpace(resourcePath))
                {
                    LoggerManager.Warning("Skipped MappingRules row with empty resource path in " + rulesPath + " line " + (i + 1));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(id))
                {
                    LoggerManager.Warning("Skipped MappingRules row with empty id and target in " + rulesPath + " line " + (i + 1));
                    continue;
                }

                MappingRuleEntry entry = new MappingRuleEntry();
                entry.Id = id;
                entry.ModId = project.ModId;
                entry.ResourcePath = resourcePath;
                entry.VirtualResourcePath = PathUtility.NormalizeResourcePath(Path.Combine(MappingDirectoryName, resourcePath));
                entry.FullSourcePath = PathUtility.CombineSafe(mappingRoot, resourcePath);
                entry.OverrideType = overrideType;
                entry.Target = target;
                entry.RawParameters = GetCell(row, parametersIndex);
                entry.Parameters = ParseParameters(entry.RawParameters);
                entry.Remark = GetCell(row, remarkIndex);
                entry.Priority = project.Priority;
                entry.HasPriority = project.HasPriority;
                entry.ProjectOrder = GetProjectOrder(project);
                entry.ResourceOrder = resourceOrder++;

                Register(entry);
            }
        }

        private static void Register(MappingRuleEntry entry)
        {
            if (entry == null) return;

            bool registered = false;

            if (!string.IsNullOrWhiteSpace(entry.Target))
            {
                string key = BuildKey(entry.OverrideType, entry.Target);
                MappingRuleEntry existing;
                if (EntriesByKey.TryGetValue(key, out existing))
                {
                    LoggerManager.Warning("Mapping override conflict: " + key + ". Keeping " + Describe(existing) + ", skipped " + Describe(entry));
                }
                else
                {
                    EntriesByKey[key] = entry;
                    registered = true;
                    LoggerManager.Debug("Registered mapping override: " + key + " -> " + Describe(entry));
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                MappingRuleEntry existingById;
                if (EntriesById.TryGetValue(entry.Id, out existingById))
                {
                    LoggerManager.Warning("Mapping resource id conflict: " + entry.Id + ". Keeping " + Describe(existingById) + ", skipped " + Describe(entry));
                }
                else
                {
                    EntriesById[entry.Id] = entry;
                    registered = true;
                    LoggerManager.Debug("Registered mapping resource id: " + entry.Id + " -> " + Describe(entry));
                }
            }

            if (registered && !Entries.Contains(entry)) Entries.Add(entry);
        }

        private static byte[] ReadAllBytesSharedWithRetry(string path)
        {
            Exception lastException = null;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (MemoryStream memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }
                }
                catch (IOException ex)
                {
                    lastException = ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                }

                Thread.Sleep(50 * (attempt + 1));
            }

            throw lastException ?? new IOException("Unable to read file.");
        }

        private static Dictionary<string, int> BuildHeaderMap(string[] headerRow)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headerRow.Length; i++)
            {
                string header = (headerRow[i] ?? string.Empty).Trim();
                if (header.Length == 0 || result.ContainsKey(header)) continue;
                result[header] = i;
            }

            return result;
        }

        private static int FindColumn(Dictionary<string, int> headers, params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                int index;
                if (headers.TryGetValue(names[i], out index)) return index;
            }

            return -1;
        }

        private static List<string[]> ParseCsv(string text)
        {
            List<string[]> rows = new List<string[]>();
            List<string> row = new List<string>();
            string field = string.Empty;
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
                            field += '"';
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field += c;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                    continue;
                }

                if (c == ',')
                {
                    row.Add(field);
                    field = string.Empty;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    row.Add(field);
                    field = string.Empty;
                    rows.Add(row.ToArray());
                    row.Clear();

                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    continue;
                }

                field += c;
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field);
                rows.Add(row.ToArray());
            }

            return rows;
        }

        private static Dictionary<string, string> ParseParameters(string raw)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(raw)) return result;

            string[] pairs = raw.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                string pair = pairs[i].Trim();
                if (pair.Length == 0) continue;

                int separator = pair.IndexOf('=');
                if (separator < 0)
                {
                    result[pair] = string.Empty;
                    continue;
                }

                string key = pair.Substring(0, separator).Trim();
                string value = pair.Substring(separator + 1).Trim();
                if (key.Length > 0) result[key] = value;
            }

            return result;
        }

        private static bool TryParseOverrideType(string raw, out MappingOverrideType type)
        {
            type = MappingOverrideType.AtlasSprite;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            int numeric;
            if (int.TryParse(raw.Trim(), out numeric) && Enum.IsDefined(typeof(MappingOverrideType), numeric))
            {
                type = (MappingOverrideType)numeric;
                return true;
            }

            return Enum.TryParse(raw.Trim(), true, out type);
        }

        private static bool IsEnabled(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string value = raw.Trim();
            return value == "1" ||
                   value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                   value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                   value == "\u662f" ||
                   value == "\u542f\u7528";
        }

        private static int GetProjectOrder(ModProjectInfo project)
        {
            return project.UsesBookLoadOrder ? project.LoadOrder : project.DiscoveryOrder;
        }

        private static bool IsEmptyRow(string[] row)
        {
            if (row == null || row.Length == 0) return true;
            for (int i = 0; i < row.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(row[i])) return false;
            }

            return true;
        }

        private static string GetCell(string[] row, int index)
        {
            if (row == null || index < 0 || index >= row.Length) return string.Empty;
            return row[index] ?? string.Empty;
        }

        private static string BuildKey(MappingOverrideType type, string target)
        {
            return type.ToString() + ":" + (target ?? string.Empty).Trim();
        }

        private static string NormalizeId(string id)
        {
            return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
        }

        private static string Describe(MappingRuleEntry entry)
        {
            return entry.ModId + "(Priority=" + (entry.HasPriority ? entry.Priority.ToString() : "missing") + ", Id=" + (string.IsNullOrEmpty(entry.Id) ? "empty" : entry.Id) + ", Source=Mapping/" + entry.ResourcePath + ")";
        }
    }
}
