using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 在鼠标位置显示提示文本
    /// 格式: ShowTextOnMouse*文本#播放音效(可选)
    ///   播放音效: 音效资源路径，如 "Sound/SoundEffect/WrongClick"，省略则不播放
    /// 示例: ShowTextOnMouse*情侣数量已达上限！
    ///       ShowTextOnMouse*操作失败#Sound/SoundEffect/WrongClick
    /// </summary>
    [SpePlotFuc("ShowTextOnMouse")]
    public static class SpePlotFucShowTextOnMouse
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*文本#播放音效(可选)]");
                return;
            }

            string text = fucParams[0];

            GameController gameController = GameController._instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法显示提示");
                return;
            }

            gameController.ShowTextOnMouse(text);
            LoggerManager.Debug($"{fucName}: 已显示提示: {text}");

            // 播放音效（可选）
            if (fucParams.Length > 1 && !string.IsNullOrWhiteSpace(fucParams[1]))
            {
                string soundPath = fucParams[1];
                try
                {
                    UnityEngine.AudioClip clip = UnityEngine.Resources.Load<UnityEngine.AudioClip>(soundPath);
                    if (clip != null)
                    {
                        NGUITools.PlaySound(clip);
                        LoggerManager.Debug($"{fucName}: 已播放音效: {soundPath}");
                    }
                    else
                    {
                        LoggerManager.Warning($"{fucName}: 未找到音效资源: {soundPath}");
                    }
                    //var clip = UnityEngine.Resources.Load(soundPath);
                    //if (clip != null)
                    //{
                    //    NGUITools.PlaySound(clip as UnityEngine.AudioClip);
                    //    LoggerManager.Debug($"{fucName}: 已播放音效: {soundPath}");
                    //}
                    //else
                    //{
                    //    LoggerManager.Warning($"{fucName}: 未找到音效资源: {soundPath}");
                    //}
                }
                catch (Exception e)
                {
                    LoggerManager.Error($"{fucName}: 播放音效失败: {e.Message}");
                }
            }
        }
    }
}
