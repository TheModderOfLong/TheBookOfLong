using System;
using System.IO;

namespace TheResourceOfLong
{
    public static class PathUtility
    {
        public static string NormalizeResourcePath(string path)
        {
            return ModResourceEntry.NormalizePath(path);
        }

        public static string RemoveExtensionFromResourcePath(string path)
        {
            string normalized = NormalizeResourcePath(path);
            string directory = Path.GetDirectoryName(normalized);
            string fileName = Path.GetFileNameWithoutExtension(normalized);
            if (string.IsNullOrEmpty(directory)) return fileName;
            return NormalizeResourcePath(Path.Combine(directory, fileName));
        }

        public static string CombineSafe(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(root)) return relativePath;
            if (string.IsNullOrWhiteSpace(relativePath)) return root;

            string cleanRelative = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            while (cleanRelative.StartsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                cleanRelative = cleanRelative.Substring(1);
            }

            string fullRoot = Path.GetFullPath(root);
            string fullPath = Path.GetFullPath(Path.Combine(fullRoot, cleanRelative));
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Path escapes root: " + relativePath);
            }

            return fullPath;
        }

        public static string GetRelativePath(string root, string fullPath)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(fullPath));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)) return path;
            return path + Path.DirectorySeparatorChar;
        }
    }
}
