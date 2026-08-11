using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TheResourceOfLong
{
    public static class UserConfigManager
    {
        private const string ConfigDirectoryName = "TheResourceOfLong";
        private const string ConfigFileName = "config.json";

        public static string GetConfigDirectoryPath(string gameRoot)
        {
            string safeGameRoot = string.IsNullOrWhiteSpace(gameRoot) ? Directory.GetCurrentDirectory() : gameRoot;
            return Path.Combine(safeGameRoot, "UserData", ConfigDirectoryName);
        }

        public static string GetConfigPath(string gameRoot)
        {
            return Path.Combine(GetConfigDirectoryPath(gameRoot), ConfigFileName);
        }

        public static ResourceProbeConfig LoadOrCreate(string gameRoot)
        {
            string directoryPath = GetConfigDirectoryPath(gameRoot);
            string configPath = Path.Combine(directoryPath, ConfigFileName);
            Directory.CreateDirectory(directoryPath);

            ResourceProbeConfig defaults = ResourceProbeConfig.CreateDefault();
            if (!File.Exists(configPath))
            {
                File.WriteAllText(configPath, SimpleJson.Serialize(defaults), Encoding.UTF8);
                return defaults;
            }

            try
            {
                Dictionary<string, object> json = SimpleJson.ParseObject(File.ReadAllText(configPath));
                ResourceProbeConfig config = new ResourceProbeConfig();
                config.EnableResourceProbe = SimpleJson.GetBool(json, "EnableResourceProbe", defaults.EnableResourceProbe);
                config.LogMisses = SimpleJson.GetBool(json, "LogMisses", defaults.LogMisses);
                config.LogStackTrace = SimpleJson.GetBool(json, "LogStackTrace", defaults.LogStackTrace);
                config.LogLoadAll = SimpleJson.GetBool(json, "LogLoadAll", defaults.LogLoadAll);
                config.EnableContainerProbe = SimpleJson.GetBool(json, "EnableContainerProbe", defaults.EnableContainerProbe);
                config.EnableContainerProbeOverlay = SimpleJson.GetBool(json, "EnableContainerProbeOverlay", defaults.EnableContainerProbeOverlay);
                config.EnableScenePrefabUiProbe = SimpleJson.GetBool(json, "EnableScenePrefabUiProbe", defaults.EnableScenePrefabUiProbe);
                config.EnableResourceManifestGenerator = SimpleJson.GetBool(json, "EnableResourceManifestGenerator", defaults.EnableResourceManifestGenerator);
                config.EnableMappingRulesGenerator = SimpleJson.GetBool(json, "EnableMappingRulesGenerator", defaults.EnableMappingRulesGenerator);
                config.MaxStackTraceLength = SimpleJson.GetInt(json, "MaxStackTraceLength", defaults.MaxStackTraceLength);
                if (config.MaxStackTraceLength <= 0) config.MaxStackTraceLength = defaults.MaxStackTraceLength;

                if (!HasKey(json, "EnableContainerProbe"))
                {
                    AppendBooleanProperty(configPath, "EnableContainerProbe", config.EnableContainerProbe);
                }

                if (!HasKey(json, "EnableContainerProbeOverlay"))
                {
                    AppendBooleanProperty(configPath, "EnableContainerProbeOverlay", config.EnableContainerProbeOverlay);
                }

                if (!HasKey(json, "EnableScenePrefabUiProbe"))
                {
                    AppendBooleanProperty(configPath, "EnableScenePrefabUiProbe", config.EnableScenePrefabUiProbe);
                }

                if (!HasKey(json, "EnableResourceManifestGenerator"))
                {
                    AppendBooleanProperty(configPath, "EnableResourceManifestGenerator", config.EnableResourceManifestGenerator);
                }

                if (!HasKey(json, "EnableMappingRulesGenerator"))
                {
                    AppendBooleanProperty(configPath, "EnableMappingRulesGenerator", config.EnableMappingRulesGenerator);
                }

                return config;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to read TheResourceOfLong config, using defaults: " + ex.Message);
                return defaults;
            }
        }

        private static bool HasKey(Dictionary<string, object> json, string key)
        {
            object value;
            return SimpleJson.TryGetValueIgnoreCase(json, key, out value);
        }

        private static void AppendBooleanProperty(string configPath, string key, bool value)
        {
            try
            {
                string text = File.ReadAllText(configPath);
                int objectEnd = text.LastIndexOf('}');
                if (objectEnd < 0) return;

                string before = text.Substring(0, objectEnd).TrimEnd();
                string after = text.Substring(objectEnd);
                string separator = before.EndsWith("{", StringComparison.Ordinal) ? string.Empty : ",";
                string line = separator + Environment.NewLine + "  \"" + key + "\": " + (value ? "true" : "false") + Environment.NewLine;
                File.WriteAllText(configPath, before + line + after, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to append TheResourceOfLong config key '" + key + "': " + ex.Message);
            }
        }
    }
}
