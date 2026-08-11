using Il2Cpp;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheExtensionOfLong
{
    [ConditionQuery("GlobalData")]
    public static class QueryGlobalData
    {

        /// <summary>
        /// GlobalData 查询指令
        /// 格式: GlobalData:全局信息
        ///   全局信息: GlobalData静态属性/字段/无参方法名(忽略大小写)，或复合方法如 ForceLvName=0
        ///           复合方法多参数用"-"分隔
        /// 示例:
        ///   [$GlobalData:MaxLoverNum$]              → 最大情人数
        ///   [$GlobalData:PlayerForceID$]            → 玩家势力ID
        ///   [$GlobalData:BaseTravelSpeed$]          → 基础旅行速度
        ///   [$GlobalData:DisableAutoSave$]          → 是否禁用自动存档(0/1)
        ///   [$GlobalData:ForceLvName=0$]            → 第0级武力等级名
        ///   [$GlobalData:GetFavorText=85.5$]        → 85.5好感度对应的文本
        ///   [$GlobalData:GetBaseAttriName=Sword$]   → 剑法属性名
        ///   [$GlobalData:Count=AttriName$]          → 属性名称列表长度
        ///   [$GlobalData:Value=AttriName-0$]         → 第0个属性名称
        ///   [$GlobalData:Index=AttriName-剑法$]      → "剑法"在属性名列表中的索引
        /// </summary>
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2)
            {
                LoggerManager.Warning("  GlobalData查询: 参数不足，格式[GlobalData:全局信息]");
                return "";
            }

            string fieldInfo = parts[1];

            // 处理复合方法（如 ForceLvName=0）
            int eqIdx = fieldInfo.IndexOf('=');
            if (eqIdx >= 0)
            {
                string methodName = fieldInfo.Substring(0, eqIdx);
                string methodArg = fieldInfo.Substring(eqIdx + 1);
                return ResolveGlobalDataCompositeMethod(plotController, methodName, methodArg);
            }

            // 无参数的复合方法也支持不带=号调用
            if (GlobalDataCompositeMethods.ContainsKey(fieldInfo))
            {
                return ResolveGlobalDataCompositeMethod(plotController, fieldInfo, "");
            }

            // 普通静态属性/方法读取
            return ConditionQueryHandlers.ReadStaticFieldValue(typeof(GlobalData), "GlobalData", fieldInfo);
        }

        /// <summary>
        /// 通过反射读取GlobalData的List&lt;string&gt;字段指定索引的值
        /// </summary>
        private static string ReadGlobalDataStringListByIndex(string listFieldName, int index)
        {
            if (index < 0) return "";
            var bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;
            Type gdType = typeof(GlobalData);

            try
            {
                object listObj = null;
                PropertyInfo prop = gdType.GetProperty(listFieldName, bindingFlags);
                if (prop != null)
                    listObj = prop.GetValue(null);
                else
                {
                    FieldInfo field = gdType.GetField(listFieldName, bindingFlags);
                    if (field != null)
                        listObj = field.GetValue(null);
                }

                if (listObj == null) return "";

                // 通过反射访问 Count 和 Item[index]（兼容 System.List 和 Il2Cpp List）
                var countProp = listObj.GetType().GetProperty("Count");
                if (countProp == null) return "";
                int count = (int)countProp.GetValue(listObj);
                if (index >= count) return "";

                var itemProp = listObj.GetType().GetProperty("Item");
                if (itemProp == null) return "";
                object item = itemProp.GetValue(listObj, new object[] { index });
                return item?.ToString() ?? "";
            }
            catch (Exception e)
            {
                LoggerManager.Error($"  GlobalData查询 读取 {listFieldName}[{index}] 失败: {e.Message}");
                return "";
            }
        }

        /// <summary>
        /// 创建List&lt;string&gt;索引查询的复合方法委托
        /// </summary>
        private static Func<PlotController, string[], string> ListStringByIndex(string listFieldName)
        {
            return (pc, args) =>
            {
                if (args.Length < 1 || !int.TryParse(args[0], out int idx) || idx < 0) return "";
                return ReadGlobalDataStringListByIndex(listFieldName, idx);
            };
        }

        /// <summary>
        /// GlobalData 复合方法字典
        /// </summary>
        private static readonly Dictionary<string, Func<PlotController, string[], string>> GlobalDataCompositeMethods
            = new Dictionary<string, Func<PlotController, string[], string>>(StringComparer.OrdinalIgnoreCase)
        {
            // 名称表索引查询
            { "ForceLvName",        ListStringByIndex("HeroForceLvName") },
            { "EquipLvName",        ListStringByIndex("EquipLvName") },
            { "ItemTypeName",       ListStringByIndex("ItemTypeName") },
            { "AttriName",          ListStringByIndex("AttriName") },
            { "FavorLvText",        ListStringByIndex("FavorLvText") },
            { "SeasonName",         ListStringByIndex("SeasonName") },
            { "SkillLvName",        ListStringByIndex("SkillLvName") },
            { "EquipWeightLvName",  ListStringByIndex("EquipmentWeightLvName") },
            { "HeroGovernLvName",   ListStringByIndex("HeroGovernLvName") },
            { "BattleTypeName",     ListStringByIndex("BattleTypeName") },
            { "BookRareLvName",     ListStringByIndex("BookRareLvName") },
            { "TreasureRareLvName", ListStringByIndex("TreasureRareLvName") },
            // 文本转换方法
            { "GetFavorText",       GDCompositeGetFavorText },
            { "GetEvilText",        GDCompositeGetEvilText },
            { "GetChaosText",       GDCompositeGetChaosText },
            { "GetForceFavorLvText",GDCompositeGetForceFavorLvText },
            { "GetBaseAttriName",   GDCompositeGetBaseAttriName },
            { "GetHobbyString",     GDCompositeGetHobbyString },
            { "GetDifficultyStarString", GDCompositeGetDifficultyStarString },
            // 数值计算方法
            { "CountArmorDamageRate", GDCompositeCountArmorDamageRate },
            { "GetAttriLv",         GDCompositeGetAttriLv },
            { "GetGovernSalary",    GDCompositeGetGovernSalary },
            { "GetNumText",         GDCompositeGetNumText },
            { "ConvertNumToChinese", GDCompositeConvertNumToChinese },
            // 新增复合方法
            { "ReplaceSpeString",              GDCompositeReplaceSpeString },
            { "GenerateChangeColorText",       GDCompositeGenerateChangeColorText },
            { "GenerateRareLvColorText",       GDCompositeGenerateRareLvColorText },
            //{ "GetRandomBreakThroughRateLv",   GDCompositeGetRandomBreakThroughRateLv },
            { "CaculateWinRate",               GDCompositeCaculateWinRate },
            { "CaculateWinTeam",               GDCompositeCaculateWinTeam },
            { "GetItemTypeString",             GDCompositeGetItemTypeString },
            { "Count",                          GDCompositeCount },
            { "Value",                          GDCompositeValue },
            { "Index",                          GDCompositeIndex },
        };

        private static string ResolveGlobalDataCompositeMethod(PlotController plotController, string methodName, string methodArg)
        {
            string[] args = methodArg.Split('-');

            if (GlobalDataCompositeMethods.TryGetValue(methodName, out var handler))
                return handler(plotController, args);

            LoggerManager.Warning($"  GlobalData查询: 未知复合方法 \"{methodName}\"");
            return "";
        }

        // ===== GlobalData 复合方法实现 =====

        private static string GDCompositeGetFavorText(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float favor)) return "";
            try { return GlobalData.GetFavorText(favor) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetFavorText({favor}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetEvilText(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float evil)) return "";
            try { return GlobalData.GetEvilText(evil) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetEvilText({evil}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetChaosText(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float chaos)) return "";
            try { return GlobalData.GetChaosText(chaos) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetChaosText({chaos}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetForceFavorLvText(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float favorNum)) return "";
            try { return GlobalData.GetForceFavorLvText(favorNum) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetForceFavorLvText({favorNum}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetBaseAttriName(PlotController pc, string[] args)
        {
            string attriName = args.Length > 0 ? args[0] : null;
            if (!ConditionQueryHandlers.TryParseAttriType(attriName, out BaseAttriType attriType)) return "";
            try { return GlobalData.GetBaseAttriName(attriType) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetBaseAttriName({attriType}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetHobbyString(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int hobby)) return "";
            try { return GlobalData.GetHobbyString(hobby) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetHobbyString({hobby}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetDifficultyStarString(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float difficulty)) return "";
            try { return GlobalData.GetDifficultyStarString(difficulty) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetDifficultyStarString({difficulty}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeCountArmorDamageRate(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float armor)) return "";
            try { return GlobalData.CountArmorDamageRate(armor).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 CountArmorDamageRate({armor}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetAttriLv(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float targetNum)) return "";
            try { return GlobalData.GetAttriLv(targetNum).ToString(); }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetAttriLv({targetNum}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetGovernSalary(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int lv)) return "";
            try { return GlobalData.GetGovernSalary(lv).ToString(); }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetGovernSalary({lv}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeGetNumText(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int num)) return "";
            try { return GlobalData.GetNumText(num) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetNumText({num}) 失败: {e.Message}"); return ""; }
        }

        private static string GDCompositeConvertNumToChinese(PlotController pc, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out int input)) return "";
            try { return GlobalData.ConvertNumToChinese(input) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 ConvertNumToChinese({input}) 失败: {e.Message}"); return ""; }
        }

        /// <summary>
        /// 替换特殊文本
        /// 格式: ReplaceSpeString=文本-角色ID(可选,默认-1)
        /// 示例:
        ///   [$GlobalData:ReplaceSpeString=#PlayerName#$]           → 替换文本中的特殊标记
        ///   [$GlobalData:ReplaceSpeString=#PlayerName#-小白$]      → 以小白角色上下文替换特殊标记
        /// </summary>
        private static string GDCompositeReplaceSpeString(PlotController plotController, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrEmpty(args[0])) return "";
            string text = args[0];
            int heroID = -1;
            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                HeroData hero = CommonHandlers.ResolveHeroId(plotController, args[1]);
                if (hero != null)
                    heroID = hero.heroID;
                else if (!int.TryParse(args[1], out heroID))
                    heroID = -1;
            }
            try { return GlobalData.ReplaceSpeString(text, heroID) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 ReplaceSpeString 失败: {e.Message}"); return ""; }
        }

        /// <summary>
        /// 生成改变颜色文本
        /// 格式1: GenerateChangeColorText=名称-数量              → name+num模式（正向）
        /// 格式2: GenerateChangeColorText=名称-数量-是否反向     → name+num+reverse模式
        /// 格式3: GenerateChangeColorText=名称-是否正向(positive) → name+positive模式（无数量，第2参数为true/false）
        /// 示例:
        ///   [$GlobalData:GenerateChangeColorText=生命-10$]          → "生命+10"（正向绿色）
        ///   [$GlobalData:GenerateChangeColorText=生命--5$]          → "生命-5"（负向红色）
        ///   [$GlobalData:GenerateChangeColorText=生命-10-true$]     → 反向模式，10显示为红色
        ///   [$GlobalData:GenerateChangeColorText=状态-true$]        → name+positive模式
        /// </summary>
        private static string GDCompositeGenerateChangeColorText(PlotController pc, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrEmpty(args[0])) return "";
            string name = args[0];

            // 判断第2参数是bool还是数值：如果是true/false则使用name+positive重载
            string secondArg = args[1].Trim().ToLower();
            if (secondArg == "true" || secondArg == "false")
            {
                bool positive = secondArg == "true";
                try { return GlobalData.GenerateChangeColorText(name, positive) ?? ""; }
                catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GenerateChangeColorText(name,positive) 失败: {e.Message}"); return ""; }
            }

            // 否则第2参数是数量(float)
            if (!float.TryParse(args[1], out float num)) return "";
            bool reverse = false;
            if (args.Length > 2)
            {
                string reverseStr = args[2].Trim().ToLower();
                reverse = reverseStr == "true" || reverseStr == "1";
            }
            try { return GlobalData.GenerateChangeColorText(name, num, reverse) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GenerateChangeColorText(name,num,reverse) 失败: {e.Message}"); return ""; }
        }

        /// <summary>
        /// 根据稀有等级设置文本颜色
        /// 格式: GenerateRareLvColorText=文本-稀有等级
        /// 示例:
        ///   [$GlobalData:GenerateRareLvColorText=铁剑-1$]  → 根据稀有等级1着色的"铁剑"
        /// </summary>
        private static string GDCompositeGenerateRareLvColorText(PlotController pc, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrEmpty(args[0])) return "";
            string text = args[0];
            if (!int.TryParse(args[1], out int rareLv)) return "";
            try { return GlobalData.GenerateRareLvColorText(text, rareLv) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GenerateRareLvColorText 失败: {e.Message}"); return ""; }
        }

        // 新版本已失效
        ///// <summary>
        ///// 获取随机突破率等级
        ///// 格式: GetRandomBreakThroughRateLv=技能稀有等级
        ///// 示例:
        /////   [$GlobalData:GetRandomBreakThroughRateLv=3$]  → 稀有等级3对应的随机突破率等级
        ///// </summary>
        //private static string GDCompositeGetRandomBreakThroughRateLv(PlotController pc, string[] args)
        //{
        //    if (args.Length < 1 || !int.TryParse(args[0], out int skillRareLv)) return "";
        //    try { return GlobalData.GetRandomBreakThroughRateLv(skillRareLv).ToString(); }
        //    catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetRandomBreakThroughRateLv({skillRareLv}) 失败: {e.Message}"); return ""; }
        //}

        /// <summary>
        /// 计算队伍胜率
        /// 格式: CaculateWinRate=队伍1评分-队伍2评分
        /// 示例:
        ///   [$GlobalData:CaculateWinRate=2400-1800$]  → 队伍1对队伍2的胜率
        /// </summary>
        private static string GDCompositeCaculateWinRate(PlotController pc, string[] args)
        {
            if (args.Length < 2 || !float.TryParse(args[0], out float team0Score) || !float.TryParse(args[1], out float team1Score)) return "";
            try { return GlobalData.CaculateWinRate(team0Score, team1Score).ToString("G"); }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 CaculateWinRate 失败: {e.Message}"); return ""; }
        }

        /// <summary>
        /// 计算胜利队伍
        /// 格式: CaculateWinTeam=队伍1评分-队伍2评分
        /// 返回: 0或1，表示哪个队伍获胜
        /// 示例:
        ///   [$GlobalData:CaculateWinTeam=2400-1800$]  → 返回0或1
        /// </summary>
        private static string GDCompositeCaculateWinTeam(PlotController pc, string[] args)
        {
            if (args.Length < 2 || !float.TryParse(args[0], out float team0Score) || !float.TryParse(args[1], out float team1Score)) return "";
            try { return GlobalData.CaculateWinTeam(team0Score, team1Score).ToString(); }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 CaculateWinTeam 失败: {e.Message}"); return ""; }
        }

        /// <summary>
        /// 获取物品类型文本
        /// 格式: GetItemTypeString=类型-子类型
        ///   类型: int (0=Equip, 1=Med, 2=Food, 3=Book, 4=Treasure, 5=Material, 6=Horse)
        ///   子类型: int
        /// 示例:
        ///   [$GlobalData:GetItemTypeString=0-1$]  → 装备子类型1的文本
        /// </summary>
        private static string GDCompositeGetItemTypeString(PlotController pc, string[] args)
        {
            if (args.Length < 2 || !int.TryParse(args[0], out int type) || !int.TryParse(args[1], out int littleType)) return "";
            try { return GlobalData.GetItemTypeString(type, littleType) ?? ""; }
            catch (Exception e) { LoggerManager.Error($"  GlobalData查询 GetItemTypeString({type},{littleType}) 失败: {e.Message}"); return ""; }
        }


        /// <summary>
        /// GlobalData Count 复合方法适配器
        /// 格式: Count=属性/方法名
        /// 示例:
        ///   [$GlobalData:Count=AttriName$]        → 属性名称列表长度
        /// </summary>
        private static string GDCompositeCount(PlotController plotController, string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                LoggerManager.Warning("  GlobalData查询 Count: 参数不足，格式[Count=属性/方法名]");
                return "";
            }
            return ConditionQueryHandlers.GenericStaticCount(typeof(GlobalData), "GlobalData", args[0]);
        }


        /// <summary>
        /// GlobalData Value 复合方法适配器
        /// 格式: Value=属性/方法名-索引
        /// 示例:
        ///   [$GlobalData:Value=AttriName-0$]         → 第0个属性名称
        ///   [$GlobalData:Value=FavorLvText-2$]       → 第2级好感度文本
        /// </summary>
        private static string GDCompositeValue(PlotController plotController, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || !int.TryParse(args[1], out int index))
            {
                LoggerManager.Warning("  GlobalData查询 Value: 参数不足，格式[Value=属性/方法名-索引]");
                return "";
            }
            return ConditionQueryHandlers.GenericStaticValue(typeof(GlobalData), "GlobalData", args[0], index);
        }


        /// <summary>
        /// GlobalData Index 复合方法适配器
        /// 格式: Index=属性/方法名-查找值
        /// 示例:
        ///   [$GlobalData:Index=AttriName-剑法$]      → "剑法"在属性名列表中的索引
        /// </summary>
        private static string GDCompositeIndex(PlotController plotController, string[] args)
        {
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[0]) || string.IsNullOrWhiteSpace(args[1]))
            {
                LoggerManager.Warning("  GlobalData查询 Index: 参数不足，格式[Index=属性/方法名-查找值]");
                return "";
            }
            return ConditionQueryHandlers.GenericStaticIndex(typeof(GlobalData), "GlobalData", args[0], args[1]);
        }
    }
}
