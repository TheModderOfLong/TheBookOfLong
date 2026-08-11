using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppSystem.Collections.Generic;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 对 WorldData.GetHero(string) 的 HarmonyPatch
    /// 当 heroName 为特定关键字时，按关键字语义返回角色数据，而非按名字遍历搜索
    /// 
    /// 背景：PlotController.GetHeroData 内部对关键字做了特殊处理，
    ///       但外部代码直接调用 WorldData.GetHero(string) 时会绕过 GetHeroData，
    ///       导致关键字（如"目标互动角色"、"剧情互动角色"等）无法被识别。
    ///       此 Patch 在 WorldData.GetHero(string) 层面拦截同样的关键字，
    ///       确保无论从哪条路径调用，关键字都能正确解析。
    /// 
    /// 支持：目标互动角色/targetInteractHero → targetInteractHero,
    ///       源互动角色/sourceInteractHero → sourceInteractHero,
    ///       临时剧情角色:Index/TempPlotHero → tempPlotHero[Index]（无:Index时取[0]）,
    ///       剧情互动角色:Index/PlotInteractHero → plotInteractHeroList[Index]（无:Index时取[0]）,
    ///       任务目标角色/MissionEventTargetHero → 同 PlotController.GetHeroData case,
    ///       任务发起角色/MissionEventSourceHero → 同 PlotController.GetHeroData case,
    ///       选中角色/ChooseHero → 返回选择器选中的角色
    /// 
    /// 注意："保持/Keep" 关键字不适用于此方法（无 origin 参数），仅在 GetHeroData 中有效
    /// </summary>
    [HarmonyPatch(typeof(WorldData), "GetHero", new[] { typeof(string) })]
    public static class WorldDataGetHeroPatch
    {
        [HarmonyPrefix]
        public static bool GetHeroStringPrefix(
            WorldData __instance,
            string heroName,
            ref HeroData __result)
        {
            if (heroName == null)
                return true; // 不拦截，走原逻辑

            string lowerName = heroName.ToLower();

            // "player" 或 "玩家" → 返回玩家角色
            if (lowerName == "player")
            {
                __result = CommonHandlers.GetPlayerHero();
                LoggerManager.Debug("WorldData.GetHero(string): heroName为玩家/player，返回=" + __result?.heroName);
                return false;
            }

            // "目标互动角色" 或 "targetInteractHero" → 返回 targetInteractHero
            if (lowerName == "目标互动角色" || lowerName == "targetinteracthero" || lowerName == "targethero")
            {
                PlotController pc = PlotController._instance;
                __result = pc?.targetInteractHero;
                LoggerManager.Debug("WorldData.GetHero(string): heroName为目标互动角色/targetInteractHero，返回=" + __result?.heroName);
                return false;
            }

            // "源互动角色" 或 "sourceInteractHero" → 返回 sourceInteractHero
            if (lowerName == "源互动角色" || lowerName == "sourceinteracthero" || lowerName == "sourcehero")
            {
                PlotController pc = PlotController._instance;
                __result = pc?.sourceInteractHero;
                LoggerManager.Debug("WorldData.GetHero(string): heroName为源互动角色/sourceInteractHero，返回=" + __result?.heroName);
                return false;
            }

            // "临时剧情角色:Index" 或 "TempPlotHero" → 同 case tempPlotHero
            // 无 ":Index" 后缀时取 tempPlotHero[0]
            if (lowerName.StartsWith("tempplothero") || lowerName.StartsWith("临时剧情角色"))
            {
                PlotController pc = PlotController._instance;
                List<HeroData> tempPlotHero = pc?.tempPlotHero;
                if (tempPlotHero == null || tempPlotHero.Count == 0)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 临时剧情角色模式，但tempPlotHero为空或null");
                    __result = null;
                    return false;
                }

                int index = 0;
                // 尝试解析 ":Index" 后缀
                int colonPos = heroName.IndexOf('：');
                if (colonPos < 0)
                    colonPos = heroName.IndexOf(':');

                if (colonPos >= 0)
                {
                    string indexStr = heroName.Substring(colonPos + 1);
                    if (!int.TryParse(indexStr, out index))
                    {
                        LoggerManager.Warning("WorldData.GetHero(string): 临时剧情角色索引解析失败: " + indexStr);
                        __result = null;
                        return false;
                    }
                }

                if (index < 0 || index >= tempPlotHero.Count)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 临时剧情角色索引越界: " + index + ", 列表长度: " + tempPlotHero.Count);
                    __result = null;
                    return false;
                }

                __result = tempPlotHero[index];
                LoggerManager.Debug("WorldData.GetHero(string): heroName为临时剧情角色/TempPlotHero，返回tempPlotHero[" + index + "]=" + __result?.heroName);
                return false;
            }

            // "剧情互动角色:Index" 或 "PlotInteractHero" → 同 case plotInteractHeroList
            // 无 ":Index" 后缀时取 plotInteractHeroList[0]
            if (lowerName.StartsWith("plotinteracthero") || lowerName.StartsWith("plotinteractherolist") || lowerName.StartsWith("剧情互动角色"))
            {
                PlotController pc = PlotController._instance;
                List<HeroData> plotInteractHeroList = pc?.plotInteractHeroList;
                if (plotInteractHeroList == null || plotInteractHeroList.Count == 0)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 剧情互动角色模式，但plotInteractHeroList为空或null");
                    __result = null;
                    return false;
                }

                int index = 0;
                // 尝试解析 ":Index" 后缀
                int colonPos = heroName.IndexOf('：');
                if (colonPos < 0)
                    colonPos = heroName.IndexOf(':');

                if (colonPos >= 0)
                {
                    string indexStr = heroName.Substring(colonPos + 1);
                    if (!int.TryParse(indexStr, out index))
                    {
                        LoggerManager.Warning("WorldData.GetHero(string): 剧情互动角色索引解析失败: " + indexStr);
                        __result = null;
                        return false;
                    }
                }

                if (index < 0 || index >= plotInteractHeroList.Count)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 剧情互动角色索引越界: " + index + ", 列表长度: " + plotInteractHeroList.Count);
                    __result = null;
                    return false;
                }

                __result = plotInteractHeroList[index];
                LoggerManager.Debug("WorldData.GetHero(string): heroName为剧情互动角色/PlotInteractHero，返回plotInteractHeroList[" + index + "]=" + __result?.heroName);
                return false;
            }

            // "任务目标角色" 或 "MissionEventTargetHero"
            if (lowerName == "任务目标角色" || lowerName == "missioneventtargethero")
            {
                PlotController pc = PlotController._instance;
                if (pc == null || pc.nowMission == null || pc.nowMission.missionTargetDatas == null)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 任务目标角色模式，但nowMission或missionTargetDatas为null");
                    __result = null;
                    return false;
                }

                if (pc.nowMission.missionTargetDatas.Count == 0)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 任务目标角色模式，但missionTargetDatas为空");
                    __result = null;
                    return false;
                }

                MissionTargetData targetData = pc.nowMission.missionTargetDatas[0];
                if (targetData == null)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 任务目标角色模式，但missionTargetDatas[0]为null");
                    __result = null;
                    return false;
                }

                int heroID = int.Parse(targetData.tirggerTargetID);
                __result = __instance.GetHero(heroID);
                LoggerManager.Debug("WorldData.GetHero(string): heroName为任务目标角色/MissionEventTargetHero，返回heroID=" + heroID);
                return false;
            }

            // "任务发起角色" 或 "MissionEventSourceHero"
            if (lowerName == "任务发起角色" || lowerName == "missioneventsourcehero")
            {
                PlotController pc = PlotController._instance;
                if (pc == null || pc.nowMission == null)
                {
                    LoggerManager.Warning("WorldData.GetHero(string): 任务发起角色模式，但PlotController或nowMission为null");
                    __result = null;
                    return false;
                }

                int heroID = pc.nowMission.sourceHeroID;
                __result = __instance.GetHero(heroID);
                LoggerManager.Debug("WorldData.GetHero(string): heroName为任务发起角色/MissionEventSourceHero，MissionEventSourceHero=" + __result?.heroName);
                return false;
            }

            // "选中角色" 或 "ChooseHero" → 返回选择器选中的角色
            if (lowerName == "选中角色" || lowerName == "choosehero" || lowerName == "chosenhero")
            {
                __result = ChooseController._instance?.chooseResult?.GetComponent<HeroIconController>()?.heroData;
                LoggerManager.Debug("WorldData.GetHero(string): heroName为选中角色/ChosenHero，返回=" + __result?.heroName);
                return false;
            }

            // 非关键字，走原逻辑
            return true;
        }

        /// <summary>
        /// 当本体按名称查询失败时，额外把纯数字字符串作为角色ID再查询一次。
        /// 这样可以让直接调用 WorldData.GetHero(string) 的旧入口兼容数字ID，
        /// 同时保留本体名称查询优先级，避免误伤名字本身为数字的角色。
        /// </summary>
        [HarmonyPostfix]
        public static void GetHeroStringPostfix(
            WorldData __instance,
            string heroName,
            ref HeroData __result)
        {
            if (__result != null)
                return;

            if (string.IsNullOrWhiteSpace(heroName))
                return;

            if (!int.TryParse(heroName, out int heroID))
                return;

            __result = __instance.GetHero(heroID);
            LoggerManager.Debug("WorldData.GetHero(string): 名称查询失败，按角色ID兜底查询 heroID=" + heroID + "，返回=" + __result?.heroName);
        }
    }
}
