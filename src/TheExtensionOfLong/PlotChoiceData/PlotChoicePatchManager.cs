using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Il2Cpp;
using Newtonsoft.Json;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 选项补丁管理器：加载JSON补丁文件，并在运行时动态应用
    /// </summary>
    public static class PlotChoicePatchManager
    {
        // 按 patchFunction 分组的补丁数据（使用标准 .NET 集合）
        private static readonly Dictionary<string, List<PlotChoicePatchData>> _patchesByFunction
            = new Dictionary<string, List<PlotChoicePatchData>>(StringComparer.OrdinalIgnoreCase);

        private static bool _isInitialized = false;

        /// <summary>
        /// 初始化：扫描 ModsOfLong 目录，加载所有 PlotChoiceDataController.json
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;

            try
            {
                List<ModProjectInfo> projects = ModProjectProvider.GetEnabledProjects();
                if (projects.Count == 0)
                {
                    LoggerManager.Warning("PlotChoicePatchManager: 未取得龙之书启用 Mod 列表，跳过加载");
                    _isInitialized = true;
                    return;
                }

                projects = projects
                    .Where(p => p != null && p.IsEnabled)
                    .OrderBy(p => p.LoadOrder)
                    .ThenBy(p => p.ModId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                int totalPatches = 0;

                foreach (ModProjectInfo project in projects)
                {
                    if (string.IsNullOrWhiteSpace(project.DirectoryPath)) continue;

                    string complexDataDir = Path.Combine(project.DirectoryPath, "ComplexData");
                    if (!Directory.Exists(complexDataDir)) continue;

                    string[] jsonFiles = Directory.GetFiles(complexDataDir, "PlotChoiceDataController*.json");
                    Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);

                    foreach (string jsonFile in jsonFiles)
                    {
                        int count = LoadPatchFile(jsonFile, project);
                        totalPatches += count;
                    }
                }

                LoggerManager.Debug($"PlotChoicePatchManager: 加载完成，共 {totalPatches} 个选项补丁，覆盖 {_patchesByFunction.Count} 个方法");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoicePatchManager: 初始化失败 - {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 重新加载所有补丁文件
        /// </summary>
        public static void Reload()
        {
            _patchesByFunction.Clear();
            _isInitialized = false;
            Initialize();
        }

        /// <summary>
        /// 加载单个JSON补丁文件
        /// </summary>
        private static int LoadPatchFile(string filePath, ModProjectInfo project)
        {
            try
            {
                string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                var patches = JsonConvert.DeserializeObject<List<PlotChoicePatchData>>(json);
                if (patches == null || patches.Count == 0) return 0;

                int validCount = 0;
                foreach (var patch in patches)
                {
                    if (string.IsNullOrEmpty(patch.patchFunction))
                    {
                        LoggerManager.Warning($"PlotChoicePatchManager: 跳过无 patchFunction 的补丁 (文件: {Path.GetFileName(filePath)})");
                        continue;
                    }

                    if (!_patchesByFunction.TryGetValue(patch.patchFunction, out var list))
                    {
                        list = new List<PlotChoicePatchData>();
                        _patchesByFunction[patch.patchFunction] = list;
                    }
                    list.Add(patch);
                    validCount++;
                }

                string modName = string.IsNullOrWhiteSpace(project.DisplayName) ? project.ModId : project.DisplayName;
                LoggerManager.Debug($"PlotChoicePatchManager: 从 [{modName}] 加载 {validCount} 个选项补丁");
                return validCount;
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoicePatchManager: 加载文件失败 {Path.GetFileName(filePath)} - {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 在指定方法执行后，动态应用选项补丁
        /// 由各 HarmonyPostfix 补丁调用
        /// </summary>
        /// <param name="plotController">PlotController 实例</param>
        /// <param name="methodName">对应的 patchFunction 值</param>
        public static void ApplyPatches(PlotController plotController, string methodName)
        {
            if (!_isInitialized) Initialize();
            if (_patchesByFunction.Count == 0) return;

            if (!_patchesByFunction.TryGetValue(methodName, out var patches)) return;

            SinglePlotData nowPlot = plotController.nowSinglePlot;
            if (nowPlot == null) return;

            Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> choices = nowPlot.choices;
            if (choices == null) return;

            // 按 priority 降序排序（高优先级先处理）
            var sortedPatches = patches.OrderByDescending(p => p.priority).ToList();

            foreach (var patch in sortedPatches)
            {
                try
                {
                    ApplySinglePatch(plotController, choices, patch);
                }
                catch (Exception ex)
                {
                    LoggerManager.Error($"PlotChoicePatchManager: 应用补丁失败 (method={methodName}, " +
                        $"text={patch.overwriteChoiceText ?? patch.ChoiceData?.choiceText}) - {ex.Message}");
                }
            }

            // 写回修改后的 choices
            nowPlot.choices = choices;
        }

        /// <summary>
        /// 应用单个选项补丁
        /// </summary>
        private static void ApplySinglePatch(
            PlotController plotController,
            Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> choices,
            PlotChoicePatchData patch)
        {
            // 1. 条件检查
            if (!string.IsNullOrEmpty(patch.conditionGroup))
            {
                bool conditionMet = ConditionExpressionEvaluator.Evaluate(plotController, patch.conditionGroup);
                if (!conditionMet)
                {
                    LoggerManager.Debug($"  选项补丁条件不满足，跳过: {patch.overwriteChoiceText ?? patch.ChoiceData?.choiceText}");
                    return;
                }
            }

            // 2. 根据 insertType 执行不同操作
            switch (patch.insertType)
            {
                case InsertType.Overwrite:
                    ApplyOverwrite(choices, patch);
                    break;
                case InsertType.Before:
                    ApplyInsert(choices, patch, before: true);
                    break;
                case InsertType.After:
                    ApplyInsert(choices, patch, before: false);
                    break;
                default:
                    LoggerManager.Warning($"PlotChoicePatchManager: 未知 insertType={patch.insertType}，跳过");
                    break;
            }
        }

        /// <summary>
        /// Overwrite 模式：用新选项数据覆盖原选项
        /// </summary>
        private static void ApplyOverwrite(Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> choices, PlotChoicePatchData patch)
        {
            if (string.IsNullOrEmpty(patch.overwriteChoiceText)) return;

            for (int i = 0; i < choices.Count; i++)
            {
                SinglePlotChoiceData choice = choices[i];
                if (choice != null && choice.choiceText == patch.overwriteChoiceText)
                {
                    if (patch.ChoiceData == null)
                    {
                        // choiceData=null 表示删除该选项
                        choices.RemoveAt(i);
                        LoggerManager.Debug($"  选项补丁[Overwrite/删除]: \"{patch.overwriteChoiceText}\"");
                    }
                    else
                    {
                        // 用新数据覆盖原选项
                        PlotChoiceDataBuilder.ApplyToChoice(choice, patch.ChoiceData);
                        LoggerManager.Debug($"  选项补丁[Overwrite]: \"{patch.overwriteChoiceText}\"");
                    }
                    return;
                }
            }

            // 未找到匹配的选项
            LoggerManager.Debug($"  选项补丁[Overwrite]: 未找到匹配 \"{patch.overwriteChoiceText}\" 的选项，跳过");
        }

        /// <summary>
        /// Before/After 模式：插入新选项
        /// </summary>
        private static void ApplyInsert(Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> choices, PlotChoicePatchData patch, bool before)
        {
            if (patch.ChoiceData == null)
            {
                LoggerManager.Warning("  选项补丁[Insert]: Before/After 模式下 ChoiceData 不能为空，跳过");
                return;
            }

            // 检查是否已有同 overwriteChoiceText 的补丁选项（去重/覆盖逻辑）
            if (!string.IsNullOrEmpty(patch.overwriteChoiceText))
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    SinglePlotChoiceData existing = choices[i];
                    if (existing != null && existing.choiceText == patch.overwriteChoiceText)
                {
                    // 已存在同描述选项，覆盖其内容
                    PlotChoiceDataBuilder.ApplyToChoice(existing, patch.ChoiceData);
                        LoggerManager.Debug($"  选项补丁[Insert/覆盖已有]: \"{patch.overwriteChoiceText}\"");
                        return;
                    }
                }
            }

            // 构建新的 SinglePlotChoiceData
            SinglePlotChoiceData newChoice = PlotChoiceDataBuilder.BuildChoice(patch.ChoiceData);
            if (newChoice == null) return;

            // 计算插入位置
            int targetIndex = CalculateInsertIndex(choices, patch, before);

            choices.Insert(targetIndex, newChoice);
            LoggerManager.Debug($"  选项补丁[{(before ? "Before" : "After")}]: " +
                $"\"{newChoice.choiceText}\" @ index {targetIndex}");
        }

        /// <summary>
        /// 计算选项插入的索引位置
        /// </summary>
        private static int CalculateInsertIndex(Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> choices, PlotChoicePatchData patch, bool before)
        {
            int targetIndex;

            if (patch.insertPos.HasValue)
            {
                // 指定了位置
                targetIndex = patch.insertPos.Value;
                if (targetIndex < 0) targetIndex = 0;
                if (targetIndex > choices.Count) targetIndex = choices.Count;

                if (!before && targetIndex < choices.Count) targetIndex++;
            }
            else
            {
                // null = 最后一个
                targetIndex = choices.Count;
                if (before && choices.Count > 0) targetIndex = choices.Count - 1;
            }

            return targetIndex;
        }
    }
}
