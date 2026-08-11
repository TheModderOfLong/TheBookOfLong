using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 单条选项的扩展元数据。
    /// 
    /// 这部分信息读表时会编码进 `SinglePlotChoiceData.describe`，显示时再解析为运行时缓存：
    /// - ShowCondition: 控制选项是否显示
    /// - RequirementCondition / RequirementDescription: 控制是否可点，以及“需要...”文案
    /// - InteractionCondition / InteractionDescription: 控制互动限制，以及“本月已用...”文案
    /// </summary>
    public sealed class PlotChoiceMetaData
    {
        /// <summary>显示条件表达式。</summary>
        public string ShowCondition;
        /// <summary>扩展前置条件表达式。</summary>
        public string RequirementCondition;
        /// <summary>扩展前置条件说明文本。</summary>
        public string RequirementDescription;
        /// <summary>扩展互动条件表达式。</summary>
        public string InteractionCondition;
        /// <summary>扩展互动条件说明文本。</summary>
        public string InteractionDescription;

        /// <summary>
        /// 当前元数据是否为空。
        /// 为空时，说明这条选项没有任何扩展逻辑，可以从缓存中移除。
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(ShowCondition)
                    && string.IsNullOrEmpty(RequirementCondition)
                    && string.IsNullOrEmpty(RequirementDescription)
                    && string.IsNullOrEmpty(InteractionCondition)
                    && string.IsNullOrEmpty(InteractionDescription);
            }
        }
    }

    /// <summary>
    /// 当前选项批次的元数据运行时缓存管理器。
    /// 
    /// 设计原则：
    /// 1. 长期存储依赖 choice.describe，不依赖对象引用。
    /// 2. 字典只服务于当前已显示的选项批次。
    /// 3. 这里集中提供登记、查询、清理和条件求值能力，避免散落在多个 patch 中。
    /// </summary>
    public static class PlotChoiceMetaDataManager
    {
        private static readonly Dictionary<SinglePlotChoiceData, PlotChoiceMetaData> _metadataMap =
            new Dictionary<SinglePlotChoiceData, PlotChoiceMetaData>(ReferenceComparer<SinglePlotChoiceData>.Instance);

        /// <summary>
        /// 登记一条选项的扩展元数据。
        /// 
        /// 如果 meta 为空，或所有字段都为空，会直接从缓存移除对应选项。
        /// </summary>
        public static void Register(SinglePlotChoiceData choice, PlotChoiceMetaData meta)
        {
            if (choice == null) return;

            if (meta == null || meta.IsEmpty)
            {
                _metadataMap.Remove(choice);
                return;
            }

            _metadataMap[choice] = meta;
        }

        /// <summary>
        /// 读取某个选项绑定的扩展元数据。
        /// </summary>
        public static bool TryGet(SinglePlotChoiceData choice, out PlotChoiceMetaData meta)
        {
            if (choice != null && _metadataMap.TryGetValue(choice, out meta))
                return true;

            meta = null;
            return false;
        }

        /// <summary>
        /// 优先读取当前批次缓存；缓存不存在时，从 choice.describe 兜底解析。
        /// </summary>
        public static PlotChoiceMetaData GetOrParse(SinglePlotChoiceData choice)
        {
            PlotChoiceMetaData meta;
            if (TryGet(choice, out meta) && meta != null)
                return meta;

            if (choice == null || !PlotChoiceMetaTagHelper.HasLeadingTag(choice.describe))
                return null;

            PlotChoiceMetaParseResult parseResult = PlotChoiceMetaTagHelper.Parse(choice.describe);
            meta = parseResult.Meta;
            Register(choice, meta);
            return meta;
        }

        /// <summary>读取显示条件表达式。</summary>
        public static string GetShowCondition(SinglePlotChoiceData choice)
        {
            PlotChoiceMetaData meta;
            return TryGet(choice, out meta) ? meta.ShowCondition : string.Empty;
        }

        /// <summary>读取扩展前置条件表达式。</summary>
        public static string GetRequirementCondition(SinglePlotChoiceData choice)
        {
            PlotChoiceMetaData meta;
            return TryGet(choice, out meta) ? meta.RequirementCondition : string.Empty;
        }

        /// <summary>读取扩展前置条件说明文本。</summary>
        public static string GetRequirementDescription(SinglePlotChoiceData choice)
        {
            PlotChoiceMetaData meta;
            return TryGet(choice, out meta) ? meta.RequirementDescription : string.Empty;
        }

        /// <summary>读取扩展互动条件表达式。</summary>
        public static string GetInteractionCondition(SinglePlotChoiceData choice)
        {
            PlotChoiceMetaData meta;
            return TryGet(choice, out meta) ? meta.InteractionCondition : string.Empty;
        }

        /// <summary>读取扩展互动条件说明文本。</summary>
        public static string GetInteractionDescription(SinglePlotChoiceData choice)
        {
            PlotChoiceMetaData meta;
            return TryGet(choice, out meta) ? meta.InteractionDescription : string.Empty;
        }

        /// <summary>
        /// 根据当前显示中的选项集合收束缓存。
        /// 
        /// 只保留仍在本轮 choices 列表中的条目，避免旧剧情残留的元数据污染后续选项。
        /// </summary>
        public static void RetainForChoices(Il2CppSystem.Collections.Generic.List<SinglePlotChoiceData> choices)
        {
            if (_metadataMap.Count == 0) return;

            HashSet<SinglePlotChoiceData> keepSet = new HashSet<SinglePlotChoiceData>(ReferenceComparer<SinglePlotChoiceData>.Instance);
            if (choices != null)
            {
                for (int i = 0; i < choices.Count; i++)
                {
                    SinglePlotChoiceData choice = choices[i];
                    if (choice != null)
                        keepSet.Add(choice);
                }
            }

            if (keepSet.Count == 0)
            {
                _metadataMap.Clear();
                return;
            }

            List<SinglePlotChoiceData> removeList = null;
            foreach (SinglePlotChoiceData key in _metadataMap.Keys)
            {
                if (!keepSet.Contains(key))
                {
                    if (removeList == null)
                        removeList = new List<SinglePlotChoiceData>();
                    removeList.Add(key);
                }
            }

            if (removeList == null) return;

            for (int i = 0; i < removeList.Count; i++)
            {
                _metadataMap.Remove(removeList[i]);
            }
        }

        /// <summary>清空全部元数据缓存。</summary>
        public static void ClearAll()
        {
            _metadataMap.Clear();
        }

        /// <summary>
        /// 解析“表达式/说明”的扩展参数格式。
        /// 
        /// 返回规则：
        /// - raw 为 null：返回 false，表示没有可解析内容
        /// - raw 为空串：返回 true，表达式与说明都为空
        /// - 无 '/'：整段视为表达式，说明为空
        /// - 有 '/'：左边为表达式，右边为说明
        /// </summary>
        public static bool TryParseConditionDescriptor(string raw, out string expression, out string description, out bool expressionMissing)
        {
            expression = string.Empty;
            description = string.Empty;
            expressionMissing = false;

            if (raw == null)
                return false;

            string text = raw.Trim();
            if (text.Length == 0)
            {
                return true;
            }

            int slashIndex = text.IndexOf('/');
            if (slashIndex < 0)
            {
                expression = text;
                return true;
            }

            expression = text.Substring(0, slashIndex).Trim();
            description = text.Substring(slashIndex + 1).Trim();

            return true;
        }

        /// <summary>
        /// 统一求值扩展条件。
        /// 
        /// 这里不额外做“错误放行”，以免条件写错后误让不该出现的选项进入游戏流程。
        /// </summary>
        public static bool TryEvaluateCondition(PlotController plotController, string expression, string contextTag, string choiceText)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return true;

            try
            {
                return ConditionExpressionEvaluator.Evaluate(plotController, expression, showDebugLog: false);
            }
            catch (Exception ex)
            {
                LoggerManager.Warning($"{contextTag}: 条件表达式执行异常，已按 false 处理。选项=\"{choiceText}\", 表达式=\"{expression}\", 异常={ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 以“引用相等”作为比较方式的字典比较器。
        /// 
        /// 这里不能用内容相等，因为选项实例并没有稳定的业务主键；
        /// 我们只关心“这个对象本身”是不是那条选项。
        /// </summary>
        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

            public bool Equals(T x, T y)
            {
                return object.ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
