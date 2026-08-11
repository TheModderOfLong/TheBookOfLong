using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    /// <summary>
    /// MissionData 查询指令。
    /// 格式: [$MissionData:任务信息:任务来源(可选)$]
    /// 任务来源为空时默认读取 PlotController.nowMission。
    /// </summary>
    [ConditionQuery("MissionData")]
    public static class QueryMissionData
    {
        private static readonly Dictionary<string, Func<PlotController, MissionData, string[], string>> CompositeMethods
            = new Dictionary<string, Func<PlotController, MissionData, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "IsNowMission", CompositeIsNowMission },
            { "NeedFinished", CompositeNeedFinished },
            { "RelateToHero", CompositeRelateToHero },
            { "HaveTargetArea", CompositeHaveTargetArea },
            { "HaveTargetInn", CompositeHaveTargetInn },
            { "GetMissionDescribe", CompositeGetMissionDescribe },
            { "GetMissionBaseDescribe", CompositeGetMissionBaseDescribe },
            { "GetMissionExtraDescribe", CompositeGetMissionExtraDescribe },
            { "GetMissionTargetDescribe", CompositeGetMissionTargetDescribe },
            { "GetTriggerTargetDescribe", CompositeGetTriggerTargetDescribe },
            { "Count", CompositeCount },
            { "Value", CompositeValue },
            { "Index", CompositeIndex },
        };

        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  MissionData查询: 参数不足，格式[MissionData:任务信息:任务来源(可选)]");
                return "";
            }

            string fieldInfo = parts[1];
            string missionSource = JoinQueryParts(parts, 2);

            MissionData mission = CommonHandlers.ResolveMissionSource(plotController, missionSource);
            if (mission == null)
            {
                LoggerManager.Warning($"  MissionData查询: 未找到任务 (source=\"{(string.IsNullOrEmpty(missionSource) ? "nowMission" : missionSource)}\")");
                return "";
            }

            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveCompositeMethod(plotController, mission, methodName, methodArg);
            }

            if (CompositeMethods.ContainsKey(fieldInfo))
                return ResolveCompositeMethod(plotController, mission, fieldInfo, "");

            return ConditionQueryHandlers.ReadObjectFieldValue(mission, "MissionData", fieldInfo);
        }

        private static string JoinQueryParts(string[] parts, int startIndex)
        {
            if (parts == null || parts.Length <= startIndex)
                return "";

            string result = parts[startIndex] ?? "";
            for (int i = startIndex + 1; i < parts.Length; i++)
            {
                result += ":" + (parts[i] ?? "");
            }

            return result;
        }

        private static string ResolveCompositeMethod(PlotController plotController, MissionData mission, string methodName, string methodArg)
        {
            string[] args = string.IsNullOrEmpty(methodArg) ? new string[0] : methodArg.Split('-');

            if (CompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, mission, args);

            LoggerManager.Warning($"  MissionData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        private static string CompositeIsNowMission(PlotController plotController, MissionData mission, string[] args)
        {
            return plotController != null && plotController.nowMission == mission ? "1" : "0";
        }

        private static string CompositeNeedFinished(PlotController plotController, MissionData mission, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "MissionData查询 NeedFinished", "需求ID", out int needDataID))
                return "0";

            try { return mission.MissionNeedFinished(needDataID) ? "1" : "0"; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 NeedFinished 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeRelateToHero(PlotController plotController, MissionData mission, string[] args)
        {
            if (!CommonHandlers.TryGetHeroIdArg(plotController, args, 0, "MissionData查询 RelateToHero", out int heroID))
                return "0";

            try { return mission.MissionRelateToHero(heroID) ? "1" : "0"; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 RelateToHero 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeHaveTargetArea(PlotController plotController, MissionData mission, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "MissionData查询 HaveTargetArea", "区域ID", out int areaID))
                return "0";

            try
            {
                var areas = mission.GetTargetAreaID();
                return areas != null && areas.Contains(areaID) ? "1" : "0";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 HaveTargetArea 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeHaveTargetInn(PlotController plotController, MissionData mission, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "MissionData查询 HaveTargetInn", "客栈ID", out int innID))
                return "0";

            try { return mission.GetTargetInnID() == innID ? "1" : "0"; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 HaveTargetInn 失败: {e.Message}");
                return "0";
            }
        }

        private static string CompositeGetMissionDescribe(PlotController plotController, MissionData mission, string[] args)
        {
            bool showMissionTargetType = CommonHandlers.GetBoolArg(args, 0, true);
            bool showDifficulty = CommonHandlers.GetBoolArg(args, 1, true);
            bool showFinishRate = CommonHandlers.GetBoolArg(args, 2, true);
            bool showForceContribution = CommonHandlers.GetBoolArg(args, 3, true);

            try { return mission.GetMissionDescribe(showMissionTargetType, showDifficulty, showFinishRate, showForceContribution) ?? ""; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 GetMissionDescribe 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetMissionBaseDescribe(PlotController plotController, MissionData mission, string[] args)
        {
            bool showFinishRate = CommonHandlers.GetBoolArg(args, 0, true);
            try { return mission.GetMissionBaseDescribe(showFinishRate) ?? ""; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 GetMissionBaseDescribe 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetMissionExtraDescribe(PlotController plotController, MissionData mission, string[] args)
        {
            bool showMissionTargetType = CommonHandlers.GetBoolArg(args, 0, true);
            bool showDifficulty = CommonHandlers.GetBoolArg(args, 1, true);
            bool showFinishRate = CommonHandlers.GetBoolArg(args, 2, true);
            bool showForceContribution = CommonHandlers.GetBoolArg(args, 3, true);

            try { return mission.GetMissionExtraDescribe(showMissionTargetType, showDifficulty, showFinishRate, showForceContribution) ?? ""; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 GetMissionExtraDescribe 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetMissionTargetDescribe(PlotController plotController, MissionData mission, string[] args)
        {
            bool showFinishRate = CommonHandlers.GetBoolArg(args, 0, true);
            try { return mission.GetMissionTargetDescribe(showFinishRate) ?? ""; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 GetMissionTargetDescribe 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeGetTriggerTargetDescribe(PlotController plotController, MissionData mission, string[] args)
        {
            int targetID = CommonHandlers.GetIntArg(args, 0, 0);
            bool unclear = CommonHandlers.GetBoolArg(args, 1, false);

            try { return mission.GetTriggerTargetDescribe(targetID, unclear) ?? ""; }
            catch (Exception e)
            {
                LoggerManager.Error($"  MissionData查询 GetTriggerTargetDescribe 失败: {e.Message}");
                return "";
            }
        }

        private static string CompositeCount(PlotController plotController, MissionData mission, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  MissionData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "-1";
            }

            return ConditionQueryHandlers.GenericCount(mission, "MissionData", args[0], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static string CompositeValue(PlotController plotController, MissionData mission, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  MissionData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }

            return ConditionQueryHandlers.GenericValue(mission, "MissionData", args[0], index, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }

        private static string CompositeIndex(PlotController plotController, MissionData mission, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  MissionData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "-1";
            }

            return ConditionQueryHandlers.GenericIndex(mission, "MissionData", args[0], args[1], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.IgnoreCase);
        }
    }
}
