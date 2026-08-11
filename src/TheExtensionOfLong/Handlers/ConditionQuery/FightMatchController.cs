using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// FightMatchController 查询指令。
    /// 格式: [$FightMatchController:比赛控制器信息$]
    /// </summary>
    [ConditionQuery("FightMatchController")]
    public static class QueryFightMatchController
    {
        private static readonly Dictionary<string, Func<Il2Cpp.FightMatchController, string[], string>> CompositeMethods
            = new Dictionary<string, Func<Il2Cpp.FightMatchController, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "GetMatchTypeName",             CompositeGetMatchTypeName },
            { "RoundFinished",                CompositeRoundFinished },
            { "FightCoupleHavePlayer",        CompositeFightCoupleHavePlayer },
            { "Count",                        CompositeCount },
            { "Value",                        CompositeValue },
            { "Index",                        CompositeIndex },
        };

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  FightMatchController查询: 参数不足，格式[FightMatchController:比赛控制器信息]");
                return "";
            }

            string fieldInfo = parts[1];

            Il2Cpp.FightMatchController fmc = Il2Cpp.FightMatchController.Instance;
            if (fmc == null)
            {
                LoggerManager.Warning("  FightMatchController查询: FightMatchController.Instance为空");
                return "";
            }

            // 处理复合方法
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(fmc, methodName, methodArg);
            }

            // 无参数的复合方法也支持不带=号调用
            if (CompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveCompositeMethod(fmc, fieldInfo, "");
            }

            // 普通属性/方法读取
            return ConditionQueryHandlers.ReadObjectFieldValue(fmc, "FightMatchController", fieldInfo);
        }

        private static string ResolveCompositeMethod(Il2Cpp.FightMatchController fmc, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(fmc, args);

            LoggerManager.Warning($"  FightMatchController查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        private static string CompositeGetMatchTypeName(Il2Cpp.FightMatchController fmc, string[] args)
        {
            try
            {
                return fmc.GetMatchTypeName() ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  FightMatchController查询 GetMatchTypeName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeRoundFinished(Il2Cpp.FightMatchController fmc, string[] args)
        {
            try
            {
                return fmc.RoundFinished() ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  FightMatchController查询 RoundFinished 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 判断对战是否包含玩家。
        /// 格式: FightCoupleHavePlayer=now/next 或 FightCoupleHavePlayer(默认now)
        /// </summary>
        private static string CompositeFightCoupleHavePlayer(Il2Cpp.FightMatchController fmc, string[] args)
        {
            try
            {
                string which = args.Length > 0 ? args[0].ToLower() : "now";
                FightMatchCouple couple = which == "next" ? fmc.nextFightMatchCouple : fmc.nowFightMatchCouple;
                if (couple == null) return "0";
                return fmc.FightCoupleHavePlayer(couple) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  FightMatchController查询 FightCoupleHavePlayer 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 读取FightMatchController中列表字段的Count。
        /// 支持: Count=HeroFinalList, Count=fightMatchCoupleList, Count=rewardList
        /// </summary>
        private static string CompositeCount(Il2Cpp.FightMatchController fmc, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                LoggerManager.Warning("  FightMatchController查询 Count: 参数格式为 Count=列表字段名，返回值: -1");
                return "-1";
            }
            try
            {
                string listName = args[0];
                var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
                Type fmcType = fmc.GetType();

                object listObj = null;
                PropertyInfo prop = fmcType.GetProperty(listName, bindingFlags);
                if (prop != null)
                    listObj = prop.GetValue(fmc);
                else
                {
                    FieldInfo field = fmcType.GetField(listName, bindingFlags);
                    if (field != null)
                        listObj = field.GetValue(fmc);
                }

                if (listObj == null)
                {
                    LoggerManager.Warning($"  FightMatchController查询 Count: 未找到属性/字段 \"{args[0]}\" 或其值为null，返回值: -1");
                    return "-1";
                }

                var countProp = listObj.GetType().GetProperty("Count");
                if (countProp == null)
                {
                    LoggerManager.Warning($"  FightMatchController查询 Count: \"{args[0]}\" 的类型 {listObj.GetType().Name} 没有 Count 属性，返回值: -1");
                    return "-1";
                }
                return countProp.GetValue(listObj)?.ToString() ?? "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  FightMatchController查询 Count({args[0]}) 失败: {e.Message}，返回值: -1");
                return "-1";
            }
        }

        /// <summary>
        /// 读取FightMatchController中列表字段的Value。
        /// 格式: Value=列表字段名-索引
        /// </summary>
        private static string CompositeValue(Il2Cpp.FightMatchController fmc, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  FightMatchController查询 Value: 参数不足，格式[Value=列表字段名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericValue(fmc, "FightMatchController", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// 从FightMatchController列表字段中按值查找索引。
        /// 格式: Index=列表字段名-查找值
        /// </summary>
        private static string CompositeIndex(Il2Cpp.FightMatchController fmc, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  FightMatchController查询 Index: 参数不足，格式[Index=列表字段名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericIndex(fmc, "FightMatchController", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }
    }
}
