using Il2Cpp;

namespace TheExtensionOfLong
{
    public static class HeroInActiveManager
    {
        private const string HeroDataObjectType = "HeroData";
        private const string InActivePropertyName = "inActive";

        public static string GetHeroDataPropertyKey(HeroData hero, string propertyName)
        {
            if (hero == null || string.IsNullOrWhiteSpace(propertyName))
                return null;

            return GetHeroDataPropertyKey(hero.heroID, propertyName);
        }

        public static string GetHeroDataPropertyKey(int heroID, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return null;

            return CustomValueManager.GetKey(HeroDataObjectType, heroID.ToString(), propertyName);
        }

        public static string GetKey(HeroData hero)
        {
            return GetHeroDataPropertyKey(hero, InActivePropertyName);
        }

        public static bool IsInActive(HeroData hero)
        {
            string key = GetKey(hero);
            if (string.IsNullOrEmpty(key))
                return false;

            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null || !logData.HaveKey(key))
                return false;

            string value = logData.Get(key);
            return CommonHandlers.TryParseStrictBool(value, out bool inActive) && inActive;
        }

        public static bool SetInActive(HeroData hero, bool inActive)
        {
            string key = GetKey(hero);
            if (string.IsNullOrEmpty(key))
                return false;

            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            if (logData == null)
                return false;

            logData.Set(key, inActive ? "1" : null);
            return true;
        }

        public static bool TryParseState(string raw, out bool inActive)
        {
            return CommonHandlers.TryParseStrictBool(raw, out inActive);
        }

        public static bool IsAllowedAutoAIData(HeroAIData aiData)
        {
            if (aiData == null)
                return true;

            if (aiData.aiStuffType == AIStuffType.MoveOnBigMap)
                return false;

            if (aiData.bigMapTargetID >= 0)
                return false;

            return IsAllowedAutoAIStuff(aiData.aiStuffType);
        }

        public static bool IsAllowedAutoAIStuff(AIStuffType aiStuffType)
        {
            switch (aiStuffType)
            {
                case AIStuffType.None:
                case AIStuffType.Free:
                case AIStuffType.Rest:
                case AIStuffType.CureSelf:
                case AIStuffType.StudyLivingSkill:
                case AIStuffType.MakeMoney:
                case AIStuffType.CraftFood:
                case AIStuffType.CraftMed:
                case AIStuffType.CraftEquip:
                case AIStuffType.ReduceBadFame:
                case AIStuffType.AddAreaState:
                    return true;
                default:
                    return false;
            }
        }

        public static HeroAIData CreateSafeAIData(HeroData hero)
        {
            try
            {
                if (hero != null && hero.GetTotalInjury() > 50f)
                    return new HeroAIData(AIStuffType.CureSelf, 99);

                if (hero != null && (hero.GetHpPercent() < 0.8f || hero.GetManaPercent() < 0.8f))
                    return new HeroAIData(AIStuffType.Rest, 1);
            }
            catch
            {
            }

            return new HeroAIData(AIStuffType.Free, 1);
        }
    }
}
