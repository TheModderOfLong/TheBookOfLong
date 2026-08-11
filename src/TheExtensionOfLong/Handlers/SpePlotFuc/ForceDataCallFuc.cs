using HarmonyLib;
using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    [SpePlotFuc("ForceDataCallFuc")]
    public static class SpePlotFucForceDataCallFuc
    {
        /// <summary>
        /// ForceDataCallFuc 指令：对 ForceData 实例调用指定函数。
        /// 格式: ForceDataCallFuc*门派ID#函数名#函数参数(可选)
        ///   门派ID: 通过 CommonHandlers.ResolveForceId 解析（int ID / 名称 / player 等关键字）
        ///   函数名: ForceData 的无参方法名、复合动作名，或可写属性/字段名
        ///   函数参数: 复合动作的参数，用 "-" 分隔；当函数名为属性/字段名时，表示要写入的新值
        ///
        /// 支持的复合动作:
        ///   ChangeForceFavor=目标门派ID-好感变化-是否提示
        ///   SetForceFavor=目标门派ID-好感值
        ///   AddAllyForce=目标门派ID-是否反向-是否提示
        ///   BreakAllyForce=目标门派ID-是否反向-是否提示
        ///   SetForceStopWarTime=目标门派ID-时间-是否反向-是否提示
        ///   ChangeResource=资源ID-数量-是否提示-是否显示HUD
        ///   CostResource=资源ID-数量-是否提示
        ///   AddHero=角色ID
        ///   AddHero=角色ID-势力等级-辈分
        ///   RemoveHero=角色ID
        ///   SetLeader=角色ID-是否提示
        ///   UpgradeForceFavorDict
        ///   UpgradeNowResearch=是否提示
        ///   SetCustomValue#属性名=属性值（推荐）或 SetCustomValue=属性名-属性值（兼容）
        ///   字段名=字段值（写入公开实例属性或字段，支持数字、布尔、字符串、枚举）
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2 || string.IsNullOrWhiteSpace(fucParams[0]) || string.IsNullOrWhiteSpace(fucParams[1]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[ForceDataCallFuc*门派ID#函数名#函数参数(可选)]");
                return;
            }

            string forceIdRaw = fucParams[0];
            string funcInfo = fucParams[1];

            ForceData force = CommonHandlers.ResolveForceId(__instance, forceIdRaw);
            if (force == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到门派/势力 \"{forceIdRaw}\"");
                return;
            }

            int eqIdx = funcInfo.IndexOf('=');
            string methodName;
            string methodArg;

            if (eqIdx >= 0)
            {
                methodName = funcInfo.Substring(0, eqIdx);
                methodArg = funcInfo.Substring(eqIdx + 1);
            }
            else
            {
                methodName = funcInfo;
                methodArg = fucParams.Length > 2 ? fucParams[2] : "";
            }

            if (ForceCallFucCompositeActions.TryGetValue(methodName, out var handler))
            {
                if (methodName.Equals("SetCustomValue", StringComparison.OrdinalIgnoreCase))
                {
                    string[] customValueArgs = eqIdx >= 0
                        ? methodArg.Split('-')
                        : new string[] { methodArg };
                    handler(__instance, force, customValueArgs);
                    return;
                }

                string[] args = methodArg.Split('-');
                for (int i = 0; i < args.Length; i++)
                {
                    args[i] = args[i].Replace("负", "-");
                }

                handler(__instance, force, args);
                return;
            }

            if (eqIdx >= 0 || fucParams.Length > 2)
            {
                if (CommonHandlers.TryWriteObjectMemberValue(force, "ForceData", methodName, methodArg, out string oldValue, out string newValue))
                {
                    LoggerManager.Debug($"{fucName}: 已设置 {force.forceName}.{methodName}: {oldValue} → {newValue}");
                    return;
                }

                LoggerManager.Warning($"{fucName}: ForceData 未找到复合动作或可写公开字段/属性 \"{methodName}\"");
                return;
            }

            TryCallNoArgMethod(fucName, force, methodName);
        }

        private static readonly Dictionary<string, Action<PlotController, ForceData, string[]>> ForceCallFucCompositeActions
            = new Dictionary<string, Action<PlotController, ForceData, string[]>>(StringComparer.OrdinalIgnoreCase)
        {
            { "ChangeForceFavor",       ForceCallFucChangeForceFavor },
            { "SetForceFavor",          ForceCallFucSetForceFavor },
            { "AddAllyForce",           ForceCallFucAddAllyForce },
            { "BreakAllyForce",         ForceCallFucBreakAllyForce },
            { "SetForceStopWarTime",    ForceCallFucSetForceStopWarTime },
            { "ChangeResource",         ForceCallFucChangeResource },
            { "CostResource",           ForceCallFucCostResource },
            { "AddHero",                ForceCallFucAddHero },
            { "RemoveHero",             ForceCallFucRemoveHero },
            { "SetLeader",              ForceCallFucSetLeader },
            { "UpgradeForceFavorDict",  ForceCallFucUpgradeForceFavorDict },
            { "UpgradeNowResearch",     ForceCallFucUpgradeNowResearch },
            { "SetCustomValue",         ForceCallFucSetCustomValue },
        };

        private static void RunForceAction(ForceData force, string actionName, string logArgs, Action action)
        {
            try
            {
                action();
                LoggerManager.Debug($"  ForceDataCallFuc: {force.forceName}.{actionName}({logArgs})");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  ForceDataCallFuc {actionName} 失败: {e.Message}");
            }
        }

        private static void ForceCallFucChangeForceFavor(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetForceIdArg(pc, args, 0, "ForceDataCallFuc ChangeForceFavor", out int targetForceID)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ForceDataCallFuc ChangeForceFavor", "好感变化", out float favor)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, true);
            RunForceAction(force, "ChangeForceFavor", $"{targetForceID}, {favor}, {showInfo}", () => force.ChangeForceFavor(targetForceID, favor, showInfo));
        }

        private static void ForceCallFucSetForceFavor(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetForceIdArg(pc, args, 0, "ForceDataCallFuc SetForceFavor", out int targetForceID)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ForceDataCallFuc SetForceFavor", "好感值", out float favor)) return;
            RunForceAction(force, "SetForceFavor", $"{targetForceID}, {favor}", () => force.SetForceFavor(targetForceID, favor));
        }

        private static void ForceCallFucAddAllyForce(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetForceIdArg(pc, args, 0, "ForceDataCallFuc AddAllyForce", out int targetForceID)) return;
            bool back = CommonHandlers.GetBoolArg(args, 1, true);
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, true);
            RunForceAction(force, "AddAllyForce", $"{targetForceID}, {back}, {showInfo}", () => force.AddAllyForce(targetForceID, back, showInfo));
        }

        private static void ForceCallFucBreakAllyForce(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetForceIdArg(pc, args, 0, "ForceDataCallFuc BreakAllyForce", out int targetForceID)) return;
            bool back = CommonHandlers.GetBoolArg(args, 1, true);
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, true);
            RunForceAction(force, "BreakAllyForce", $"{targetForceID}, {back}, {showInfo}", () => force.BreakAllyForce(targetForceID, back, showInfo));
        }

        private static void ForceCallFucSetForceStopWarTime(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetForceIdArg(pc, args, 0, "ForceDataCallFuc SetForceStopWarTime", out int targetForceID)) return;
            if (!CommonHandlers.TryGetIntArg(args, 1, "ForceDataCallFuc SetForceStopWarTime", "停战时间", out int time)) return;
            bool back = CommonHandlers.GetBoolArg(args, 2, true);
            bool showInfo = CommonHandlers.GetBoolArg(args, 3, true);
            RunForceAction(force, "SetForceStopWarTime", $"{targetForceID}, {time}, {back}, {showInfo}", () => force.SetForceStopWarTime(targetForceID, time, back, showInfo));
        }

        private static void ForceCallFucChangeResource(PlotController pc, ForceData force, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ForceDataCallFuc ChangeResource", "资源ID", out int resourceID)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ForceDataCallFuc ChangeResource", "数量", out float num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, false);
            bool showHud = CommonHandlers.GetBoolArg(args, 3, true);
            RunForceAction(force, "ChangeResource", $"{resourceID}, {num}, {showInfo}, {showHud}", () => force.ChangeResource(resourceID, num, showInfo, showHud));
        }

        private static void ForceCallFucCostResource(PlotController pc, ForceData force, string[] args)
        {
            if (!CommonHandlers.TryGetIntArg(args, 0, "ForceDataCallFuc CostResource", "资源ID", out int resourceID)) return;
            if (!CommonHandlers.TryGetFloatArg(args, 1, "ForceDataCallFuc CostResource", "数量", out float num)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 2, false);
            RunForceAction(force, "CostResource", $"{resourceID}, {num}, {showInfo}", () => force.CostResource(resourceID, num, showInfo));
        }

        private static void ForceCallFucAddHero(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetHeroArg(pc, args, 0, "ForceDataCallFuc AddHero", out HeroData targetHero)) return;

            if (args != null && args.Length >= 3)
            {
                if (!CommonHandlers.TryGetIntArg(args, 1, "ForceDataCallFuc AddHero", "势力等级", out int forceLv)) return;
                if (!CommonHandlers.TryGetIntArg(args, 2, "ForceDataCallFuc AddHero", "辈分", out int generation)) return;
                RunForceAction(force, "AddHero", $"{targetHero.heroName}, {forceLv}, {generation}", () => force.AddHero(targetHero, forceLv, generation));
                return;
            }

            RunForceAction(force, "AddHero", targetHero.heroName, () => force.AddHero(targetHero));
        }

        private static void ForceCallFucRemoveHero(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetHeroArg(pc, args, 0, "ForceDataCallFuc RemoveHero", out HeroData targetHero)) return;
            RunForceAction(force, "RemoveHero", targetHero.heroName, () => force.RemoveHero(targetHero));
        }

        private static void ForceCallFucSetLeader(PlotController pc, ForceData force, string[] args)
        {
            if (!TryGetHeroArg(pc, args, 0, "ForceDataCallFuc SetLeader", out HeroData targetHero)) return;
            bool showInfo = CommonHandlers.GetBoolArg(args, 1, false);
            RunForceAction(force, "SetLeader", $"{targetHero.heroName}, {showInfo}", () => force.SetLeader(targetHero, showInfo));
        }

        private static void ForceCallFucUpgradeForceFavorDict(PlotController pc, ForceData force, string[] args)
        {
            RunForceAction(force, "UpgradeForceFavorDict", "", () => force.UpgradeForceFavorDict());
        }

        private static void ForceCallFucUpgradeNowResearch(PlotController pc, ForceData force, string[] args)
        {
            bool showInfo = CommonHandlers.GetBoolArg(args, 0, true);
            RunForceAction(force, "UpgradeNowResearch", $"{showInfo}", () => force.UpgradeNowResearch(showInfo));
        }

        private static void ForceCallFucSetCustomValue(PlotController pc, ForceData force, string[] args)
        {
            if (force == null)
                return;

            if (!TryParseCustomValueArgs(args, out string propertyName, out string value))
            {
                LoggerManager.Warning("  ForceDataCallFuc SetCustomValue: 参数不足，格式[SetCustomValue#属性名=属性值]或[SetCustomValue=属性名-属性值]");
                return;
            }

            string objectID = force.forceID.ToString();
            string key = CustomValueManager.GetKey("ForceData", objectID, propertyName);
            if (string.IsNullOrEmpty(key))
            {
                LoggerManager.Warning($"  ForceDataCallFuc SetCustomValue: 属性名无效 \"{propertyName}\"");
                return;
            }

            if (!CustomValueManager.SetRaw("ForceData", objectID, propertyName, value))
            {
                LoggerManager.Error($"  ForceDataCallFuc SetCustomValue: PlotEventLogData实例不存在，无法写入 {key}");
                return;
            }

            if (string.IsNullOrEmpty(value))
                LoggerManager.Debug($"  ForceDataCallFuc: 已删除自定义变量 {key}");
            else
                LoggerManager.Debug($"  ForceDataCallFuc: 已设置自定义变量 {key}={value}");
        }

        private static bool TryGetForceIdArg(PlotController plotController, string[] args, int index, string actionName, out int forceID)
        {
            forceID = 0;
            if (args == null || args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
            {
                LoggerManager.Warning($"{actionName}: 参数不足，缺少目标门派ID");
                return false;
            }

            ForceData targetForce = CommonHandlers.ResolveForceId(plotController, args[index]);
            if (targetForce == null)
            {
                LoggerManager.Warning($"{actionName}: 未找到目标门派/势力 \"{args[index]}\"");
                return false;
            }

            forceID = targetForce.forceID;
            return true;
        }

        private static bool TryGetHeroArg(PlotController plotController, string[] args, int index, string actionName, out HeroData hero)
        {
            hero = null;
            if (args == null || args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
            {
                LoggerManager.Warning($"{actionName}: 参数不足，缺少目标角色ID");
                return false;
            }

            hero = CommonHandlers.ResolveHeroId(plotController, args[index]);
            if (hero == null)
            {
                LoggerManager.Warning($"{actionName}: 未找到目标角色 \"{args[index]}\"");
                return false;
            }

            return true;
        }

        private static bool TryParseCustomValueArgs(string[] args, out string propertyName, out string value)
        {
            propertyName = "";
            value = "";

            if (args == null || args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
                return false;

            int eqIdx = args[0].IndexOf('=');
            if (eqIdx >= 0)
            {
                propertyName = args[0].Substring(0, eqIdx).Trim();
                value = args[0].Substring(eqIdx + 1);
                return !string.IsNullOrWhiteSpace(propertyName);
            }

            if (args.Length < 2)
                return false;

            propertyName = args[0].Trim();
            value = JoinArgs(args, 1, "-");
            return !string.IsNullOrWhiteSpace(propertyName);
        }

        private static string JoinArgs(string[] args, int startIndex, string separator)
        {
            if (args == null || startIndex >= args.Length)
                return "";

            string result = args[startIndex] ?? "";
            for (int i = startIndex + 1; i < args.Length; i++)
            {
                result += separator + (args[i] ?? "");
            }

            return result;
        }

        private static void TryCallNoArgMethod(string fucName, ForceData force, string methodName)
        {
            try
            {
                BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
                MethodInfo method = force.GetType().GetMethod(methodName, bindingFlags, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    LoggerManager.Warning($"{fucName}: ForceData 未找到无参方法 \"{methodName}\"，且非已知复合动作");
                    return;
                }

                object result = method.Invoke(force, null);
                string resultText = method.ReturnType == typeof(void) ? "" : $" => {result}";
                LoggerManager.Debug($"{fucName}: 已调用 {force.forceName}.{methodName}(){resultText}");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"{fucName}: 调用 {force.forceName}.{methodName}() 失败: {e.Message}");
            }
        }
    }
}
