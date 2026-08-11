using System;
using System.Collections.Generic;
using System.IO;

namespace TheResourceOfLong
{
    public static class MappingRulesPathResolver
    {
        private const string DataDirectoryName = "Data";
        private const string MappingDirectoryName = "Mapping";
        private const string MappingRulesFileName = "MappingRules.csv";
        private static readonly List<Func<ModProjectInfo, string>> CandidatePathProviders = new List<Func<ModProjectInfo, string>>();

        static MappingRulesPathResolver()
        {
            RegisterCandidatePathProvider(GetDataRulesPath);
            RegisterCandidatePathProvider(GetDefaultOutputPath);
        }

        public static void RegisterCandidatePathProvider(Func<ModProjectInfo, string> provider)
        {
            if (provider == null) return;
            CandidatePathProviders.Add(provider);
        }

        public static string GetMappingRoot(ModProjectInfo project)
        {
            return project == null ? string.Empty : Path.Combine(project.ResDirectoryPath, MappingDirectoryName);
        }

        public static string GetDefaultOutputPath(ModProjectInfo project)
        {
            return Path.Combine(GetMappingRoot(project), MappingRulesFileName);
        }

        public static bool TryResolveExisting(ModProjectInfo project, out string rulesPath, out string fallbackPath)
        {
            rulesPath = null;
            fallbackPath = GetDefaultOutputPath(project);

            foreach (string candidate in EnumerateCandidatePaths(project))
            {
                if (!File.Exists(candidate)) continue;
                rulesPath = candidate;
                return true;
            }

            return false;
        }

        public static bool HasExistingRules(ModProjectInfo project)
        {
            string rulesPath;
            string fallbackPath;
            return TryResolveExisting(project, out rulesPath, out fallbackPath);
        }

        private static IEnumerable<string> EnumerateCandidatePaths(ModProjectInfo project)
        {
            if (project == null) yield break;

            foreach (Func<ModProjectInfo, string> provider in CandidatePathProviders)
            {
                string candidate = provider(project);
                if (!string.IsNullOrWhiteSpace(candidate)) yield return candidate;
            }
        }

        private static string GetDataRulesPath(ModProjectInfo project)
        {
            return Path.Combine(project.DirectoryPath, DataDirectoryName, MappingRulesFileName);
        }
    }
}
