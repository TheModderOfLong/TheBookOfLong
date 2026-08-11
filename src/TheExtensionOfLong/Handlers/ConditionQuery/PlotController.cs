using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// PlotController 查询指令。
    /// 格式: [$PlotController:剧情控制器信息$]
    /// </summary>
    [ConditionQuery("PlotController")]
    public static class QueryPlotController
    {
        private static readonly Dictionary<string, Func<Il2Cpp.PlotController, string[], string>> CompositeMethods
            = new Dictionary<string, Func<Il2Cpp.PlotController, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "CheckPlotAvailable",            CompositeCheckPlotAvailable },
            { "GetResourceText",               CompositeGetResourceText },
            { "TargetInteractHeroInsidePlayerTeam", CompositeTargetInteractHeroInsidePlayerTeam },
            { "HaveNoPlotWait",                CompositeHaveNoPlotWait },
            { "GetLoseFightExcapeRate",        CompositeGetLoseFightExcapeRate },
            { "GetNowEventDifficulty",         CompositeGetNowEventDifficulty },
            { "Count",                         CompositeCount },
            { "Value",                         CompositeValue },
            { "Index",                         CompositeIndex },
        };

        public static string TryQuery(Il2Cpp.PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  PlotController查询: 参数不足，格式[PlotController:剧情控制器信息]");
                return "";
            }

            string fieldInfo = parts[1];

            Il2Cpp.PlotController pc = Il2Cpp.PlotController._instance;
            if (pc == null)
            {
                LoggerManager.Warning("  PlotController查询: PlotController._instance为空");
                return "";
            }

            // 处理复合方法
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(pc, methodName, methodArg);
            }

            // 无参数的复合方法也支持不带=号调用
            if (CompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveCompositeMethod(pc, fieldInfo, "");
            }

            // 普通属性/方法读取
            return ConditionQueryHandlers.ReadObjectFieldValue(pc, "PlotController", fieldInfo);
        }

        private static string ResolveCompositeMethod(Il2Cpp.PlotController pc, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(pc, args);

            LoggerManager.Warning($"  PlotController查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        private static string CompositeCheckPlotAvailable(Il2Cpp.PlotController pc, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int plotID))
            {
                LoggerManager.Warning("  PlotController查询 CheckPlotAvailable: 参数格式为 CheckPlotAvailable=剧情intID");
                return "0";
            }
            try
            {
                return pc.CheckPlotAvailable(plotID) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  PlotController查询 CheckPlotAvailable 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetResourceText(Il2Cpp.PlotController pc, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int id))
            {
                LoggerManager.Warning("  PlotController查询 GetResourceText: 参数格式为 GetResourceText=资源intID");
                return "";
            }
            try
            {
                return pc.GetResourceText(id) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  PlotController查询 GetResourceText 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeTargetInteractHeroInsidePlayerTeam(Il2Cpp.PlotController pc, string[] args)
        {
            try
            {
                return pc.TargetInteractHeroInsidePlayerTeam() ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  PlotController查询 TargetInteractHeroInsidePlayerTeam 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeHaveNoPlotWait(Il2Cpp.PlotController pc, string[] args)
        {
            try
            {
                return pc.HaveNoPlotWait() ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  PlotController查询 HaveNoPlotWait 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetLoseFightExcapeRate(Il2Cpp.PlotController pc, string[] args)
        {
            try
            {
                return pc.GetLoseFightExcapeRate().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  PlotController查询 GetLoseFightExcapeRate 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetNowEventDifficulty(Il2Cpp.PlotController pc, string[] args)
        {
            try
            {
                return pc.GetNowEventDifficulty().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  PlotController查询 GetNowEventDifficulty 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 读取PlotController中列表字段的Count。
        /// 支持: Count=plotInteractHeroList, Count=tempPlotHero, Count=tempPlotShop, Count=plotQueue, Count=eventQueue
        /// </summary>
        private static string CompositeCount(Il2Cpp.PlotController pc, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                LoggerManager.Warning("  PlotController查询 Count: 参数格式为 Count=列表字段名，返回值: -1");
                return "-1";
            }
            try
            {
                string listName = args[0];

                var bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase;
                Type pcType = pc.GetType();

                object listObj = null;
                PropertyInfo prop = pcType.GetProperty(listName, bindingFlags);
                if (prop != null)
                    listObj = prop.GetValue(pc);
                else
                {
                    FieldInfo field = pcType.GetField(listName, bindingFlags);
                    if (field != null)
                        listObj = field.GetValue(pc);
                }

                if (listObj == null)
                {
                    LoggerManager.Warning($"  PlotController查询 Count: 未找到属性/字段 \"{args[0]}\" 或其值为null，返回值: -1");
                    return "-1";
                }

                return ConditionQueryHandlers.ResolveCountFromObject(listObj, "PlotController", listName);
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  PlotController查询 Count({args[0]}) 失败: {e.Message}，返回值: -1");
                return "-1";
            }
        }

        /// <summary>
        /// 读取PlotController中列表字段的Value。
        /// 格式: Value=列表字段名-索引
        /// </summary>
        private static string CompositeValue(Il2Cpp.PlotController pc, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  PlotController查询 Value: 参数不足，格式[Value=列表字段名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericValue(pc, "PlotController", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// 从PlotController列表字段中按值查找索引。
        /// 格式: Index=列表字段名-查找值
        /// </summary>
        private static string CompositeIndex(Il2Cpp.PlotController pc, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  PlotController查询 Index: 参数不足，格式[Index=列表字段名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericIndex(pc, "PlotController", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }
    }
}
