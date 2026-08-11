using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("FinishMission")]
    public static class SpePlotFucFinishMission
    {
        /// <summary>
        /// 完成指定任务。
        /// 格式: FinishMission*任务来源
        ///   任务来源: 空/nowMission, forceMission, speMissionID-1001, speMissionID=1001, speMissionID:1001, 任务id, 任务名
        /// </summary>
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*任务来源]");
                return;
            }

            string missionSource = fucParams[0];
            MissionData mission = CommonHandlers.ResolveMissionSource(plotController, missionSource);
            if (mission == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到任务 \"{(string.IsNullOrEmpty(missionSource) ? "nowMission" : missionSource)}\"");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在，无法完成任务");
                return;
            }

            try
            {
                LoggerManager.Debug($"{fucName}: 准备完成任务 {mission.name}(id={mission.id}, speMissionID={mission.speMissionID})");
                gameController.FinishMission(mission);
                LoggerManager.Debug($"{fucName}: 已调用 FinishMission 完成任务 {mission.name}");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"{fucName}: 完成任务 {mission.name} 失败: {e.Message}");
            }
        }
    }
}
