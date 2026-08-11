using Il2Cpp;
using System;
using System.Collections.Generic;

namespace TheExtensionOfLong
{
    public sealed class AddressFormRule
    {
        public string ModId;
        public int Priority;
        public string RawAddressForm;
        public string Condition;
        public int LoadOrder;
        public int RowOrder;
    }

    public static class HeroAddressFormRegistry
    {
        private static readonly CsvTableDefinition Table = new CsvTableDefinition
        {
            FileName = "HeroAddressFormData.csv",
            DisplayName = "默认称呼规则表",
            Required = false
        };

        private static readonly List<AddressFormRule> Rules = new List<AddressFormRule>();
        private static bool _initialized;

        public static bool HasRules
        {
            get { return Rules.Count > 0; }
        }

        public static void Initialize()
        {
            if (_initialized) return;
            Reload();
            _initialized = true;
        }

        public static void Reload()
        {
            Rules.Clear();

            List<ModProjectInfo> projects = ModProjectProvider.GetEnabledProjects();
            int totalTables = 0;
            for (int i = 0; i < projects.Count; i++)
            {
                CsvTable table;
                if (!CsvTableLoader.TryLoad(projects[i], Table, out table))
                    continue;

                totalTables++;
                LoadRules(table);
            }

            Rules.Sort(CompareRules);
            LoggerManager.Info("HeroAddressFormRegistry: 已加载默认称呼规则 " + Rules.Count + " 条，来源表 " + totalTables + " 个");
        }

        public static bool TryGetAddressForm(PlotController plotController, HeroData sourceHero, HeroData targetHero, out string addressForm)
        {
            addressForm = null;
            Initialize();

            if (Rules.Count == 0 || plotController == null || sourceHero == null || targetHero == null)
                return false;

            HeroData oldSource = plotController.sourceInteractHero;
            HeroData oldTarget = plotController.targetInteractHero;
            Dictionary<string, string> queryCache = null;

            try
            {
                plotController.sourceInteractHero = sourceHero;
                plotController.targetInteractHero = targetHero;

                for (int i = 0; i < Rules.Count; i++)
                {
                    AddressFormRule rule = Rules[i];
                    bool matched;
                    try
                    {
                        matched = ConditionExpressionEvaluator.Evaluate(plotController, rule.Condition, ref queryCache, showDebugLog: false);
                    }
                    catch (Exception ex)
                    {
                        LoggerManager.Warning("HeroAddressFormRegistry: 条件求值失败 [" + rule.ModId + "] " + rule.Condition + " - " + ex.Message);
                        continue;
                    }

                    if (!matched) continue;

                    string resolved = ConditionQueryHandlers.ResolveAllCommands(plotController, rule.RawAddressForm, ref queryCache);
                    if (string.IsNullOrEmpty(resolved))
                    {
                        LoggerManager.Warning("HeroAddressFormRegistry: 命中规则但称呼为空 [" + rule.ModId + "] " + rule.RawAddressForm);
                        continue;
                    }

                    addressForm = resolved;
                    LoggerManager.Debug("HeroAddressFormRegistry: 命中默认称呼规则 mod=" + rule.ModId +
                        ", loadOrder=" + rule.LoadOrder +
                        ", priority=" + rule.Priority +
                        ", result=" + resolved);
                    return true;
                }
            }
            finally
            {
                plotController.sourceInteractHero = oldSource;
                plotController.targetInteractHero = oldTarget;
            }

            return false;
        }

        private static void LoadRules(CsvTable table)
        {
            int addressIndex = table.FindColumn("称呼", "addressForm", "name");
            int priorityIndex = table.FindColumn("优先级", "priority");
            int conditionIndex = table.FindColumn("条件表达式", "condition", "expression");

            if (addressIndex < 0 || priorityIndex < 0 || conditionIndex < 0)
            {
                List<string> missingColumns = new List<string>();
                if (addressIndex < 0) missingColumns.Add("称呼/addressForm/name");
                if (priorityIndex < 0) missingColumns.Add("优先级/priority");
                if (conditionIndex < 0) missingColumns.Add("条件表达式/condition/expression");

                LoggerManager.Warning("HeroAddressFormRegistry: 表头缺少必填列 [" + string.Join(", ", missingColumns) +
                    "]: " + table.FilePath +
                    "，当前表头=[" + string.Join(", ", table.Headers ?? new string[0]) + "]");
                return;
            }

            for (int i = 0; i < table.Rows.Count; i++)
            {
                string[] row = table.Rows[i];
                if (CsvTable.IsEmptyRow(row)) continue;

                string addressForm = CsvTable.GetCell(row, addressIndex);
                string priorityText = CsvTable.GetCell(row, priorityIndex);
                string condition = CsvTable.GetCell(row, conditionIndex);

                if (string.IsNullOrWhiteSpace(addressForm) || string.IsNullOrWhiteSpace(condition))
                {
                    LoggerManager.Warning("HeroAddressFormRegistry: 跳过称呼或条件为空的规则: " + table.FilePath + " 行 " + (i + 2));
                    continue;
                }

                int priority;
                if (!int.TryParse(priorityText, out priority))
                {
                    LoggerManager.Warning("HeroAddressFormRegistry: 优先级解析失败: " + table.FilePath + " 行 " + (i + 2) + " 值=" + priorityText);
                    continue;
                }

                AddressFormRule rule = new AddressFormRule();
                rule.ModId = table.Project.ModId;
                rule.Priority = priority;
                rule.RawAddressForm = addressForm;
                rule.Condition = condition;
                rule.LoadOrder = table.Project.LoadOrder;
                rule.RowOrder = i;

                Rules.Add(rule);
            }
        }

        private static int CompareRules(AddressFormRule left, AddressFormRule right)
        {
            int cmp = right.LoadOrder.CompareTo(left.LoadOrder);
            if (cmp != 0) return cmp;

            cmp = right.Priority.CompareTo(left.Priority);
            if (cmp != 0) return cmp;

            return left.RowOrder.CompareTo(right.RowOrder);
        }
    }
}
