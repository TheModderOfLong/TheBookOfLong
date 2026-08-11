using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TheResourceOfLong
{
    public static class ModDiscovery
    {
        public const int MissingPriority = int.MinValue;

        public static string ResolveGameRoot()
        {
            string current = Directory.GetCurrentDirectory();
            string resolved = TryFindGameRoot(current);
            if (!string.IsNullOrEmpty(resolved)) return resolved;

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            resolved = TryFindGameRoot(baseDirectory);
            if (!string.IsNullOrEmpty(resolved)) return resolved;

            return current;
        }

        public static string ResolveModsOfLongRoot(string gameRoot)
        {
            string modsOfLong = Path.Combine(gameRoot, "Mods", "ModsOfLong");
            if (Directory.Exists(modsOfLong)) return modsOfLong;

            string mods = Path.Combine(gameRoot, "Mods");
            return Directory.Exists(mods) ? mods : modsOfLong;
        }

        public static List<ModProjectInfo> DiscoverProjects(string modsOfLongRoot)
        {
            List<ModProjectInfo> projects;
            if (TryDiscoverProjectsFromTheBookOfLong(out projects))
            {
                projects.Sort(CompareProjectsByBookLoadOrder);
                return projects;
            }

            LoggerManager.Warning("TheBookOfLong ModProjectRegistry unavailable, TheResourceOfLong will use legacy RES discovery.");
            projects = DiscoverProjectsByLegacyScan(modsOfLongRoot);
            projects.Sort(CompareProjectsForLoad);
            return projects;
        }

        public static int CompareProjectsByBookLoadOrder(ModProjectInfo left, ModProjectInfo right)
        {
            int orderCompare = right.LoadOrder.CompareTo(left.LoadOrder);
            if (orderCompare != 0) return orderCompare;

            return string.Compare(left.ModId, right.ModId, StringComparison.OrdinalIgnoreCase);
        }

        public static int CompareProjectsForLoad(ModProjectInfo left, ModProjectInfo right)
        {
            int priorityCompare = right.Priority.CompareTo(left.Priority);
            if (priorityCompare != 0) return priorityCompare;

            return string.Compare(left.ModId, right.ModId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryDiscoverProjectsFromTheBookOfLong(out List<ModProjectInfo> result)
        {
            result = new List<ModProjectInfo>();

            try
            {
                Type registryType = FindTheBookOfLongRegistryType();
                if (registryType == null) return false;

                MethodInfo initializeMethod = registryType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                MethodInfo snapshotMethod = registryType.GetMethod("GetEnabledProjectsSnapshot", BindingFlags.Public | BindingFlags.Static);
                if (initializeMethod == null || snapshotMethod == null) return false;

                object initialized = initializeMethod.Invoke(null, null);
                if (!(initialized is bool) || !(bool)initialized) return false;

                object snapshot = snapshotMethod.Invoke(null, null);
                IEnumerable enabledProjects = snapshot as IEnumerable;
                if (enabledProjects == null) return false;

                int discoveryOrder = 0;
                foreach (object source in enabledProjects)
                {
                    if (source == null) continue;

                    bool isEnabled = GetBoolProperty(source, "IsEnabled", true);
                    if (!isEnabled) continue;

                    string modDirectory = GetStringProperty(source, "ModDirectory");
                    if (string.IsNullOrWhiteSpace(modDirectory)) continue;

                    string resDirectory = Path.Combine(modDirectory, "RES");
                    if (!Directory.Exists(resDirectory)) continue;

                    ModProjectInfo project = ReadProjectInfo(modDirectory);
                    project.ModId = GetStringProperty(source, "FolderName");
                    project.DisplayName = GetStringProperty(source, "DisplayName");
                    project.Version = GetStringProperty(source, "Version");
                    project.DirectoryPath = modDirectory;
                    project.ResDirectoryPath = resDirectory;
                    project.LoadOrder = GetIntProperty(source, "LoadOrder", 0);
                    project.IsEnabled = isEnabled;
                    project.DiscoveryOrder = discoveryOrder++;
                    project.UsesBookLoadOrder = true;

                    if (string.IsNullOrWhiteSpace(project.ModId))
                    {
                        project.ModId = Path.GetFileName(modDirectory);
                    }

                    result.Add(project);
                }

                LoggerManager.Info("Using TheBookOfLong ModProjectRegistry for RES discovery. Project(s) with RES: " + result.Count + ".");
                return true;
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to read TheBookOfLong ModProjectRegistry, falling back to legacy RES discovery: " + ex.Message);
                result.Clear();
                return false;
            }
        }

        private static List<ModProjectInfo> DiscoverProjectsByLegacyScan(string modsOfLongRoot)
        {
            List<ModProjectInfo> result = new List<ModProjectInfo>();
            if (!Directory.Exists(modsOfLongRoot))
            {
                LoggerManager.Warning("ModsOfLong directory not found: " + modsOfLongRoot);
                return result;
            }

            DirectoryInfo root = new DirectoryInfo(modsOfLongRoot);
            DirectoryInfo[] directories = root.GetDirectories();
            Array.Sort(directories, delegate(DirectoryInfo left, DirectoryInfo right)
            {
                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });

            int discoveryOrder = 0;
            foreach (DirectoryInfo directory in directories)
            {
                string resDirectory = Path.Combine(directory.FullName, "RES");
                if (!Directory.Exists(resDirectory)) continue;

                ModProjectInfo project = ReadProjectInfo(directory.FullName);
                project.ModId = directory.Name;
                project.DirectoryPath = directory.FullName;
                project.ResDirectoryPath = resDirectory;
                project.DiscoveryOrder = discoveryOrder++;
                project.LoadOrder = project.DiscoveryOrder;
                project.IsEnabled = true;
                project.UsesBookLoadOrder = false;
                result.Add(project);
            }

            return result;
        }

        private static Type FindTheBookOfLongRegistryType()
        {
            Type registryType = Type.GetType("TheBookOfLong.ModProjectRegistry, TheBookOfLong");
            if (registryType != null) return registryType;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                registryType = assemblies[i].GetType("TheBookOfLong.ModProjectRegistry", false);
                if (registryType != null) return registryType;
            }

            return null;
        }

        private static string GetStringProperty(object source, string propertyName)
        {
            object value = GetPropertyValue(source, propertyName);
            return value == null ? null : value.ToString();
        }

        private static int GetIntProperty(object source, string propertyName, int defaultValue)
        {
            object value = GetPropertyValue(source, propertyName);
            int result;
            return value != null && int.TryParse(value.ToString(), out result) ? result : defaultValue;
        }

        private static bool GetBoolProperty(object source, string propertyName, bool defaultValue)
        {
            object value = GetPropertyValue(source, propertyName);
            bool result;
            return value != null && bool.TryParse(value.ToString(), out result) ? result : defaultValue;
        }

        private static object GetPropertyValue(object source, string propertyName)
        {
            if (source == null) return null;

            PropertyInfo property = source.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            return property == null ? null : property.GetValue(source, null);
        }

        private static ModProjectInfo ReadProjectInfo(string modDirectory)
        {
            ModProjectInfo info = new ModProjectInfo();
            info.Priority = MissingPriority;
            info.HasPriority = false;

            string infoPath = Path.Combine(modDirectory, "Info.json");
            if (!File.Exists(infoPath)) return info;

            try
            {
                Dictionary<string, object> json = SimpleJson.ParseObject(File.ReadAllText(infoPath));
                object priorityToken;
                if (SimpleJson.TryGetValueIgnoreCase(json, "Priority", out priorityToken))
                {
                    int priority;
                    if (priorityToken != null && int.TryParse(priorityToken.ToString(), out priority))
                    {
                        info.Priority = priority;
                        info.HasPriority = true;
                    }
                    else
                    {
                        LoggerManager.Warning("Invalid Priority in " + infoPath + ": " + priorityToken);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning("Failed to read Info.json: " + infoPath + " - " + ex.Message);
            }

            return info;
        }

        private static string TryFindGameRoot(string start)
        {
            if (string.IsNullOrWhiteSpace(start)) return null;

            DirectoryInfo current = new DirectoryInfo(start);
            while (current != null)
            {
                if (Directory.Exists(Path.Combine(current.FullName, "LongYinLiZhiZhuan_Data")) &&
                    Directory.Exists(Path.Combine(current.FullName, "Mods")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return null;
        }
    }
}
