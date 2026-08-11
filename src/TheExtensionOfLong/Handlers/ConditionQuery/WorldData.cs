using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// WorldData 查询指令。
    /// 格式: [$WorldData:世界信息$]
    /// </summary>
    [ConditionQuery("WorldData")]
    public static class QueryWorldData
    {
        private static readonly Dictionary<string, Func<PlotController, Il2Cpp.WorldData, string[], string>> CompositeMethods
            = new Dictionary<string, Func<PlotController, Il2Cpp.WorldData, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "FindTempTagID",                 CompositeFindTempTagID },
            { "HaveTempTag",                   CompositeHaveTempTag },
            { "HaveGameResultTriggered",       CompositeHaveGameResultTriggered },
            { "SkinUnlocked",                  CompositeSkinUnlocked },
            { "GetHero",                       CompositeGetHero },
            { "GetArea",                       CompositeGetArea },
            { "GetForce",                      CompositeGetForce },
            { "GetHeroForce",                  CompositeGetHeroForce },
            { "Count",                         CompositeCount },
            { "Value",                         CompositeValue },
            { "Index",                         CompositeIndex },
        };

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  WorldData查询: 参数不足，格式[WorldData:世界信息]");
                return "";
            }

            string fieldInfo = parts[1];

            Il2Cpp.WorldData worldData = CommonHandlers.GetWorldData();
            if (worldData == null)
            {
                LoggerManager.Warning("  WorldData查询: WorldData实例为空");
                return "";
            }

            // 处理复合方法（如 FindTempTagID=标签名）
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(plotController, worldData, methodName, methodArg);
            }

            // 无参数的复合方法也支持不带=号调用
            if (CompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveCompositeMethod(plotController, worldData, fieldInfo, "");
            }

            // 普通属性/方法读取
            return ConditionQueryHandlers.ReadObjectFieldValue(worldData, "WorldData", fieldInfo);
        }

        private static string ResolveCompositeMethod(PlotController plotController, Il2Cpp.WorldData worldData, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, worldData, args);

            LoggerManager.Warning($"  WorldData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        private static string CompositeFindTempTagID(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                LoggerManager.Warning("  WorldData查询 FindTempTagID: 缺少标签名参数");
                return "";
            }
            try
            {
                return worldData.FindTempTagID(args[0]).ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 FindTempTagID({args[0]}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeHaveTempTag(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                LoggerManager.Warning("  WorldData查询 HaveTempTag: 缺少标签名参数");
                return "";
            }
            try
            {
                var tag = worldData.FindTempTag(args[0]);
                return tag != null ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 HaveTempTag({args[0]}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeHaveGameResultTriggered(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            try
            {
                if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
                    return worldData.HaveGameResultTriggered() ? "1" : "0";

                if (int.TryParse(args[0], out int resultID))
                    return worldData.HaveGameResultTriggered(resultID) ? "1" : "0";

                LoggerManager.Warning($"  WorldData查询 HaveGameResultTriggered: 无效的结局ID \"{args[0]}\"");
                return "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 HaveGameResultTriggered 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeSkinUnlocked(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[0], out int skinID) || !int.TryParse(args[1], out int skinLv))
            {
                LoggerManager.Warning("  WorldData查询 SkinUnlocked: 参数格式为 皮肤ID-皮肤等级，如 SkinUnlocked=1-2");
                return "";
            }
            try
            {
                return worldData.SkinUnlocked(skinID, skinLv) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 SkinUnlocked({skinID},{skinLv}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetHero(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                LoggerManager.Warning("  WorldData查询 GetHero: 缺少角色ID参数");
                return "";
            }
            try
            {
                HeroData hero;
                if (int.TryParse(args[0], out int heroIdInt))
                    hero = worldData.GetHero(heroIdInt);
                else
                    hero = worldData.GetHero(args[0]);

                // GetHero 返回的是对象，返回角色名或ID作为字符串
                if (hero == null) return "";
                return hero.heroName;
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 GetHero({args[0]}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetArea(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                LoggerManager.Warning("  WorldData查询 GetArea: 缺少区域名参数");
                return "";
            }
            try
            {
                var area = worldData.GetArea(args[0]);
                return area != null ? area.areaName ?? args[0] : "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 GetArea({args[0]}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForce(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0]))
            {
                LoggerManager.Warning("  WorldData查询 GetForce: 缺少势力名参数");
                return "";
            }
            try
            {
                var force = worldData.GetForce(args[0]);
                return force != null ? force.forceName ?? args[0] : "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 GetForce({args[0]}) 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetHeroForce(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int heroId))
            {
                LoggerManager.Warning("  WorldData查询 GetHeroForce: 需要角色int ID参数");
                return "";
            }
            try
            {
                var force = worldData.GetHeroForce(heroId);
                return force != null ? force.forceName ?? "" : "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  WorldData查询 GetHeroForce({args[0]}) 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// WorldData Count 复合方法适配器。
        /// 格式: Count=属性/方法名
        /// </summary>
        private static string CompositeCount(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  WorldData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }
            return ConditionQueryHandlers.GenericCount(worldData, "WorldData", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// WorldData Value 复合方法适配器。
        /// 格式: Value=属性/方法名-索引
        /// </summary>
        private static string CompositeValue(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  WorldData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericValue(worldData, "WorldData", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// WorldData Index 复合方法适配器。
        /// 格式: Index=属性/方法名-查找值
        /// </summary>
        private static string CompositeIndex(PlotController plotController, Il2Cpp.WorldData worldData, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  WorldData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericIndex(worldData, "WorldData", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }
    }
}
