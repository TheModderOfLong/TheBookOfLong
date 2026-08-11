using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader;
using Newtonsoft.Json;

namespace TheExtensionOfLong
{
    internal static class ModLoadConfigService
    {
        public const string DefaultDescription = "修改本文件后，需要完全重启游戏才能应用新的 Mod 加载配置。Mods 数组中的顺序就是加载顺序，越靠后覆盖能力越强。";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static ModLoadConfigDocument LoadDocument()
        {
            ModLoadConfigDocument document = CreateDocumentSkeleton();
            Dictionary<string, ModLoadConfigEntry> scanned = ScanModDirectories(document.ModsOfLongRoot);
            ModLoadConfigFile existing = ReadConfigFile(document.ConfigPath);

            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (existing != null && existing.Mods != null)
            {
                for (int i = 0; i < existing.Mods.Count; i++)
                {
                    ModLoadConfigEntry oldEntry = existing.Mods[i];
                    if (oldEntry == null || string.IsNullOrWhiteSpace(oldEntry.FolderName)) continue;

                    ModLoadConfigEntry scannedEntry;
                    if (!scanned.TryGetValue(oldEntry.FolderName, out scannedEntry)) continue;

                    scannedEntry.Enabled = oldEntry.Enabled;
                    document.Entries.Add(scannedEntry);
                    used.Add(scannedEntry.FolderName);
                }
            }

            List<ModLoadConfigEntry> newEntries = new List<ModLoadConfigEntry>();
            foreach (KeyValuePair<string, ModLoadConfigEntry> pair in scanned)
            {
                if (!used.Contains(pair.Key))
                {
                    newEntries.Add(pair.Value);
                }
            }

            newEntries.Sort(delegate(ModLoadConfigEntry left, ModLoadConfigEntry right)
            {
                return string.Compare(left.FolderName, right.FolderName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < newEntries.Count; i++)
            {
                document.Entries.Add(newEntries[i]);
            }

            if (!File.Exists(document.ConfigPath))
            {
                document.IsDirty = document.Entries.Count > 0;
                document.LastMessage = "未找到配置文件，保存后会创建新的 TheBookOfLong.ModLoadConfig.json。";
            }
            else
            {
                document.LastMessage = "已加载配置。";
            }

            return document;
        }

        public static void SaveDocument(ModLoadConfigDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");

            Directory.CreateDirectory(Path.GetDirectoryName(document.ConfigPath));

            ModLoadConfigFile file = new ModLoadConfigFile();
            file.Mods = new List<ModLoadConfigEntry>();
            for (int i = 0; i < document.Entries.Count; i++)
            {
                ModLoadConfigEntry source = document.Entries[i];
                if (source == null || string.IsNullOrWhiteSpace(source.FolderName)) continue;

                file.Mods.Add(new ModLoadConfigEntry
                {
                    FolderName = source.FolderName,
                    DisplayName = source.DisplayName,
                    Version = string.IsNullOrWhiteSpace(source.Version) ? "unspecified" : source.Version,
                    Enabled = source.Enabled
                });
            }

            string json = JsonConvert.SerializeObject(file, Formatting.Indented);
            string tempPath = document.ConfigPath + ".tmp";
            string backupPath = document.ConfigPath + ".bak";

            File.WriteAllText(tempPath, json, Utf8NoBom);
            if (File.Exists(document.ConfigPath))
            {
                File.Copy(document.ConfigPath, backupPath, true);
                File.Delete(document.ConfigPath);
            }

            File.Move(tempPath, document.ConfigPath);
            document.IsDirty = false;
            document.LastMessage = "已保存。新的启用状态和加载顺序需要完全重启游戏后生效。";
        }

        private static ModLoadConfigDocument CreateDocumentSkeleton()
        {
            string modsRoot = ResolveModsRoot();
            string gameRoot = ResolveGameRoot(modsRoot);
            string modsOfLongRoot = Path.Combine(modsRoot, "ModsOfLong");
            string userDataRoot = Path.Combine(gameRoot, "UserData");

            Directory.CreateDirectory(userDataRoot);
            Directory.CreateDirectory(modsOfLongRoot);

            ModLoadConfigDocument document = new ModLoadConfigDocument();
            document.GameRoot = gameRoot;
            document.ModsRoot = modsRoot;
            document.ModsOfLongRoot = modsOfLongRoot;
            document.ConfigPath = Path.Combine(userDataRoot, "TheBookOfLong.ModLoadConfig.json");
            return document;
        }

        private static string ResolveModsRoot()
        {
            try
            {
                string melonMods = MelonLoader.Utils.MelonEnvironment.ModsDirectory;
                if (!string.IsNullOrWhiteSpace(melonMods))
                {
                    return melonMods;
                }
            }
            catch
            {
            }

            string gameRoot = ResolveGameRoot(null);
            return Path.Combine(gameRoot, "Mods");
        }

        private static string ResolveGameRoot(string modsRoot)
        {
            if (!string.IsNullOrWhiteSpace(modsRoot))
            {
                DirectoryInfo parent = Directory.GetParent(modsRoot);
                if (parent != null) return parent.FullName;
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string found = TryFindGameRoot(baseDirectory);
            if (!string.IsNullOrWhiteSpace(found)) return found;

            string current = Directory.GetCurrentDirectory();
            found = TryFindGameRoot(current);
            return string.IsNullOrWhiteSpace(found) ? current : found;
        }

        private static string TryFindGameRoot(string start)
        {
            if (string.IsNullOrWhiteSpace(start)) return null;

            DirectoryInfo current = new DirectoryInfo(start);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "Mods")) ||
                    Directory.Exists(Path.Combine(current.FullName, "LongYinLiZhiZhuan_Data")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return null;
        }

        private static ModLoadConfigFile ReadConfigFile(string configPath)
        {
            if (!File.Exists(configPath)) return null;

            try
            {
                string json = File.ReadAllText(configPath, Encoding.UTF8);
                return JsonConvert.DeserializeObject<ModLoadConfigFile>(json);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigService: 读取配置失败，将按当前目录重新生成列表 - " + ex.Message);
                return null;
            }
        }

        private static Dictionary<string, ModLoadConfigEntry> ScanModDirectories(string modsOfLongRoot)
        {
            Dictionary<string, ModLoadConfigEntry> result = new Dictionary<string, ModLoadConfigEntry>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(modsOfLongRoot))
            {
                return result;
            }

            DirectoryInfo root = new DirectoryInfo(modsOfLongRoot);
            DirectoryInfo[] directories = root.GetDirectories("mod*");
            for (int i = 0; i < directories.Length; i++)
            {
                DirectoryInfo directory = directories[i];
                ModInfoFile info = ReadInfoFile(Path.Combine(directory.FullName, "Info.json"));

                ModLoadConfigEntry entry = new ModLoadConfigEntry();
                entry.FolderName = directory.Name;
                entry.DisplayName = ResolveDisplayName(directory.Name, info);
                entry.Version = info == null || string.IsNullOrWhiteSpace(info.Version) ? "unspecified" : info.Version.Trim();
                entry.Author = info == null || string.IsNullOrWhiteSpace(info.Author) ? string.Empty : info.Author.Trim();
                entry.Desc = info == null || string.IsNullOrWhiteSpace(info.Desc) ? string.Empty : NormalizeInfoText(info.Desc);
                entry.Enabled = true;
                result[entry.FolderName] = entry;
            }

            return result;
        }

        private static ModInfoFile ReadInfoFile(string infoPath)
        {
            if (!File.Exists(infoPath)) return null;

            try
            {
                return JsonConvert.DeserializeObject<ModInfoFile>(File.ReadAllText(infoPath, Encoding.UTF8));
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("ModLoadConfigService: 读取 Info.json 失败 " + infoPath + " - " + ex.Message);
                return null;
            }
        }

        private static string ResolveDisplayName(string folderName, ModInfoFile info)
        {
            if (info != null && !string.IsNullOrWhiteSpace(info.Name))
            {
                return info.Name.Trim();
            }

            string value = folderName ?? string.Empty;
            if (value.StartsWith("mod", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(3);
            }

            value = value.Trim(' ', '_', '-');
            return string.IsNullOrWhiteSpace(value) ? folderName : value;
        }

        private static string NormalizeInfoText(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Trim().Replace("\\n", "\n");
        }
    }
}
