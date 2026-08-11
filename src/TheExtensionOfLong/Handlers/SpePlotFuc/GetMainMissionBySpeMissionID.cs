using Il2Cpp;
using Il2CppInterop.Runtime;

namespace TheExtensionOfLong
{
    /// <summary>
    /// 根据speMissionID获取主线任务并交给GameController处理
    /// 在 MainMissionDataBase 中查找 MissionData.speMissionID 匹配的任务，Clone 后调用 GetFullMission
    /// 与原版 GetMainMission(int) 按索引取值不同，此指令按 speMissionID 字段搜索
    /// 格式: GetMainMissionBySpeMissionID*speMissionID
    /// 示例: GetMainMissionBySpeMissionID*101
    /// </summary>
    [SpePlotFuc("GetMainMissionBySpeMissionID")]
    public static class SpePlotFucGetMainMissionBySpeMissionID
    {
        public static void TryCall(PlotController plotController, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1 || string.IsNullOrWhiteSpace(fucParams[0]))
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*speMissionID]");
                return;
            }

            string idStr = fucParams[0];
            if (!int.TryParse(idStr, out int speMissionID))
            {
                LoggerManager.Warning($"{fucName}: speMissionID格式错误 \"{idStr}\"，需为整数");
                return;
            }

            MissionDataController missionDataController = MissionDataController._instance;
            if (missionDataController == null)
            {
                LoggerManager.Error($"{fucName}: MissionDataController实例不存在");
                return;
            }

            var mainMissionDataBase = missionDataController.MainMissionDataBase;
            if (mainMissionDataBase == null)
            {
                LoggerManager.Error($"{fucName}: MainMissionDataBase为空");
                return;
            }

            MissionData found = null;
            for (int i = 0; i < mainMissionDataBase.Count; i++)
            {
                MissionData md = mainMissionDataBase[i];
                if (md != null && md.speMissionID == speMissionID)
                {
                    found = md;
                    break;
                }
            }

            if (found == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到speMissionID={speMissionID}的主线任务");
                return;
            }

            GameController gameController = GameController.Instance;
            if (gameController == null)
            {
                LoggerManager.Error($"{fucName}: GameController实例不存在");
                return;
            }

            MissionData cloned = found.Clone().Cast<MissionData>();
            gameController.GetFullMission(cloned);
            LoggerManager.Debug($"{fucName}: 已获取主线任务 speMissionID={speMissionID}, name={cloned.name}");
        }
    }
}
