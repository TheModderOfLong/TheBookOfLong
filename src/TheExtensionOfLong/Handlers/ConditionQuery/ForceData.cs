using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// ForceData 查询指令。
    /// 格式: [$ForceData:势力信息:势力ID/名称/关键字(可选)$]
    /// </summary>
    [ConditionQuery("ForceData")]
    public static class QueryForceData
    {
        private static readonly Dictionary<string, Func<PlotController, ForceData, string[], string>> CompositeMethods
            = new Dictionary<string, Func<PlotController, ForceData, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "GetLeaderID", CompositeGetLeaderID },
            { "GetLeaderName", CompositeGetLeaderName },
            { "MainAreaID", CompositeMainAreaID },
            { "MainAreaName", CompositeMainAreaName },
            { "GetForceFavor", CompositeGetForceFavor },
            { "GetForceFavorRate", CompositeGetForceFavorRate },
            { "GetForceStartFavor", CompositeGetForceStartFavor },
            { "GetForceRelationshipText", CompositeGetForceRelationshipText },
            { "GetForceStopWarTime", CompositeGetForceStopWarTime },
            { "IsAllyForce", CompositeIsAllyForce },
            { "CanAttack", CompositeCanAttack },
            { "GetResourcePercent", CompositeGetResourcePercent },
            { "HaveResource", CompositeHaveResource },
            { "GetOwnHeroID", CompositeGetOwnHeroID },
            { "GetOwnHeroName", CompositeGetOwnHeroName },
            { "HaveHero", CompositeHaveHero },
            { "Count", CompositeCount },
            { "Value", CompositeValue },
            { "Index", CompositeIndex },
        };

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  ForceData查询: 参数不足，格式[ForceData:势力信息:势力ID/名称/关键字(可选)]");
                return "";
            }

            string fieldInfo = parts[1];
            string forceIdRaw = parts.Length > 2 ? parts[2] : null;

            ForceData force = CommonHandlers.ResolveForceId(plotController, forceIdRaw);
            if (force == null)
            {
                LoggerManager.Warning($"  ForceData查询: 未找到势力 (id=\"{forceIdRaw}\")");
                return "";
            }

            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(plotController, force, methodName, methodArg);
            }

            if (CompositeMethods.ContainsKey(fieldInfo))
                return ResolveCompositeMethod(plotController, force, fieldInfo, "");

            return ConditionQueryHandlers.ReadObjectFieldValue(force, "ForceData", fieldInfo);
        }

        private static string ResolveCompositeMethod(PlotController plotController, ForceData force, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, force, args);

            LoggerManager.Warning($"  ForceData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        private static string CompositeGetLeaderID(PlotController plotController, ForceData force, string[] args)
        {
            try
            {
                HeroData leader = force.GetLeader();
                return leader != null ? leader.heroID.ToString() : "-1";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetLeaderID 失败: {e.Message}");
                return "-1";
            }
        }

        private static string CompositeGetLeaderName(PlotController plotController, ForceData force, string[] args)
        {
            try
            {
                HeroData leader = force.GetLeader();
                return leader?.heroName ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetLeaderName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeMainAreaID(PlotController plotController, ForceData force, string[] args)
        {
            try
            {
                AreaData area = force.MainArea();
                return area != null ? area.areaID.ToString() : force.mainAreaID.ToString();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 MainAreaID 失败: {e.Message}");
                return "-1";
            }
        }

        private static string CompositeMainAreaName(PlotController plotController, ForceData force, string[] args)
        {
            try
            {
                AreaData area = force.MainArea();
                return area?.areaName ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 MainAreaName 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceFavor(PlotController plotController, ForceData force, string[] args)
        {
            if (!TryResolveTargetForceID(plotController, args, 0, "GetForceFavor", out int targetForceID))
                return "";

            try { return force.GetForceFavor(targetForceID).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetForceFavor 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceFavorRate(PlotController plotController, ForceData force, string[] args)
        {
            if (!TryResolveTargetForce(plotController, args, 0, "GetForceFavorRate", out ForceData targetForce))
                return "";

            try { return force.GetForceFavorRate(targetForce).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetForceFavorRate 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceStartFavor(PlotController plotController, ForceData force, string[] args)
        {
            if (!TryResolveTargetForceID(plotController, args, 0, "GetForceStartFavor", out int targetForceID))
                return "";

            try { return force.GetForceStartFavor(targetForceID).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetForceStartFavor 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceRelationshipText(PlotController plotController, ForceData force, string[] args)
        {
            if (!TryResolveTargetForceID(plotController, args, 0, "GetForceRelationshipText", out int targetForceID))
                return "";

            bool useDarkColor = CommonHandlers.GetBoolArg(args, 1, true);
            try { return force.GetForceRelationshipText(targetForceID, useDarkColor) ?? ""; }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetForceRelationshipText 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetForceStopWarTime(PlotController plotController, ForceData force, string[] args)
        {
            if (!TryResolveTargetForceID(plotController, args, 0, "GetForceStopWarTime", out int targetForceID))
                return "";

            try { return force.GetForceStopWarTime(targetForceID).ToString(); }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetForceStopWarTime 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeIsAllyForce(PlotController plotController, ForceData force, string[] args)
        {
            if (!TryResolveTargetForceID(plotController, args, 0, "IsAllyForce", out int targetForceID))
                return "0";

            try { return force.IsAllyForce(targetForceID) ? "1" : "0"; }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 IsAllyForce 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeCanAttack(PlotController plotController, ForceData force, string[] args)
        {
            if (!TryResolveTargetForceID(plotController, args, 0, "CanAttack", out int targetForceID))
                return "0";

            try { return force.CanAttack(targetForceID) ? "1" : "0"; }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 CanAttack 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetResourcePercent(PlotController plotController, ForceData force, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ForceData查询 GetResourcePercent", "资源ID", out int resourceID))
                return "";

            try { return force.GetResourcePercent(resourceID).ToString("G"); }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 GetResourcePercent 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeHaveResource(PlotController plotController, ForceData force, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ForceData查询 HaveResource", "资源ID", out int resourceID) ||
                !CommonHandlers.TryGetFloatArg(args, 1, "ForceData查询 HaveResource", "数量", out float num))
            {
                return "0";
            }

            try { return force.HaveResource(resourceID, num) ? "1" : "0"; }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceData查询 HaveResource 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetOwnHeroID(PlotController plotController, ForceData force, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ForceData查询 GetOwnHeroID", "索引", out int index))
                return "-1";

            if (force.ownHeros == null || index < 0 || index >= force.ownHeros.Count)
                return "-1";

            return force.ownHeros[index].ToString();
        }

        private static string CompositeGetOwnHeroName(PlotController plotController, ForceData force, string[] args)
        {
            string heroID = CompositeGetOwnHeroID(plotController, force, args);
            if (!int.TryParse(heroID, out int id) || id < 0)
                return "";

            HeroData hero = CommonHandlers.GetWorldData()?.GetHero(id);
            return hero?.heroName ?? "";
        }

        private static string CompositeHaveHero(PlotController plotController, ForceData force, string[] args)
        {
            if (!CommonHandlers.TryGetHeroIdArg(plotController, args, 0, "ForceData查询 HaveHero", out int heroID))
                return "0";

            return force.ownHeros != null && force.ownHeros.Contains(heroID) ? "1" : "0";
        }

        private static string CompositeCount(PlotController plotController, ForceData force, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  ForceData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "-1";
            }

            return ConditionQueryHandlers.GenericCount(force, "ForceData", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static string CompositeValue(PlotController plotController, ForceData force, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  ForceData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }

            return ConditionQueryHandlers.GenericValue(force, "ForceData", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static string CompositeIndex(PlotController plotController, ForceData force, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  ForceData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "-1";
            }

            return ConditionQueryHandlers.GenericIndex(force, "ForceData", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static bool TryResolveTargetForce(PlotController plotController, string[] args, int index, string actionName, out ForceData targetForce)
        {
            targetForce = null;
            if (args == null || args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
            {
                LoggerManager.Warning($"  ForceData查询 {actionName}: 缺少目标势力参数");
                return false;
            }

            targetForce = CommonHandlers.ResolveForceId(plotController, args[index]);
            if (targetForce == null)
            {
                LoggerManager.Warning($"  ForceData查询 {actionName}: 未找到目标势力 \"{args[index]}\"");
                return false;
            }

            return true;
        }

        private static bool TryResolveTargetForceID(PlotController plotController, string[] args, int index, string actionName, out int forceID)
        {
            forceID = -1;
            if (!TryResolveTargetForce(plotController, args, index, actionName, out ForceData targetForce))
                return false;

            forceID = targetForce.forceID;
            return true;
        }
    }
}
