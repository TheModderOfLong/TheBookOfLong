using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 发送屏幕提示消息（InfoTab）
    /// 格式: AddInfoTab*提示文本#图标名(可选)#音效名(可选)
    /// 提示文本支持富文本颜色标签，如 "&lt;color=#FFD700&gt;金色文本&lt;/color&gt;"
    /// 图标名对应 UIAtlas 中的精灵名，为空则不显示图标
    /// </summary>
    [SpePlotFuc("AddInfoTab")]
    public static class SpePlotFucAddInfoTab
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*提示文本#图标名(可选)#音效名(可选)]");
                return;
            }

            string infoText = fucParams[0];
            if (string.IsNullOrWhiteSpace(infoText))
            {
                LoggerManager.Warning($"{fucName}: 提示文本不能为空");
                return;
            }

            InfoController infoController = InfoController._instance;
            if (infoController == null)
            {
                LoggerManager.Error($"{fucName}: InfoController实例不存在，无法发送提示");
                return;
            }

            string infoPic = fucParams.Length > 1 && !string.IsNullOrWhiteSpace(fucParams[1]) ? fucParams[1] : null;
            string soundName = fucParams.Length > 2 && !string.IsNullOrWhiteSpace(fucParams[2]) ? fucParams[2] : "Woosh";

            try
            {
                infoController.AddInfoTab(infoText, "UIAtlas", infoPic, soundName);
                LoggerManager.Debug($"{fucName}: 已发送提示: {infoText}");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"{fucName}: AddInfoTab调用失败: {e.Message}");
            }
        }
    }
}
