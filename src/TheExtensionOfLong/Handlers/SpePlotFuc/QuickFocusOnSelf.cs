using Il2Cpp;
using UnityEngine;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 将大地图视角快速切到玩家当前位置。
    /// 格式: QuickFocusOnSelf
    /// </summary>
    [SpePlotFuc("QuickFocusOnSelf")]
    public static class SpePlotFucQuickFocusOnSelf
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            BigMapController bmc = BigMapController.Instance;
            if (bmc == null)
            {
                LoggerManager.Warning($"{fucName}: BigMapController实例不存在");
                return;
            }

            HeroData playerHero = CommonHandlers.GetPlayerHero();
            if (playerHero == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到玩家角色");
                return;
            }

            // 确保 playerArmy 有效：QuickFocusOnSelf 内部第一行就调用 PlayerStopMove，
            // 如果 playerArmy 引用已失效（GameObject 被销毁），方法中途静默失败，
            // 会导致 PlayerStopMove 已执行但 focus 未完成，玩家被锁定无法移动。
            // 因此先检查 playerArmy，无效则重新创建。
            if (bmc.playerArmy == null) // Il2Cpp 重载了 == 操作符，已销毁的 GameObject 也会判定为 null
            {
                LoggerManager.Debug($"{fucName}: playerArmy 为空，重新创建");
                bmc.CreateBigMapNpc(playerHero);
            }

            try
            {
                bmc.QuickFocusOnSelf();
                LoggerManager.Debug($"{fucName}: 已切换视角到玩家位置");
            }
            catch (System.Exception e)
            {
                LoggerManager.Error($"{fucName}: QuickFocusOnSelf 失败，尝试重建 playerArmy 后重试: {e.Message}");
                // playerArmy 可能引用了已销毁的 GameObject，重新创建后再试
                try
                {
                    bmc.CreateBigMapNpc(playerHero);
                    bmc.QuickFocusOnSelf();
                    LoggerManager.Debug($"{fucName}: 重建 playerArmy 后聚焦成功");
                }
                catch (System.Exception e2)
                {
                    LoggerManager.Error($"{fucName}: 重建后仍失败: {e2.Message}");
                }
            }
        }
    }
}
