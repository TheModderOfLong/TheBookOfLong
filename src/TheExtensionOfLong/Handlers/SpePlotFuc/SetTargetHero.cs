using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("SetTargetHero")]
    public static class SpePlotFucSetTargetHero
    {
        /// <summary>
        /// 尝试调用"SetTargetHero"功能，将指定角色设置为当前剧情环境中的角色对象
        /// 格式: SetTargetHero*角色ID/角色名称/关键字角色#对象(可选)
        ///   角色ID/角色名称/关键字角色: "NULL"(忽略大小写)时进入清除模式，将目标槽位设为null
        ///   对象(可选): 要设置的目标槽位，默认为targetInteractHero
        ///     支持的关键字: targetInteractHero/目标互动角色, sourceInteractHero/源互动角色,
        ///                  PlotInteractHero[-Index]/剧情互动角色[-索引]
        ///   PlotInteractHero行为(正常模式)：
        ///     不指定Index → 追加到PlotInteractHeroList末尾
        ///     指定Index且Index存在 → 替换该位置
        ///     指定Index但Index越界 → 追加到末尾
        ///   PlotInteractHero行为(NULL模式)：
        ///     不指定Index → 清空plotInteractHeroList
        ///     指定Index且Index存在 → 移除该位置的角色
        ///     指定Index但Index越界 → 不操作并警告
        /// 示例: SetTargetHero*小白                              → 将小白设为targetInteractHero
        ///       SetTargetHero*player#sourceInteractHero         → 将玩家设为sourceInteractHero
        ///       SetTargetHero*ChooseHero                        → 将选中角色设为targetInteractHero
        ///       SetTargetHero*小白#PlotInteractHero             → 将小白追加到PlotInteractHeroList
        ///       SetTargetHero*小白#PlotInteractHero-1           → 将小白设为PlotInteractHeroList[1]，越界则追加
        ///       SetTargetHero*NULL#targetInteractHero           → 将targetInteractHero设为null
        ///       SetTargetHero*NULL#PlotInteractHero             → 清空plotInteractHeroList
        ///       SetTargetHero*NULL#PlotInteractHero-0           → 移除plotInteractHeroList[0]
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID/角色名称/关键字角色#对象(可选)]");
                return;
            }

            string heroRef = fucParams[0];
            string targetSlot = fucParams.Length > 1 && !string.IsNullOrWhiteSpace(fucParams[1]) ? fucParams[1] : null;

            if (string.IsNullOrWhiteSpace(heroRef))
            {
                LoggerManager.Warning($"{fucName}: 角色引用不能为空");
                return;
            }

            // NULL模式：将目标槽位设为null（清除模式）
            bool isNullMode = heroRef.Equals("NULL", System.StringComparison.OrdinalIgnoreCase);

            // 解析角色引用
            HeroData heroData = null;
            if (!isNullMode)
            {
                heroData = CommonHandlers.ResolveHeroId(__instance, heroRef, null);
                if (heroData == null)
                {
                    LoggerManager.Warning($"{fucName}: 未找到角色 \"{heroRef}\"");
                    return;
                }
            }

            // 解析目标槽位并设置
            string lowerSlot = (targetSlot ?? "").ToLower().Trim();

            if (string.IsNullOrEmpty(lowerSlot) || lowerSlot == "targetinteracthero" || lowerSlot == "目标互动角色")
            {
                __instance.targetInteractHero = heroData;
                if (isNullMode)
                    LoggerManager.Debug($"{fucName}: 已将 targetInteractHero 设为 null");
                else
                    LoggerManager.Debug($"{fucName}: 已将 {heroData.heroName}(ID={heroData.heroID}) 设为 targetInteractHero");
            }
            else if (lowerSlot == "sourceinteracthero" || lowerSlot == "源互动角色")
            {
                __instance.sourceInteractHero = heroData;
                if (isNullMode)
                    LoggerManager.Debug($"{fucName}: 已将 sourceInteractHero 设为 null");
                else
                    LoggerManager.Debug($"{fucName}: 已将 {heroData.heroName}(ID={heroData.heroID}) 设为 sourceInteractHero");
            }
            else if (lowerSlot.StartsWith("plotinteracthero") || lowerSlot.StartsWith("剧情互动角色"))
            {
                List<HeroData> plotInteractHeroList = __instance.plotInteractHeroList;

                // NULL模式下，若plotInteractHeroList为null则先初始化
                if (plotInteractHeroList == null)
                {
                    if (isNullMode)
                    {
                        plotInteractHeroList = new List<HeroData>();
                        __instance.plotInteractHeroList = plotInteractHeroList;
                        LoggerManager.Debug($"{fucName}: plotInteractHeroList为空，已初始化为新的List<HeroData>");
                    }
                    else
                    {
                        LoggerManager.Warning($"{fucName}: plotInteractHeroList为空，无法操作剧情互动角色");
                        return;
                    }
                }

                // 尝试解析 "-Index" 后缀
                int dashPos = targetSlot.IndexOf('-');
                if (dashPos < 0)
                {
                    // 不指定Index
                    if (isNullMode)
                    {
                        // NULL模式 → 清空plotInteractHeroList
                        plotInteractHeroList.Clear();
                        LoggerManager.Debug($"{fucName}: 已清空 plotInteractHeroList");
                    }
                    else
                    {
                        // 正常模式 → 追加到末尾
                        plotInteractHeroList.Add(heroData);
                        LoggerManager.Debug($"{fucName}: 已将 {heroData.heroName}(ID={heroData.heroID}) 追加到 plotInteractHeroList[{plotInteractHeroList.Count - 1}]");
                    }
                }
                else
                {
                    string indexPart = targetSlot.Substring(dashPos + 1);
                    if (!int.TryParse(indexPart, out int index))
                    {
                        LoggerManager.Warning($"{fucName}: 剧情互动角色索引解析失败: {indexPart}");
                        return;
                    }

                    if (isNullMode)
                    {
                        // NULL模式 → 移除指定Index
                        if (index >= 0 && index < plotInteractHeroList.Count)
                        {
                            plotInteractHeroList.RemoveAt(index);
                            LoggerManager.Debug($"{fucName}: 已移除 plotInteractHeroList[{index}]");
                        }
                        else
                        {
                            LoggerManager.Warning($"{fucName}: 索引{index}越界(列表长度:{plotInteractHeroList.Count})，无法移除");
                        }
                    }
                    else
                    {
                        if (index >= 0 && index < plotInteractHeroList.Count)
                        {
                            // Index存在 → 替换
                            plotInteractHeroList[index] = heroData;
                            LoggerManager.Debug($"{fucName}: 已将 {heroData.heroName}(ID={heroData.heroID}) 设为 plotInteractHeroList[{index}]");
                        }
                        else
                        {
                            // Index越界 → 追加到末尾
                            plotInteractHeroList.Add(heroData);
                            LoggerManager.Debug($"{fucName}: 索引{index}越界(列表长度:{plotInteractHeroList.Count - 1})，已将 {heroData.heroName}(ID={heroData.heroID}) 追加到 plotInteractHeroList[{plotInteractHeroList.Count - 1}]");
                        }
                    }
                }
            }
            else
            {
                LoggerManager.Warning($"{fucName}: 不支持的目标槽位 \"{targetSlot}\"，支持: targetInteractHero/sourceInteractHero/PlotInteractHero[-Index]");
            }
        }
    }
}
