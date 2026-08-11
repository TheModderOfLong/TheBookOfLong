using System;
using Il2Cpp;

namespace TheExtensionOfLong
{
    public static class PlotCommandDebugConsole
    {
        private static string _lastCommand = string.Empty;

        public static void Show()
        {
            if (TextInputDialog.IsShowing) return;

            TextInputDialog.Show(
                "剧情指令调试台",
                _lastCommand,
                "例如: ChangePlotDataBase;0",
                Execute);
        }

        private static void Execute(string rawCommand)
        {
            string command = rawCommand == null ? string.Empty : rawCommand.Trim();
            if (command.Length == 0)
            {
                LoggerManager.Warning("PlotCommandDebugConsole: 输入为空，未执行");
                return;
            }

            _lastCommand = command;

            PlotController plotController = PlotController._instance;
            if (plotController == null || plotController.gameObject == null)
            {
                LoggerManager.Warning("PlotCommandDebugConsole: PlotController._instance 为空，无法执行剧情指令: " + command);
                return;
            }

            try
            {
                ExecuteClickCallFuc(plotController, command);
                LoggerManager.Info("PlotCommandDebugConsole: 已执行剧情指令: " + command);
            }
            catch (Exception ex)
            {
                LoggerManager.Error("PlotCommandDebugConsole: 执行剧情指令失败: " + command + "\n" + ex);
            }
        }

        private static void ExecuteClickCallFuc(PlotController plotController, string clickCallFuc)
        {
            PlotCommandHandler.ExecuteSendMessageCallbacks(plotController, clickCallFuc, true, "PlotCommandDebugConsole");
        }
    }
}
