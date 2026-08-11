using Il2Cpp;
using System;

namespace TheExtensionOfLong
{
    public enum RandomHeroNameReturnType
    {
        Full = 0,
        Family = 1,
        Given = 2
    }

    /// <summary>
    /// 生成随机角色姓名。
    /// 格式: [$GenerateRandomHeroName:性别限制(可选):返回类型(可选):姓氏(可选)$]
    /// </summary>
    [ConditionQuery("GenerateRandomHeroName", Cacheable = false)]
    public static class QueryGenerateRandomHeroName
    {
        public static string TryQuery(PlotController plotController, string[] parts)
        {
            GameDataController gameData = GameDataController._instance;
            if (gameData == null)
            {
                LoggerManager.Warning("  GenerateRandomHeroName查询: GameDataController实例不存在");
                return "";
            }

            SexLimit sexLimit = parts.Length > 1
                ? CommonHandlers.ParseSexLimit(parts[1], SexLimit.None)
                : SexLimit.None;

            RandomHeroNameReturnType returnType = RandomHeroNameReturnType.Full;
            if (parts.Length > 2 && !string.IsNullOrWhiteSpace(parts[2]))
            {
                if (!TryParseReturnType(parts[2], out returnType))
                {
                    LoggerManager.Warning($"  GenerateRandomHeroName查询: 返回类型解析失败 {parts[2]}，仅支持 0/1/2 或 Full/Family/Given");
                    return "";
                }
            }

            string familyName = parts.Length > 3 ? (parts[3] ?? "").Trim() : "";
            if (string.IsNullOrWhiteSpace(familyName))
                familyName = gameData.GenerateRandomHeroFamilyName();

            bool isFemale = ResolveIsFemale(sexLimit);

            switch (returnType)
            {
                case RandomHeroNameReturnType.Family:
                    return familyName ?? "";
                case RandomHeroNameReturnType.Given:
                    return gameData.GenerateRandomHeroGivenName(isFemale, false) ?? "";
                case RandomHeroNameReturnType.Full:
                default:
                    return GenerateUniqueFullName(gameData, familyName, isFemale);
            }
        }

        private static bool ResolveIsFemale(SexLimit sexLimit)
        {
            if (sexLimit == SexLimit.Female)
                return true;
            if (sexLimit == SexLimit.Male)
                return false;

            return UnityEngine.Random.Range(0, 2) == 1;
        }

        private static string GenerateUniqueFullName(GameDataController gameData, string familyName, bool isFemale)
        {
            WorldData world = CommonHandlers.GetWorldData();
            string result = "";

            for (int i = 0; i < 20; i++)
            {
                result = gameData.GenerateRandomHeroName(isFemale, familyName, false) ?? "";
                if (string.IsNullOrWhiteSpace(result))
                    return "";

                if (world == null || world.GetHero(result) == null)
                    return result;
            }

            LoggerManager.Warning($"  GenerateRandomHeroName查询: 随机姓名重试20次后仍可能重复，将返回最后一次结果 {result}");
            return result;
        }

        private static bool TryParseReturnType(string raw, out RandomHeroNameReturnType returnType)
        {
            returnType = RandomHeroNameReturnType.Full;
            string value = (raw ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (int.TryParse(value, out int intValue))
            {
                if (Enum.IsDefined(typeof(RandomHeroNameReturnType), intValue))
                {
                    returnType = (RandomHeroNameReturnType)intValue;
                    return true;
                }

                return false;
            }

            return Enum.TryParse(value, true, out returnType);
        }
    }
}
