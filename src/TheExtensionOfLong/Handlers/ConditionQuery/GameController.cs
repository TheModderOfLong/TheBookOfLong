using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// GameController 查询指令。
    /// 格式: [$GameController:信息名$]
    /// </summary>
    [ConditionQuery("GameController")]
    public static class QueryGameController
    {
        private static readonly Dictionary<string, Func<PlotController, Il2Cpp.GameController, string[], string>> CompositeMethods
            = new Dictionary<string, Func<PlotController, Il2Cpp.GameController, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "GetHeroName",               CompositeGetHeroName },
            { "SameForce",                 CompositeSameForce },
            { "EnemyForce",                CompositeEnemyForce },
            { "IsPlayerForce",             CompositeIsPlayerForce },
            //{ "GetPlayerForceTotalArea",   CompositeGetPlayerForceTotalArea },
            { "GetTimeDifficulty",         CompositeGetTimeDifficulty },
            { "GetTimeDifficultyRate",     CompositeGetTimeDifficultyRate },
            { "CanSaveLoad",               CompositeCanSaveLoad },
            { "HaveSpeUI",                 CompositeHaveSpeUI },
            { "Count",                     CompositeCount },
            { "Value",                     CompositeValue },
            { "Index",                     CompositeIndex },
        };

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  GameController查询: 参数不足，格式[GameController:信息名]");
                return "";
            }

            string fieldInfo = parts[1];

            Il2Cpp.GameController gc = Il2Cpp.GameController._instance;
            if (gc == null)
            {
                LoggerManager.Warning("  GameController查询: GameController._instance为空");
                return "";
            }

            // 处理复合方法（如 GetHeroName=1-2）
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(plotController, gc, methodName, methodArg);
            }

            // 无参数的复合方法也支持不带=号调用
            if (CompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveCompositeMethod(plotController, gc, fieldInfo, "");
            }

            // 普通属性/方法读取
            return ConditionQueryHandlers.ReadObjectFieldValue(gc, "GameController", fieldInfo);
        }

        private static string ResolveCompositeMethod(PlotController plotController, Il2Cpp.GameController gc, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, gc, args);

            LoggerManager.Warning($"  GameController查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        /// <summary>
        /// 获取角色名称（基于上下文关系）。
        /// 格式: GetHeroName=sourceHeroID-targetHeroID 或 GetHeroName=sourceHero(默认targetInteractHero)
        /// </summary>
        private static string CompositeGetHeroName(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "";
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  GameController查询 GetHeroName: 缺少源角色ID参数");
                return "";
            }
            try
            {
                HeroData sourceHero = CommonHandlers.ResolveHeroId(plotController, args[0]);
                if (sourceHero == null) return "";
                string targetIdRaw = args.Length > 1 ? args[1] : null;
                HeroData targetHero = CommonHandlers.ResolveHeroId(plotController, targetIdRaw);
                if (targetHero == null) return "";
                return gc.GetHeroName(sourceHero, targetHero) ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 GetHeroName 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 判断两个角色是否同势力。
        /// 格式: SameForce=sourceHero-targetHero
        /// </summary>
        private static string CompositeSameForce(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "0";
            if (args.Length < 2)
            {
                LoggerManager.Warning("  GameController查询 SameForce: 需要两个角色参数");
                return "0";
            }
            try
            {
                HeroData sourceHero = CommonHandlers.ResolveHeroId(plotController, args[0]);
                HeroData targetHero = CommonHandlers.ResolveHeroId(plotController, args[1]);
                if (sourceHero == null || targetHero == null) return "0";
                return gc.SameForce(sourceHero, targetHero) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 SameForce 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 判断两个角色是否敌对势力。
        /// 格式: EnemyForce=sourceHero-targetHero
        /// </summary>
        private static string CompositeEnemyForce(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "0";
            if (args.Length < 2)
            {
                LoggerManager.Warning("  GameController查询 EnemyForce: 需要两个角色参数");
                return "0";
            }
            try
            {
                HeroData sourceHero = CommonHandlers.ResolveHeroId(plotController, args[0]);
                HeroData targetHero = CommonHandlers.ResolveHeroId(plotController, args[1]);
                if (sourceHero == null || targetHero == null) return "0";
                return gc.EnemyForce(sourceHero, targetHero) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 EnemyForce 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 判断指定势力是否为玩家势力。
        /// 格式: IsPlayerForce=forceID
        /// </summary>
        private static string CompositeIsPlayerForce(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "0";
            if (args.Length < 1 || !int.TryParse(args[0], out int forceID))
            {
                LoggerManager.Warning("  GameController查询 IsPlayerForce: 缺少势力ID参数");
                return "0";
            }
            try
            {
                return gc.IsPlayerForce(forceID) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 IsPlayerForce 失败: {e.Message}");
                return "0";
            }
        }

        ///// <summary>
        ///// 获取玩家势力总区域数
        ///// 格式: GetPlayerForceTotalArea
        ///// 示例:
        /////   [$GameController:GetPlayerForceTotalArea$]  → 玩家势力总区域数
        ///// </summary>
        //private static string CompositeGetPlayerForceTotalArea(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        //{
        //    if (gc == null) return "";
        //    try
        //    {
        //        return gc.GetPlayerForceTotalArea().ToString();
        //    }
        //    catch (Exception e)
        //    {
        //        LoggerManager.Error($"  GameController查询 GetPlayerForceTotalArea 失败: {e.Message}");
        //        return "";
        //    }
        //}

        /// <summary>
        /// 获取当前时间难度。
        /// 格式: GetTimeDifficulty
        /// </summary>
        private static string CompositeGetTimeDifficulty(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "";
            try
            {
                return gc.GetTimeDifficulty().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 GetTimeDifficulty 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取时间难度比率。
        /// 格式: GetTimeDifficultyRate
        /// </summary>
        private static string CompositeGetTimeDifficultyRate(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "";
            try
            {
                return gc.GetTimeDifficultyRate().ToString("G");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 GetTimeDifficultyRate 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 判断是否可以存档。
        /// 格式: CanSaveLoad 或 CanSaveLoad=includeHeroDetail(可选，默认true)
        /// </summary>
        private static string CompositeCanSaveLoad(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "0";
            bool includeHeroDetail = args.Length < 1 || args[0].ToLower() != "false";
            try
            {
                return gc.CanSaveLoad(includeHeroDetail) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 CanSaveLoad 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// 判断是否有特殊UI。
        /// 格式: HaveSpeUI 或 HaveSpeUI=includeHeroDetail(可选，默认true)
        /// </summary>
        private static string CompositeHaveSpeUI(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "0";
            bool includeHeroDetail = args.Length < 1 || args[0].ToLower() != "false";
            try
            {
                return gc.HaveSpeUI(includeHeroDetail) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GameController查询 HaveSpeUI 失败: {e.Message}");
                return "0";
            }
        }

        /// <summary>
        /// GameController Count 复合方法适配器。
        /// 格式: Count=属性/方法名
        /// </summary>
        private static string CompositeCount(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "";
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  GameController查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }
            return ConditionQueryHandlers.GenericCount(gc, "GameController", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// GameController Value 复合方法适配器。
        /// 格式: Value=属性/方法名-索引
        /// </summary>
        private static string CompositeValue(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "";
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  GameController查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericValue(gc, "GameController", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        /// <summary>
        /// GameController Index 复合方法适配器。
        /// 格式: Index=属性/方法名-查找值
        /// </summary>
        private static string CompositeIndex(PlotController plotController, Il2Cpp.GameController gc, string[] args)
        {
            if (gc == null) return "";
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  GameController查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericIndex(gc, "GameController", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }
    }
}
