using System;
using HarmonyLib;
using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 选项补丁统一入口
    /// 
    /// 各互动方法的 Postfix 中调用 PlotChoicePatchManager.ApplyPatches，
    /// 通过函数名匹配 PlotChoiceDataController 配置来注入/覆盖/删除选项。
    /// </summary>

    /// <summary>
    /// FurtherInteractWithNPC（关系）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "FurtherInteractWithNPC")]
    public static class PlotChoiceDataPatchFurtherInteractWithNPC
    {
        [HarmonyPostfix]
        public static void FurtherInteractWithNPCPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "FurtherInteractWithNPC");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: FurtherInteractWithNPC Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ChatWithNPC（论战）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "ChatWithNPC")]
    public static class PlotChoiceDataPatchChatWithNPC
    {
        [HarmonyPostfix]
        public static void ChatWithNPCPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "ChatWithNPC");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: ChatWithNPC Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ChangeNormalMeetNpcPlot（常规互动）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "ChangeNormalMeetNpcPlot")]
    public static class PlotChoiceDataPatchChangeNormalMeetNpcPlot
    {
        [HarmonyPostfix]
        public static void ChangeNormalMeetNpcPlotPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "ChangeNormalMeetNpcPlot");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: ChangeNormalMeetNpcPlot Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// LoverInteractWithNPC（情侣交互）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "LoverInteractWithNPC")]
    public static class PlotChoiceDataPatchLoverInteractWithNPC
    {
        [HarmonyPostfix]
        public static void LoverInteractWithNPCPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "LoverInteractWithNPC");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: LoverInteractWithNPC Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ForceInteractWithNPC（势力角色交互）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "ForceInteractWithNPC")]
    public static class PlotChoiceDataPatchForceInteractWithNPC
    {
        [HarmonyPostfix]
        public static void ForceInteractWithNPCPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "ForceInteractWithNPC");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: ForceInteractWithNPC Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// MeditationWorkContinue（修行）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "MeditationWorkContinue")]
    public static class PlotChoiceDataPatchMeditationWorkContinue
    {
        [HarmonyPostfix]
        public static void MeditationWorkContinuePostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "MeditationWorkContinue");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: MeditationWorkContinue Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// ChangeNpcPracticeSkillPlot（习武）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "ChangeNpcPracticeSkillPlot")]
    public static class PlotChoiceDataPatchChangeNpcPracticeSkillPlot
    {
        [HarmonyPostfix]
        public static void ChangeNpcPracticeSkillPlotPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "ChangeNpcPracticeSkillPlot");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: ChangeNpcPracticeSkillPlot Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// AskHeroMakeFriend（亲密）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "AskHeroMakeFriend")]
    public static class PlotChoiceDataPatchAskHeroMakeFriend
    {
        [HarmonyPostfix]
        public static void AskHeroMakeFriendPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "AskHeroMakeFriend");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: AskHeroMakeFriend Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// AskNPCMission（委托）的选项补丁入口
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "AskNPCMission")]
    public static class PlotChoiceDataPatchAskNPCMission
    {
        [HarmonyPostfix]
        public static void AskNPCMissionPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;
                PlotChoicePatchManager.ApplyPatches(__instance, "AskNPCMission");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: AskNPCMission Postfix 异常 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// PlotPrefabs 相关的选项补丁入口
    /// </summary>
    /// <summary>
    /// HomeRest锛堜紤鎭級鐨勯€夐」琛ヤ竵鍏ュ彛
    /// </summary>
    [HarmonyPatch(typeof(PlotController), "HomeRest")]
    public static class PlotChoiceDataPatchHomeRest
    {
        [HarmonyPostfix]
        public static void HomeRestPostfix(PlotController __instance)
        {
            if (__instance == null) return;

            try
            {
                PlotChoicePatchManager.ApplyPatches(__instance, "HomeRest");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: HomeRest Postfix 寮傚父 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// CityHouseBookRoom锛堜功鎴匡級鐨勯€夐」琛ヤ竵鍏ュ彛
    /// </summary>
    [HarmonyPatch(typeof(BuildingUIController), "CityHouseBookRoom")]
    public static class PlotChoiceDataPatchCityHouseBookRoom
    {
        [HarmonyPostfix]
        public static void CityHouseBookRoomPostfix()
        {
            try
            {
                PlotController plotController = PlotController._instance;
                if (plotController == null) return;

                PlotChoicePatchManager.ApplyPatches(plotController, "CityHouseBookRoom");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: CityHouseBookRoom Postfix 寮傚父 - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// CityHousePracticeRoom锛堢粌鍔熸埧锛夌殑閫夐」琛ヤ竵鍏ュ彛
    /// </summary>
    [HarmonyPatch(typeof(BuildingUIController), "CityHousePracticeRoom")]
    public static class PlotChoiceDataPatchCityHousePracticeRoom
    {
        [HarmonyPostfix]
        public static void CityHousePracticeRoomPostfix()
        {
            try
            {
                PlotController plotController = PlotController._instance;
                if (plotController == null) return;

                PlotChoicePatchManager.ApplyPatches(plotController, "CityHousePracticeRoom");
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: CityHousePracticeRoom Postfix 寮傚父 - {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PlotController), "ChangePlot", new[] { typeof(string) })]
    public static class PlotChoiceDataPatcPlotPrefabs
    {
        [HarmonyPostfix]
        public static void ChangePlotPostfix(PlotController __instance, string plotID)
        {
            if (__instance == null) return;

            try
            {
                if (__instance.targetInteractHero == null) return;

                if (plotID == "0")
                {
                    PlotChoicePatchManager.ApplyPatches(__instance, "ChangePlotPrefabToPractice");
                }
                else if(plotID == "1")
                {
                    PlotChoicePatchManager.ApplyPatches(__instance, "ChangePlotPrefabToKindness");
                }
                else if (plotID == "2")
                {
                    PlotChoicePatchManager.ApplyPatches(__instance, "ChangePlotPrefabToHostile");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"PlotChoiceDataPatch: ChangePlot Postfix 异常 - {ex.Message}");
            }
        }
    }

}
