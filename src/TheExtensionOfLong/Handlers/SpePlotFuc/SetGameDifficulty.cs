using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 修改游戏难度
    /// 格式: SetGameDifficulty*难度值
    ///   难度值: 0=简单, 1=普通, 2=困难, 3=极难, 4=自定义
    ///   也可直接传难度名称: 简单/普通/困难/极难/自定义
    /// 示例: SetGameDifficulty*1 或 SetGameDifficulty*困难
    /// </summary>
    [SpePlotFuc("SetGameDifficulty")]
    public static class SpePlotFucSetGameDifficulty
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*难度值(0-4)或难度名称(简单/普通/困难/极难/自定义)]");
                return;
            }

            string difficultyStr = fucParams[0];
            int difficultyValue;

            // 支持数字直接传入
            if (int.TryParse(difficultyStr, out difficultyValue))
            {
                if (difficultyValue < 0 || difficultyValue > 99)
                {
                    LoggerManager.Warning($"{fucName}: 难度值必须在合理范围内");
                    return;
                }
            }
            // 支持中文难度名称
            else
            {
                LoggerManager.Warning($"{fucName}: 难度值只支持使用数字格式！");
                return;
            }

            GameController instance = GameController.Instance;
            if (instance == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法修改难度");
                return;
            }

            WorldData worldData = instance.worldData;
            if (worldData == null)
            {
                LoggerManager.Error($"{fucName}: WorldData实例不存在，无法修改难度");
                return;
            }

            int oldDifficulty = worldData.gameDifficulty;
            worldData.gameDifficulty = difficultyValue;

            LoggerManager.Debug($"{fucName}: 游戏难度已从 {oldDifficulty} 变更为 {difficultyValue}");
        }
    }
}
