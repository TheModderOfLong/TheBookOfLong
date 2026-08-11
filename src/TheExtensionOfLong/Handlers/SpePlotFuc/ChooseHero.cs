using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("ChooseHero")]
    public static class SpePlotFucChooseHero
    {
        /// <summary>
        /// 尝试调用"ChooseHero"功能，根据条件表达式筛选角色并弹出角色选择面板
        /// 格式: ChooseHero*角色筛选条件表达式#回调函数名(可选)#回调参数(可选)
        /// 条件表达式中可使用 [$HeroData:...$] 查询，当前被筛选角色默认作为targetInteractHero
        /// 也可通过 [$HeroData:...:player$] 等引用其他角色
        /// 示例: ChooseHero*[$HeroData:isFemale$][=]1 AND [$HeroData:dead$][=]0#OnHeroSelected#param1
        ///       ChooseHero*[$HeroData:Favor$][>=]50 AND [$HeroData:isFemale$] != [$HeroData:isFemale:player$]
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色筛选条件表达式#回调函数名(可选)#回调参数(可选)]");
                return;
            }

            string expression = fucParams[0];
            string callbackFuc = fucParams.Length > 1 && !string.IsNullOrWhiteSpace(fucParams[1]) ? fucParams[1] : null;
            string callbackParam = fucParams.Length > 2 && !string.IsNullOrWhiteSpace(fucParams[2]) ? fucParams[2] : null;
            string cancelFuc = fucParams.Length > 3 && !string.IsNullOrWhiteSpace(fucParams[3]) ? fucParams[3] : null;

            // 获取世界角色列表
            WorldData worldData = CommonHandlers.GetWorldData();
            if (worldData == null || worldData.Heros == null)
            {
                LoggerManager.Error($"{fucName}: WorldData或Heros为空");
                return;
            }

            // 保存原始 targetInteractHero，遍历筛选后恢复
            HeroData originalTargetHero = __instance.targetInteractHero;

            // 筛选角色
            List<HeroData> candidateList = new List<HeroData>();
            var heros = worldData.Heros;

            try
            {
                for (int i = 0; i < heros.Count; i++)
                {
                    HeroData hero = heros[i];
                    if (hero == null) continue;

                    // 临时设置 targetInteractHero 为当前被筛选角色
                    __instance.targetInteractHero = hero;

                    try
                    {
                        bool match = ConditionExpressionEvaluator.Evaluate(__instance, expression);
                        if (match)
                        {
                            candidateList.Add(hero);
                        }
                    }
                    catch (System.Exception e)
                    {
                        LoggerManager.Warning($"{fucName}: 评估角色 {hero.heroName} 条件时出错: {e.Message}");
                    }
                }
            }
            finally
            {
                // 确保恢复原始 targetInteractHero
                __instance.targetInteractHero = originalTargetHero;
            }

            LoggerManager.Debug($"{fucName}: 筛选完成，共{candidateList.Count}个候选角色");

            //if (candidateList.Count == 0)
            //{
            //    LoggerManager.Debug($"{fucName}: 没有符合条件的角色，不弹出面板");
            //    return;
            //}

            // 弹出角色选择面板
            ChooseController chooseController = ChooseController._instance;
            if (chooseController == null)
            {
                LoggerManager.Error($"{fucName}: ChooseController实例不存在");
                return;
            }

            chooseController.ShowChoosePanel(
                ChooseType.Hero,
                candidateList,
                __instance.gameObject,
                callbackFuc,
                callbackParam,
                ChooseFilterType.None,
                cancelFuc
            );
        }
    }
}
