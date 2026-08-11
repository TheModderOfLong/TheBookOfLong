using System;
using System.Collections.Generic;
using System.IO;
using TheBookOfLong;

namespace TheExtensionOfLong
{
    public static class ModProjectProvider
    {
        public static List<ModProjectInfo> GetEnabledProjects()
        {
            List<ModProjectInfo> result = new List<ModProjectInfo>();

            try
            {
                if (!ModProjectRegistry.Initialize())
                {
                    LoggerManager.Error("ModProjectProvider: 龙之书 ModProjectRegistry 初始化失败，无法加载拓展数据规则");
                    return result;
                }

                IReadOnlyList<IModProject> projects = ModProjectRegistry.GetEnabledProjectsSnapshot();
                for (int i = 0; i < projects.Count; i++)
                {
                    IModProject source = projects[i];
                    if (source == null || !source.IsEnabled) continue;

                    ModProjectInfo info = new ModProjectInfo();
                    info.ModId = source.FolderName;
                    info.DisplayName = source.DisplayName;
                    info.Version = source.Version;
                    info.LoadOrder = source.LoadOrder;
                    info.IsEnabled = source.IsEnabled;
                    info.DirectoryPath = source.ModDirectory;
                    info.DataDirectory = source.DataDirectory;

                    if (string.IsNullOrWhiteSpace(info.DataDirectory) && !string.IsNullOrWhiteSpace(info.DirectoryPath))
                        info.DataDirectory = Path.Combine(info.DirectoryPath, "Data");

                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error("ModProjectProvider: 获取龙之书 Mod 项目快照失败: " + ex.Message);
            }

            return result;
        }
    }
}
