using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("HeroGetChosenItem")]
    public static class SpePlotFucHeroGetChosenItem
    {
        /// <summary>
        /// 让目标角色获取选择器选择的物品
        /// 配合 ShowChoosePanel（HeroItem模式）使用，作为选中物品后的回调处理
        /// 支持两种参数分隔格式：
        ///   格式1(#分隔): HeroGetChosenItem*目标角色ID/名称#是否显示弹窗(可选,默认true)#源角色ID/名称(可选)#是否关联好感度(可选)#好感度变化上限(可选)
        ///   格式2(-分隔): HeroGetChosenItem*目标角色ID/名称-是否显示弹窗(可选,默认true)-源角色ID/名称(可选)-是否关联好感度(可选)-好感度变化上限(可选)
        ///   目标角色ID/名称: 接收物品的角色，支持ID/名称/关键字(player等)
        ///   是否显示弹窗: true/1 显示获取弹窗, false/0 不显示, 默认true
        ///   源角色ID/名称: 不为空时，源角色将尝试失去该物品
        ///   是否关联好感度: "1"或"True"(忽略大小写)时，目标角色根据物品价值变化好感度
        ///   好感度变化上限: 决定好感度变化的上限，不指定时不限制上限
        /// 示例: HeroGetChosenItem*小白                          → 小白获得选中物品，显示弹窗
        ///       HeroGetChosenItem*小白#false                    → 小白获得选中物品，不显示弹窗
        ///       HeroGetChosenItem*小白#true#player              → 玩家失去物品，小白获得物品，显示弹窗
        ///       HeroGetChosenItem*小白-true-player-1-20         → 玩家失去物品，小白获得物品，好感变化上限20
        /// 注意: 此指令需在 ShowChoosePanel(HeroItem) 选中物品后调用。
        ///       当作为 ShowChoosePanel 的回调参数嵌入时，由于 SpePlotFuc 以 # 分隔参数，
        ///       建议使用 - 分隔格式以避免 # 冲突。
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*目标角色ID/名称[#/-]是否显示弹窗(可选)[#/-]源角色ID/名称(可选)[#/-]是否关联好感度(可选)[#/-]好感度变化上限(可选)]");
                return;
            }

            // 兼容两种分隔格式：
            //   # 分隔：fucParams 已由 SpePlotFucPrefix 按 # 拆分，直接使用
            //   - 分隔：fucParams[0] 包含所有参数，需按 - 重新拆分
            string[] parsedParams;
            if (fucParams.Length == 1 && fucParams[0].Contains("-"))
            {
                parsedParams = fucParams[0].Split('-');
            }
            else
            {
                parsedParams = fucParams;
            }

            // 1. 从选择器获取选中物品
            ChooseController chooseController = ChooseController._instance;
            if (chooseController == null || chooseController.chooseResult == null)
            {
                LoggerManager.Error($"{fucName}: 选择器未选中物品");
                return;
            }

            ItemIconController itemIconCtrl = chooseController.chooseResult.GetComponent<ItemIconController>();
            if (itemIconCtrl == null || itemIconCtrl.itemData == null)
            {
                LoggerManager.Error($"{fucName}: 无法获取选中物品数据");
                return;
            }
            ItemData itemData = itemIconCtrl.itemData;

            // 2. 解析目标角色
            HeroData targetHero = CommonHandlers.ResolveHeroId(__instance, parsedParams[0]);
            if (targetHero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到目标角色 \"{parsedParams[0]}\"");
                return;
            }

            // 3. 解析是否显示弹窗（可选，默认true）
            bool showPopInfo = true;
            if (parsedParams.Length > 1 && !string.IsNullOrWhiteSpace(parsedParams[1]))
            {
                string showStr = parsedParams[1].ToLower().Trim();
                showPopInfo = !(showStr == "false" || showStr == "0");
            }

            // 4. 解析源角色（可选，不为空时源角色失去该物品）
            HeroData sourceHero = null;
            if (parsedParams.Length > 2 && !string.IsNullOrWhiteSpace(parsedParams[2]))
            {
                sourceHero = CommonHandlers.ResolveHeroId(__instance, parsedParams[2]);
                if (sourceHero == null)
                {
                    LoggerManager.Warning($"{fucName}: 未找到源角色 \"{parsedParams[2]}\"");
                }
            }

            // 5. 解析是否关联好感度（可选，"1"或"True"忽略大小写时启用）
            bool linkFavor = false;
            if (parsedParams.Length > 3 && !string.IsNullOrWhiteSpace(parsedParams[3]))
            {
                string favorStr = parsedParams[3].ToLower().Trim();
                linkFavor = (favorStr == "1" || favorStr == "true");
            }

            // 6. 解析好感度变化上限（可选，不指定时不限制上限）
            bool hasFavorMax = false;
            float favorMax = 0f;
            if (parsedParams.Length > 4 && !string.IsNullOrWhiteSpace(parsedParams[4]))
            {
                if (float.TryParse(parsedParams[4], out float maxVal) && maxVal > 0)
                {
                    hasFavorMax = true;
                    favorMax = maxVal;
                }
            }

            // 7. 源角色失去物品（如果指定了源角色）
            if (sourceHero != null)
            {
                sourceHero.LoseItem(itemData, showPopInfo);
                LoggerManager.Debug($"{fucName}: {sourceHero.heroName} 失去物品 {itemData.Name(true)}");
            }

            // 8. 目标角色获得物品
            targetHero.GetItem(itemData, showPopInfo);
            LoggerManager.Debug($"{fucName}: {targetHero.heroName} 获得物品 {itemData.Name(true)}, showPopInfo={showPopInfo}");

            // 9. 关联好感度变化
            if (linkFavor)
            {
                float itemFavorValue = targetHero.GetItemFavorValue(itemData, hasFavorMax ? favorMax : 99999f);
                __instance.PlotChangeHeroFavor(targetHero, itemFavorValue, 100f, 0f, false);
                LoggerManager.Debug($"{fucName}: {targetHero.heroName} 好感度变化 {itemFavorValue:F1}" + (hasFavorMax ? $", 上限 {favorMax}" : ", 无上限"));
            }
        }
    }
}
