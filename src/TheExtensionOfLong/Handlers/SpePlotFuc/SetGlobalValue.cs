using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置玩家本地全局变量。
    /// 格式: SetGlobalValue*Key#Value
    ///   Value为空字符串时删除Key。
    ///   数据写入 GameDataController.playerPrefData，会跨存档生效。
    /// </summary>
    [SpePlotFuc("SetGlobalValue")]
    public static class SpePlotFucSetGlobalValue
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 2)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*Key#Value]");
                return;
            }

            string key = fucParams[0];
            string value = fucParams[1];

            if (string.IsNullOrWhiteSpace(key))
            {
                LoggerManager.Warning($"{fucName}: Key不能为空或空白");
                return;
            }

            if (GameDataController.playerPrefData == null || GameDataController.playerPrefData.playerPrefData == null)
            {
                LoggerManager.Error($"{fucName}: playerPrefData实例不存在，无法设置全局变量");
                return;
            }

            try
            {
                PlayerPrefDictionary playerPrefData = GameDataController.playerPrefData.playerPrefData;
                if (string.IsNullOrEmpty(value))
                {
                    playerPrefData.RemoveKey(key);
                    LoggerManager.Debug($"{fucName}: 已删除全局变量: {key}");
                }
                else
                {
                    playerPrefData.SetKey(key, value);
                    LoggerManager.Debug($"{fucName}: 已设置全局变量: {key}={value}");
                }

                GameDataController instance = GameDataController._instance;
                if (instance != null)
                {
                    instance.SavePlayerprefData();
                }
                else
                {
                    LoggerManager.Warning($"{fucName}: GameDataController实例不存在，全局变量已写入内存但无法立即保存");
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Error($"{fucName}: 设置全局变量失败: {ex.Message}");
            }
        }
    }
}
