using Il2Cpp;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 弹出通用选择面板（角色/物品/武学），委托给 ChoosePanelHelper
    /// </summary>
    [SpePlotFuc("ShowChoosePanel")]
    public static class SpePlotFucShowChoosePanel
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            ChoosePanelHelper.ShowChoosePanel(plotController, fucName, fucParams);
        }
    }
}
