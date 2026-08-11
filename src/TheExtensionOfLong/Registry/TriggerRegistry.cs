using System;
using System.Collections.Generic;
using System.Linq;

namespace TheExtensionOfLong
{
    public static class TriggerRegistry
    {
        private static readonly CsvTableDefinition Table = new CsvTableDefinition
        {
            FileName = "TriggerData.csv",
            DisplayName = "触发器规则表",
            Required = false
        };

        private static readonly Dictionary<string, TriggerRule> RulesById = new Dictionary<string, TriggerRule>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<TriggerType, List<TriggerRule>> RulesByType = new Dictionary<TriggerType, List<TriggerRule>>();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            Reload();
            _initialized = true;
        }

        public static void Reload()
        {
            RulesById.Clear();
            RulesByType.Clear();

            List<ModProjectInfo> projects = ModProjectProvider.GetEnabledProjects();
            projects.Sort(delegate(ModProjectInfo left, ModProjectInfo right)
            {
                int cmp = left.LoadOrder.CompareTo(right.LoadOrder);
                if (cmp != 0) return cmp;
                return string.Compare(left.ModId, right.ModId, StringComparison.OrdinalIgnoreCase);
            });

            int totalTables = 0;
            for (int i = 0; i < projects.Count; i++)
            {
                CsvTable table;
                if (!CsvTableLoader.TryLoad(projects[i], Table, out table))
                    continue;

                totalTables++;
                LoadRules(table);
            }

            RebuildTypeIndex();
            TriggerStateManager.ApplyPersistedStates();

            LoggerManager.Info("TriggerRegistry: 已加载触发器规则 " + RulesById.Count + " 条，来源表 " + totalTables + " 个");
        }

        public static List<TriggerRule> GetEnabledRules(TriggerType type)
        {
            Initialize();

            List<TriggerRule> rules;
            if (!RulesByType.TryGetValue(type, out rules) || rules == null || rules.Count == 0)
                return new List<TriggerRule>();

            List<TriggerRule> result = new List<TriggerRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                TriggerRule rule = rules[i];
                if (rule != null && rule.Enabled)
                    result.Add(rule);
            }

            return result;
        }

        public static bool TryGetRule(string id, out TriggerRule rule)
        {
            Initialize();
            rule = null;

            if (string.IsNullOrWhiteSpace(id))
                return false;

            return RulesById.TryGetValue(id.Trim(), out rule);
        }

        public static bool ApplyEnabledState(string id, bool enabled, bool rebuildIndexes = true)
        {
            if (string.IsNullOrWhiteSpace(id))
                return false;

            TriggerRule rule;
            if (!RulesById.TryGetValue(id.Trim(), out rule) || rule == null)
                return false;

            rule.Enabled = enabled;
            if (rebuildIndexes)
                RebuildTypeIndex();

            return true;
        }

        public static void RebuildIndexes()
        {
            RebuildTypeIndex();
        }

        private static void LoadRules(CsvTable table)
        {
            int idIndex = table.FindColumn("编号", "id", "ID");
            int typeIndex = table.FindColumn("类型", "type");
            int priorityIndex = table.FindColumn("优先级", "priority");
            int enabledIndex = table.FindColumn("启用", "enabled");
            int conditionIndex = table.FindColumn("条件表达式", "condition expression", "expression", "条件", "condition");
            int functionsIndex = table.FindColumn("函数", "functions", "function", "callFuc");
            int noteIndex = table.FindColumn("备注", "note", "remark");

            if (idIndex < 0 || typeIndex < 0 || priorityIndex < 0 || enabledIndex < 0 || conditionIndex < 0 || functionsIndex < 0)
            {
                List<string> missingColumns = new List<string>();
                if (idIndex < 0) missingColumns.Add("编号/id/ID");
                if (typeIndex < 0) missingColumns.Add("类型/type");
                if (priorityIndex < 0) missingColumns.Add("优先级/priority");
                if (enabledIndex < 0) missingColumns.Add("启用/enabled");
                if (conditionIndex < 0) missingColumns.Add("条件表达式/condition expression/expression/条件/condition");
                if (functionsIndex < 0) missingColumns.Add("函数/functions/function/callFuc");

                LoggerManager.Warning("TriggerRegistry: 表头缺少必填列 [" + string.Join(", ", missingColumns) +
                    "]: " + table.FilePath +
                    "，当前表头=[" + string.Join(", ", table.Headers ?? new string[0]) + "]");
                return;
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {
                string[] row = table.Rows[i];
                if (CsvTable.IsEmptyRow(row)) continue;

                string id = CsvTable.GetCell(row, idIndex);
                if (string.IsNullOrWhiteSpace(id))
                {
                    LoggerManager.Warning("TriggerRegistry: 跳过编号为空的规则: " + table.FilePath + " 行 " + (i + 2));
                    continue;
                }

                TriggerType type;
                string typeText = CsvTable.GetCell(row, typeIndex);
                if (!TryParseTriggerType(typeText, out type))
                {
                    LoggerManager.Warning("TriggerRegistry: 类型解析失败: " + table.FilePath + " 行 " + (i + 2) + " 值=" + typeText);
                    continue;
                }

                int priority;
                string priorityText = CsvTable.GetCell(row, priorityIndex);
                if (!int.TryParse(priorityText, out priority))
                {
                    LoggerManager.Warning("TriggerRegistry: 优先级解析失败: " + table.FilePath + " 行 " + (i + 2) + " 值=" + priorityText);
                    continue;
                }

                bool enabled = CommonHandlers.ParseBool(CsvTable.GetCell(row, enabledIndex), false);
                string condition = CsvTable.GetCell(row, conditionIndex);
                string functions = CsvTable.GetCell(row, functionsIndex);

                TriggerRule rule = new TriggerRule();
                rule.Id = id.Trim();
                rule.Type = type;
                rule.Priority = priority;
                rule.DefaultEnabled = enabled;
                rule.Enabled = enabled;
                rule.Condition = condition;
                rule.Functions = functions;
                rule.Note = noteIndex >= 0 ? CsvTable.GetCell(row, noteIndex) : "";
                rule.ModId = table.Project?.ModId ?? "";
                rule.SourceFile = table.FilePath;
                rule.LoadOrder = table.Project?.LoadOrder ?? 0;
                rule.RowOrder = i;

                if (RulesById.ContainsKey(rule.Id))
                {
                    TriggerRule old = RulesById[rule.Id];
                    LoggerManager.Debug("TriggerRegistry: 触发器编号覆盖 id=" + rule.Id +
                        ", oldMod=" + old.ModId + ", newMod=" + rule.ModId);
                }

                RulesById[rule.Id] = rule;
            }
        }

        private static bool TryParseTriggerType(string raw, out TriggerType type)
        {
            type = TriggerType.None;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string text = raw.Trim();
            if (Enum.TryParse(text, true, out type) && Enum.IsDefined(typeof(TriggerType), type))
                return true;

            int value;
            if (int.TryParse(text, out value) && Enum.IsDefined(typeof(TriggerType), value))
            {
                type = (TriggerType)value;
                return true;
            }

            return false;
        }

        private static void RebuildTypeIndex()
        {
            RulesByType.Clear();

            foreach (TriggerRule rule in RulesById.Values)
            {
                if (rule == null) continue;

                List<TriggerRule> list;
                if (!RulesByType.TryGetValue(rule.Type, out list))
                {
                    list = new List<TriggerRule>();
                    RulesByType.Add(rule.Type, list);
                }

                list.Add(rule);
            }

            foreach (List<TriggerRule> list in RulesByType.Values)
            {
                list.Sort(CompareRules);
            }
        }

        private static int CompareRules(TriggerRule left, TriggerRule right)
        {
            int cmp = right.Priority.CompareTo(left.Priority);
            if (cmp != 0) return cmp;

            cmp = right.LoadOrder.CompareTo(left.LoadOrder);
            if (cmp != 0) return cmp;

            return left.RowOrder.CompareTo(right.RowOrder);
        }
    }
}
