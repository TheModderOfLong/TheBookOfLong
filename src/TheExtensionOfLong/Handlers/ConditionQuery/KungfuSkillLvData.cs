using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// KungfuSkillLvData 查询指令。
    /// 格式: [$KungfuSkillLvData:技能信息:技能来源$]
    /// </summary>
    [ConditionQuery("KungfuSkillLvData")]
    public static class QueryKungfuSkillLvData
    {
        private static readonly Dictionary<string, Func<PlotController, Il2Cpp.KungfuSkillLvData, string[], string>> CompositeMethods
            = new Dictionary<string, Func<PlotController, Il2Cpp.KungfuSkillLvData, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Name",                              CompositeName },
            { "GetSkillDescribe",                  CompositeGetSkillDescribe },
            { "GetSkillNeedExpRate",               CompositeGetSkillNeedExpRate },
            { "SkillGetMaxExp",                    CompositeSkillGetMaxExp },
            { "Count",                             CompositeCount },
            { "Value",                             CompositeValue },
            { "Index",                             CompositeIndex },
        };

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  KungfuSkillLvData查询: 参数不足，格式[KungfuSkillLvData:技能信息:技能来源(可选)]");
                return "";
            }

            string fieldInfo = parts[1]; // 技能信息，可能含 =参数
            string skillSourceRaw = parts.Length > 2 ? parts[2] : null; // 技能来源

            // 解析技能来源 → KungfuSkillLvData
            Il2Cpp.KungfuSkillLvData skill = CommonHandlers.ResolveKungfuSkillSource(plotController, skillSourceRaw);
            if (skill == null)
            {
                LoggerManager.Warning($"  KungfuSkillLvData查询: 未找到技能 (source=\"{skillSourceRaw}\")");
                return "";
            }

            // 处理复合方法（如 Name=true）
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(plotController, skill, methodName, methodArg);
            }

            // 无参数的复合方法也支持不带=号调用
            if (CompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveCompositeMethod(plotController, skill, fieldInfo, "");
            }

            // 普通属性/方法读取
            return ConditionQueryHandlers.ReadObjectFieldValue(skill, "KungfuSkillLvData", fieldInfo);
        }

        private static string ResolveCompositeMethod(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, skill, args);

            LoggerManager.Warning($"  KungfuSkillLvData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        /// <summary>
        /// 获取技能显示名称。
        /// 格式: Name=true(带颜色) 或 Name=false(不带颜色) 或 Name(默认true)
        /// </summary>
        private static string CompositeName(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string[] args)
        {
            if (skill == null) return "";
            bool colored = args.Length < 1 || args[0].ToLower() != "false";
            try
            {
                return skill.Name(colored) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  KungfuSkillLvData查询 Name 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取技能描述文本。
        /// 格式: GetSkillDescribe=fullDetail-showDamage-bookDescribe 或 GetSkillDescribe(默认true-true-false)
        /// </summary>
        private static string CompositeGetSkillDescribe(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string[] args)
        {
            if (skill == null) return "";
            try
            {
                bool fullDetail = args.Length < 1 || args[0].ToLower() != "false";
                bool showDamage = args.Length < 2 || args[1].ToLower() != "false";
                bool bookDescribe = args.Length >= 3 && args[2].ToLower() == "true";
                return skill.GetSkillDescribe(fullDetail, showDamage, bookDescribe) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  KungfuSkillLvData查询 GetSkillDescribe 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取指定角色学习此技能的经验需求倍率。
        /// 格式: GetSkillNeedExpRate=角色ID 或 GetSkillNeedExpRate(默认targetInteractHero)
        /// </summary>
        private static string CompositeGetSkillNeedExpRate(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string[] args)
        {
            if (skill == null) return "";
            try
            {
                HeroData hero = CommonHandlers.ResolveHeroId(plotController, args.Length > 0 ? args[0] : null);
                if (hero == null) return "";
                return ConditionQueryHandlers.ConvertFieldValue(skill.GetSkillNeedExpRate(hero), typeof(float));
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  KungfuSkillLvData查询 GetSkillNeedExpRate 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取指定经验类型的最大经验值。
        /// 格式: SkillGetMaxExp=经验类型(0=战斗,1=秘籍) 或 SkillGetMaxExp(默认0)
        /// </summary>
        private static string CompositeSkillGetMaxExp(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string[] args)
        {
            if (skill == null) return "";
            try
            {
                int expType = 0;
                if (args.Length > 0 && int.TryParse(args[0], out int parsed))
                    expType = parsed;
                return ConditionQueryHandlers.ConvertFieldValue(skill.SkillGetMaxExp(expType), typeof(float));
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  KungfuSkillLvData查询 SkillGetMaxExp 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// KungfuSkillLvData Count 复合方法适配器。
        /// 格式: Count=属性/方法名
        /// </summary>
        private static string CompositeCount(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string[] args)
        {
            if (skill == null) return "";
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  KungfuSkillLvData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }
            return ConditionQueryHandlers.GenericCount(skill, "KungfuSkillLvData", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// KungfuSkillLvData Value 复合方法适配器。
        /// 格式: Value=属性/方法名-索引
        /// </summary>
        private static string CompositeValue(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string[] args)
        {
            if (skill == null) return "";
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  KungfuSkillLvData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericValue(skill, "KungfuSkillLvData", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// KungfuSkillLvData Index 复合方法适配器。
        /// 格式: Index=属性/方法名-查找值
        /// </summary>
        private static string CompositeIndex(PlotController plotController, Il2Cpp.KungfuSkillLvData skill, string[] args)
        {
            if (skill == null) return "";
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  KungfuSkillLvData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericIndex(skill, "KungfuSkillLvData", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }
    }
}
