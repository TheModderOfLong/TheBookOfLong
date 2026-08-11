using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 检查情侣角色是否感情破裂，如果是则解除关系
    /// 格式: CheckPlayerMakeLoverUnhappy*角色ID
    ///   角色ID: 角色的数字ID/名称/关键字(player等)
    /// 示例: CheckPlayerMakeLoverUnhappy*小白   → 检查小白是否为情侣且是否感情破裂，如果是则解除关系
    /// </summary>
    [SpePlotFuc("CheckPlayerMakeLoverUnhappy")]
    public static class SpePlotFucCheckPlayerMakeLoverUnhappy
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*角色ID]");
                return;
            }

            HeroData hero = CommonHandlers.ResolveHeroId(plotController, fucParams[0]);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 \"{fucParams[0]}\"");
                return;
            }

            hero.CheckPlayerMakeLoverUnhappy();
            LoggerManager.Debug($"{fucName}: 已执行 {hero.heroName} 的情侣感情破裂检查");
        }
    }
}
