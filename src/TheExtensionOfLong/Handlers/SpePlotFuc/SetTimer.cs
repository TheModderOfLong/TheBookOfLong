using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 设置存档内持久化定时器，可作为纯计时器使用，也可在到期后执行指定回调函数。
    /// 具体参数解析、保存和触发逻辑统一委托给 TimerManager。
    /// 格式: SetTimer*定时器ID#定时器类型#时间参数#回调函数名(可选)#函数参数(可选)#自定义参数(可选)#是否强制更新(可选)#是否立即触发(可选)
    /// </summary>
    [SpePlotFuc("SetTimer")]
    public static class SpePlotFucSetTimer
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            TimerManager.HandleSetTimer(plotController, fucName, fucParams);
        }
    }
}
