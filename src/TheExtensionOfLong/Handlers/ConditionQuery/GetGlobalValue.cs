using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    internal static class GlobalValueQueryHelper
    {
        public static bool TryGetRawValue(string key, out string value)
        {
            value = "";
            if (string.IsNullOrWhiteSpace(key))
                return false;

            if (GameDataController.playerPrefData == null || GameDataController.playerPrefData.playerPrefData == null)
                return false;

            PlayerPrefDictionary playerPrefData = GameDataController.playerPrefData.playerPrefData;
            if (!playerPrefData.ContainsKey(key))
                return false;

            value = playerPrefData.GetString(key) ?? "";
            return true;
        }
    }

    /// <summary>
    /// 从玩家本地全局变量中读取字符串。
    /// 格式: [$GetGlobalStrVal:Key$]
    /// </summary>
    [ConditionQuery("GetGlobalStrVal")]
    public static class QueryGetGlobalStrVal
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2) return "";
            return GlobalValueQueryHelper.TryGetRawValue(parts[1], out string value) ? value : "";
        }
    }

    /// <summary>
    /// 从玩家本地全局变量中读取整数。
    /// 格式: [$GetGlobalIntVal:Key$]
    /// </summary>
    [ConditionQuery("GetGlobalIntVal")]
    public static class QueryGetGlobalIntVal
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2) return "0";
            string key = parts[1];
            if (!GlobalValueQueryHelper.TryGetRawValue(key, out string value))
                return "0";

            if (int.TryParse(value, out int intValue))
                return intValue.ToString();

            LoggerManager.Warning($"  GetGlobalIntVal查询({key}) 失败: 全局变量值不是有效整数，value={value}");
            return "0";
        }
    }

    /// <summary>
    /// 从玩家本地全局变量中读取浮点数。
    /// 格式: [$GetGlobalFloatVal:Key$]
    /// </summary>
    [ConditionQuery("GetGlobalFlotVal")]
    [ConditionQuery("GetGlobalFloatVal")]
    public static class QueryGetGlobalFloatVal
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            if (parts.Length < 2) return "0.0";
            string key = parts[1];
            if (!GlobalValueQueryHelper.TryGetRawValue(key, out string value))
                return "0.0";

            if (double.TryParse(value, out double doubleValue))
                return doubleValue.ToString("G");

            string queryName = parts.Length > 0 ? parts[0] : "GetGlobalFloatVal";
            LoggerManager.Warning($"  {queryName}查询({key}) 失败: 全局变量值不是有效浮点数，value={value}");
            return "0.0";
        }
    }
}
