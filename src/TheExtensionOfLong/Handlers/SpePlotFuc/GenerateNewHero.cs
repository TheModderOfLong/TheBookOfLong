using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace TheExtensionOfLong
{
    [SpePlotFuc("GenerateNewHero")]
    public static class SpePlotFucGenerateNewHero
    {
        private struct GenerateNewHeroParams
        {
            public string heroName;
            public bool hasHeroName;
            public string heroFamilyName;
            public bool hasHeroFamilyName;
            public int belongForceID;
            public bool hasBelongForceID;
            public float heroForceLv;
            public bool hasHeroForceLv;
            public bool isTempHero;
            public SexLimit sexLimit;
            public bool isRandomEnemy;
            public int nature;
            public bool hasNature;
            public int age;
            public bool hasAge;
            public int talent;
            public bool hasTalent;
            public float chaos;
            public bool hasChaos;
            public float evil;
            public bool hasEvil;
            public int enterAreaID;
            public bool hasEnterAreaID;
            public bool hide;
            public bool recruitAble;
            public bool hasRecruitAble;
            public bool loveAble;
            public bool hasLoveAble;
            public string heroNickName;
            public bool hasHeroNickName;
            public List<int> hobby;
            public bool hasHobby;
            public float fame;
            public bool hasFame;
            public float loyal;
            public bool hasLoyal;
            public int skillForceID;
            public bool hasSkillForceID;
            public float heroStrengthLv;
            public bool hasHeroStrengthLv;
            public List<int> kungfuFocus;
            public bool hasKungfuFocus;
            public List<int> livingFocus;
            public bool hasLivingFocus;
            public int uniqueSkillID;
            public bool hasUniqueSkill;
            public List<int> tagIDs;
            public bool hasTags;
            public string teacherRef;
            public bool hasTeacher;
            public string relationsRaw;
            public bool hasRelations;
            public int skinID;
            public bool hasSkinID;
            public int skinLv;
            public bool hasSkinLv;
            public bool speSkeleton;
            public bool hasSpeSkeleton;
            public bool inActive;
            public bool hasInActive;

            public static GenerateNewHeroParams Default => new GenerateNewHeroParams
            {
                heroName = null,
                hasHeroName = false,
                heroFamilyName = null,
                hasHeroFamilyName = false,
                belongForceID = -1,
                hasBelongForceID = false,
                heroForceLv = 0f,
                hasHeroForceLv = false,
                isTempHero = false,
                sexLimit = SexLimit.None,
                isRandomEnemy = false,
                nature = 0,
                hasNature = false,
                age = 0,
                hasAge = false,
                talent = 0,
                hasTalent = false,
                chaos = 0f,
                hasChaos = false,
                evil = 0f,
                hasEvil = false,
                enterAreaID = -1,
                hasEnterAreaID = false,
                hide = false,
                recruitAble = false,
                hasRecruitAble = false,
                loveAble = false,
                hasLoveAble = false,
                heroNickName = null,
                hasHeroNickName = false,
                hobby = null,
                hasHobby = false,
                fame = 0f,
                hasFame = false,
                loyal = 0f,
                hasLoyal = false,
                skillForceID = -1,
                hasSkillForceID = false,
                heroStrengthLv = 0f,
                hasHeroStrengthLv = false,
                kungfuFocus = null,
                hasKungfuFocus = false,
                livingFocus = null,
                hasLivingFocus = false,
                uniqueSkillID = -1,
                hasUniqueSkill = false,
                tagIDs = null,
                hasTags = false,
                teacherRef = null,
                hasTeacher = false,
                relationsRaw = null,
                hasRelations = false,
                skinID = 0,
                hasSkinID = false,
                skinLv = 0,
                hasSkinLv = false,
                speSkeleton = false,
                hasSpeSkeleton = false,
                inActive = false,
                hasInActive = false,
            };
        }

        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            string contextTarget = null;
            int paramStartIdx = 0;

            if (fucParams.Length > 0 && !string.IsNullOrWhiteSpace(fucParams[0]))
            {
                string firstParam = fucParams[0].Trim();
                if (!firstParam.Contains("="))
                {
                    if (!firstParam.Equals("null", System.StringComparison.OrdinalIgnoreCase))
                        contextTarget = firstParam;
                    paramStartIdx = 1;
                }
            }

            GenerateNewHeroParams p = ParseParams(fucParams, paramStartIdx, fucName);

            GameController gc = GameController.Instance;
            if (gc == null)
            {
                LoggerManager.Error($"{fucName}: GameController 实例为空");
                return;
            }

            WorldData world = gc.worldData;
            if (world == null)
            {
                LoggerManager.Error($"{fucName}: WorldData 为空");
                return;
            }

            if (!p.hasBelongForceID)
                p.belongForceID = PickRandomForceID(world);
            else if (p.belongForceID >= 0 && world.GetForce(p.belongForceID) == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到 belongForceID={p.belongForceID}，改用 -1");
                p.belongForceID = -1;
            }

            if (!p.hasHeroForceLv)
                p.heroForceLv = UnityEngine.Random.Range(0, 5);
            p.heroForceLv = Mathf.Clamp(p.heroForceLv, -1f, 5f);

            AreaData explicitEnterArea = null;
            if (p.hasEnterAreaID)
            {
                explicitEnterArea = world.GetArea(p.enterAreaID);
                if (!p.isTempHero && explicitEnterArea == null)
                {
                    LoggerManager.Warning($"{fucName}: 未找到 enterAreaID={p.enterAreaID}，中止生成");
                    return;
                }
                if (p.isTempHero)
                {
                    LoggerManager.Warning($"{fucName}: isTempHero=true 时会忽略 enterAreaID={p.enterAreaID}");
                }
            }

            HeroData hero = GenerateHero(gc, world, p, fucName);
            if (hero == null)
            {
                LoggerManager.Warning($"{fucName}: 角色生成失败");
                return;
            }

            if (p.isTempHero)
            {
                world.AddTempHero(hero);
            }
            else
            {
                world.AddNewHero(hero);
                ApplyForceRegistration(world, hero, p, fucName);
                ApplyEnterArea(gc, world, hero, p, explicitEnterArea, fucName);
            }

            ApplyOverrideParams(hero, p);
            ApplyPostRegisterParams(__instance, hero, p, fucName);
            ApplyInActiveState(hero, p, fucName);
            gc.CountHeroData(hero);
            AssignGeneratedHeroToContext(__instance, contextTarget, hero, fucName);

            LoggerManager.Debug($"{fucName}: 已生成角色 {hero.heroName}(ID={hero.heroID})，isTempHero={p.isTempHero}，context={contextTarget ?? "null"}");
        }

        private static GenerateNewHeroParams ParseParams(string[] fucParams, int startIdx, string fucName)
        {
            GenerateNewHeroParams p = GenerateNewHeroParams.Default;

            for (int i = startIdx; i < fucParams.Length; i++)
            {
                string param = fucParams[i];
                if (string.IsNullOrWhiteSpace(param)) continue;

                int eqIdx = param.IndexOf('=');
                if (eqIdx < 0)
                {
                    LoggerManager.Warning($"{fucName}: 已忽略未命名参数 \"{param}\"");
                    continue;
                }

                string key = param.Substring(0, eqIdx).Trim().ToLowerInvariant();
                string val = param.Substring(eqIdx + 1).Trim();

                switch (key)
                {
                    case "heroname":
                        p.heroName = string.IsNullOrWhiteSpace(val) ? null : val;
                        p.hasHeroName = !string.IsNullOrWhiteSpace(val);
                        NormalizeHeroName(ref p);
                        break;
                    case "herofamilyname":
                    case "familyname":
                    case "surname":
                        p.heroFamilyName = string.IsNullOrWhiteSpace(val) ? null : val;
                        p.hasHeroFamilyName = !string.IsNullOrWhiteSpace(val);
                        NormalizeHeroName(ref p);
                        break;
                    case "belongforceid":
                    case "belongforce":
                    case "force":
                        p.belongForceID = CommonHandlers.ResolveToForceID(val, -1);
                        p.hasBelongForceID = true;
                        break;
                    case "heroforcelv":
                        p.heroForceLv = CommonHandlers.ParseHeroForceLv(val, p.heroForceLv);
                        p.hasHeroForceLv = true;
                        break;
                    case "istemphero":
                        p.isTempHero = CommonHandlers.ParseBool(val, false);
                        break;
                    case "sexlimit":
                        p.sexLimit = CommonHandlers.ParseSexLimit(val, SexLimit.None);
                        break;
                    case "israndomenemy":
                        p.isRandomEnemy = CommonHandlers.ParseBool(val, false);
                        break;
                    case "nature":
                        p.nature = CommonHandlers.ParseNature(val, p.nature);
                        p.hasNature = true;
                        break;
                    case "age":
                        if (int.TryParse(val, out p.age))
                            p.hasAge = true;
                        else
                            LoggerManager.Warning($"{fucName}: age 解析失败：{val}");
                        break;
                    case "talent":
                        p.talent = CommonHandlers.ParseTalent(val, p.talent);
                        p.hasTalent = true;
                        break;
                    case "chaos":
                        p.chaos = CommonHandlers.ParseChaos(val, p.chaos);
                        p.hasChaos = true;
                        break;
                    case "evil":
                        p.evil = CommonHandlers.ParseEvil(val, p.evil);
                        p.hasEvil = true;
                        break;
                    case "enterareaid":
                        if (int.TryParse(val, out p.enterAreaID))
                            p.hasEnterAreaID = true;
                        else
                            LoggerManager.Warning($"{fucName}: enterAreaID 解析失败：{val}");
                        break;
                    case "hide":
                        p.hide = CommonHandlers.ParseBool(val, false);
                        break;
                    case "recruitable":
                        p.recruitAble = CommonHandlers.ParseBool(val, false);
                        p.hasRecruitAble = true;
                        break;
                    case "loveable":
                        p.loveAble = CommonHandlers.ParseBool(val, false);
                        p.hasLoveAble = true;
                        break;
                    case "heronickname":
                        p.heroNickName = val;
                        p.hasHeroNickName = true;
                        break;
                    case "hobby":
                        p.hobby = ParseHobbyList(val, fucName);
                        p.hasHobby = true;
                        break;
                    case "fame":
                        if (float.TryParse(val, out p.fame))
                            p.hasFame = true;
                        else
                            LoggerManager.Warning($"{fucName}: fame 解析失败：{val}");
                        break;
                    case "loyal":
                        if (float.TryParse(val, out p.loyal))
                            p.hasLoyal = true;
                        else
                            LoggerManager.Warning($"{fucName}: loyal 解析失败：{val}");
                        break;
                    case "skillforceid":
                    case "skillforce":
                        p.skillForceID = CommonHandlers.ResolveToForceID(val, -1);
                        p.hasSkillForceID = true;
                        break;
                    case "herostrengthlv":
                    case "strengthlv":
                        if (float.TryParse(val, out p.heroStrengthLv))
                            p.hasHeroStrengthLv = true;
                        else
                            LoggerManager.Warning($"{fucName}: heroStrengthLv 解析失败：{val}");
                        break;
                    case "kungfufocus":
                        p.kungfuFocus = CommonHandlers.ResolveFocusList(val, FocusListKind.Kungfu);
                        p.hasKungfuFocus = true;
                        break;
                    case "livingfocus":
                        p.livingFocus = CommonHandlers.ResolveFocusList(val, FocusListKind.Living);
                        p.hasLivingFocus = true;
                        break;
                    case "uniqueskill":
                        p.uniqueSkillID = CommonHandlers.ResolveSkillID(val, -1);
                        p.hasUniqueSkill = p.uniqueSkillID >= 0;
                        break;
                    case "tags":
                        p.tagIDs = CommonHandlers.ResolveTagList(val);
                        p.hasTags = true;
                        break;
                    case "teacher":
                        p.teacherRef = val;
                        p.hasTeacher = !string.IsNullOrWhiteSpace(val);
                        break;
                    case "relations":
                        p.relationsRaw = val;
                        p.hasRelations = !string.IsNullOrWhiteSpace(val);
                        break;
                    case "skinid":
                        if (int.TryParse(val, out p.skinID))
                            p.hasSkinID = true;
                        else
                            LoggerManager.Warning($"{fucName}: skinID 解析失败：{val}");
                        break;
                    case "skinlv":
                        if (int.TryParse(val, out p.skinLv))
                            p.hasSkinLv = true;
                        else
                            LoggerManager.Warning($"{fucName}: skinLv 解析失败：{val}");
                        break;
                    case "speskeleton":
                        p.speSkeleton = CommonHandlers.ParseBool(val, false);
                        p.hasSpeSkeleton = true;
                        break;
                    case "inactive":
                        if (HeroInActiveManager.TryParseState(val, out p.inActive))
                            p.hasInActive = true;
                        else
                            LoggerManager.Warning($"{fucName}: inActive 解析失败：{val}，仅支持 0/1/true/false");
                        break;
                    default:
                        LoggerManager.Warning($"{fucName}: 未知命名参数 \"{key}\"，已忽略");
                        break;
                }
            }

            return p;
        }

        private static HeroData GenerateHero(GameController gc, WorldData world, GenerateNewHeroParams p, string fucName)
        {
            if (p.hasHeroName)
            {
                HeroData exists = world.GetHero(p.heroName);
                if (exists != null)
                    LoggerManager.Warning($"{fucName}: heroName \"{p.heroName}\" 已存在");

                HeroData namedHero = gc.GenerateHeroData(p.heroName, -1, p.belongForceID, p.heroForceLv, null, true, p.sexLimit, p.isRandomEnemy, false);
                if (namedHero != null && p.hasHeroFamilyName)
                    namedHero.heroFamilyName = p.heroFamilyName;

                return namedHero;
            }

            if (p.hasHeroFamilyName)
            {
                HeroData heroByFamilyName = gc.GenerateHeroData(null, -1, p.belongForceID, p.heroForceLv, null, true, p.sexLimit, p.isRandomEnemy, false);
                if (heroByFamilyName == null)
                    return null;

                heroByFamilyName.heroFamilyName = p.heroFamilyName;
                heroByFamilyName.heroName = GameDataController._instance.GenerateRandomHeroName(heroByFamilyName.isFemale, p.heroFamilyName, false);

                for (int i = 0; i < 20 && !string.IsNullOrWhiteSpace(heroByFamilyName.heroName) && world.GetHero(heroByFamilyName.heroName) != null; i++)
                {
                    heroByFamilyName.heroName = GameDataController._instance.GenerateRandomHeroName(heroByFamilyName.isFemale, p.heroFamilyName, false);
                }

                if (!string.IsNullOrWhiteSpace(heroByFamilyName.heroName) && world.GetHero(heroByFamilyName.heroName) != null)
                    LoggerManager.Warning($"{fucName}: 指定 heroFamilyName={p.heroFamilyName} 后随机 heroName 重试 20 次仍然重复，将使用最后一次生成的名称");

                return heroByFamilyName;
            }

            HeroData hero = null;
            for (int i = 0; i < 20; i++)
            {
                hero = gc.GenerateHeroData(null, -1, p.belongForceID, p.heroForceLv, null, true, p.sexLimit, p.isRandomEnemy, false);
                if (hero == null || string.IsNullOrWhiteSpace(hero.heroName))
                    return hero;

                if (world.GetHero(hero.heroName) == null)
                    return hero;
            }

            LoggerManager.Warning($"{fucName}: 随机 heroName 重试 20 次后仍然重复，将使用最后一次生成的名称");
            return hero;
        }

        private static void ApplyOverrideParams(HeroData hero, GenerateNewHeroParams p)
        {
            hero.hide = p.hide;
            if (p.hasHeroFamilyName) hero.heroFamilyName = p.heroFamilyName;
            if (p.hasNature) hero.nature = p.nature;
            if (p.hasAge) hero.age = p.age;
            if (p.hasTalent) hero.talent = p.talent;
            if (p.hasChaos) hero.chaos = Mathf.Clamp(p.chaos, 0f, 100f);
            if (p.hasEvil) hero.evil = Mathf.Clamp(p.evil, 0f, 100f);
            if (p.hasRecruitAble) hero.recruitAble = p.recruitAble;
            if (p.hasLoveAble) hero.loveAble = p.loveAble;
            if (p.hasHeroNickName) hero.heroNickName = p.heroNickName;
            if (p.hasHobby) hero.hobby = p.hobby;
            if (p.hasFame) hero.fame = p.fame;
            if (p.hasLoyal) hero.loyal = p.loyal;
            if (p.hasSkillForceID) hero.skillForceID = p.skillForceID;
            if (p.hasHeroStrengthLv) hero.heroStrengthLv = p.heroStrengthLv;
            if (p.hasKungfuFocus) hero.kungfuSkillFocus = p.kungfuFocus;
            if (p.hasLivingFocus) hero.livingSkillFocus = p.livingFocus;
            if (p.hasSpeSkeleton) hero.speHero = p.speSkeleton;
            if (p.hasSkinID) hero.SetSkin(p.skinID, p.hasSkinLv ? p.skinLv : 0);
            if (p.hasTags) ApplyTags(hero, p.tagIDs);
        }

        private static void ApplyPostRegisterParams(PlotController pc, HeroData hero, GenerateNewHeroParams p, string fucName)
        {
            if (p.hasUniqueSkill)
                ApplyUniqueSkill(hero, p.uniqueSkillID, fucName);

            if (p.isTempHero)
            {
                if (p.hasTeacher || p.hasRelations)
                    LoggerManager.Warning($"{fucName}: 临时角色不支持 teacher/relations，已忽略 {hero.heroName}(ID={hero.heroID}) 的相关设置");
                return;
            }

            if (p.hasTeacher)
                ApplyTeacher(pc, hero, p.teacherRef, fucName);

            if (p.hasRelations)
                ApplyRelations(pc, hero, p.relationsRaw, fucName);
        }

        private static void ApplyInActiveState(HeroData hero, GenerateNewHeroParams p, string fucName)
        {
            if (!p.hasInActive)
                return;

            if (!HeroInActiveManager.SetInActive(hero, p.inActive))
            {
                LoggerManager.Warning($"{fucName}: 设置 {hero?.heroName}(ID={hero?.heroID}) inActive 失败");
                return;
            }

            LoggerManager.Debug($"{fucName}: 已设置 {hero.heroName}(ID={hero.heroID}) inActive={(p.inActive ? "1" : "0")}");
        }

        private static void ApplyForceRegistration(WorldData world, HeroData hero, GenerateNewHeroParams p, string fucName)
        {
            if (world == null || hero == null || hero.belongForceID < 0)
                return;

            int forceID = hero.belongForceID;
            ForceData force = world.GetForce(forceID);
            if (force == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到 forceID={forceID}，跳过 JoinForce");
                return;
            }

            int forceLv = Mathf.Clamp(hero.heroForceLv, -1, 5);
            hero.JoinForce(forceID, forceLv, -1, false, true);

            force.forceDetailDirty = true;
            force.forceHeroDetailDirty = true;

            LoggerManager.Debug($"{fucName}: 已将角色 {hero.heroName}(ID={hero.heroID}) 登记到门派 forceID={forceID}，forceLv={forceLv}");
        }

        private static void ApplyTags(HeroData hero, List<int> tagIDs)
        {
            if (hero == null || tagIDs == null)
                return;

            for (int i = 0; i < tagIDs.Count; i++)
            {
                int tagID = tagIDs[i];
                if (tagID >= 0)
                    hero.AddTag(tagID, -1f, null, false, false);
            }
        }

        private static void ApplyUniqueSkill(HeroData hero, int skillID, string fucName)
        {
            if (hero == null || skillID < 0)
                return;

            KungfuSkillLvData skill = null;
            if (hero.kungfuSkills == null)
                hero.kungfuSkills = new List<KungfuSkillLvData>();

            for (int i = 0; i < hero.kungfuSkills.Count; i++)
            {
                KungfuSkillLvData item = hero.kungfuSkills[i];
                if (item != null && item.skillID == skillID)
                {
                    skill = item;
                    break;
                }
            }

            if (skill == null)
            {
                skill = new KungfuSkillLvData(skillID);
                hero.kungfuSkills.Add(skill);
            }

            skill.belongHeroID = hero.heroID;
            hero.uniqueSkill = skill;
            LoggerManager.Debug($"{fucName}: 已为角色 {hero.heroName}(ID={hero.heroID}) 设置 uniqueSkill={skillID}");
        }

        private static void ApplyTeacher(PlotController pc, HeroData hero, string teacherRef, string fucName)
        {
            HeroData teacher = CommonHandlers.ResolveHeroId(pc, teacherRef);
            if (teacher == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到 teacher：{teacherRef}");
                return;
            }

            teacher.AddStudent(hero.heroID, false);
        }

        private static void ApplyRelations(PlotController pc, HeroData hero, string relationsRaw, string fucName)
        {
            string[] entries = SplitRelationEntries(relationsRaw);
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                int split = entry.IndexOf(':');
                if (split < 0)
                {
                    LoggerManager.Warning($"{fucName}: relations 条目解析失败：{entry}");
                    continue;
                }

                string relationType = entry.Substring(0, split).Trim();
                string targetRefsRaw = entry.Substring(split + 1).Trim();
                string[] targetRefs = SplitRelationTargets(targetRefsRaw);
                if (targetRefs.Length == 0)
                {
                    LoggerManager.Warning($"{fucName}: relations 目标为空：{entry}");
                    continue;
                }

                for (int j = 0; j < targetRefs.Length; j++)
                {
                    string targetRef = targetRefs[j].Trim();
                    if (string.IsNullOrWhiteSpace(targetRef))
                        continue;

                    HeroData target = CommonHandlers.ResolveHeroId(pc, targetRef);
                    if (target == null)
                    {
                        LoggerManager.Warning($"{fucName}: 未找到 relations 目标：{targetRef}");
                        continue;
                    }

                    ApplyRelation(hero, target, relationType, fucName);
                }
            }
        }

        private static void ApplyRelation(HeroData hero, HeroData target, string relationType, string fucName)
        {
            string lower = relationType.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "friend":
                case "朋友":
                    hero.AddFriend(target.heroID, false);
                    break;
                case "hater":
                case "仇人":
                case "仇敌":
                    hero.AddHater(target.heroID, false);
                    break;
                case "brother":
                case "结义":
                    hero.AddBrother(target.heroID, false);
                    break;
                case "lover":
                case "夫妻":
                    hero.SetLover(target.heroID, false);
                    break;
                case "prelover":
                case "情侣":
                    hero.AddPrelover(target.heroID, false);
                    break;
                case "teacher":
                case "师父":
                    target.AddStudent(hero.heroID, false);
                    break;
                case "relative":
                case "亲属":
                    AddRelationID(hero.Relatives, target.heroID);
                    AddRelationID(target.Relatives, hero.heroID);
                    break;
                default:
                    LoggerManager.Warning($"{fucName}: 不支持的 relations 类型：{relationType}");
                    break;
            }
        }

        private static void AddRelationID(List<int> list, int heroID)
        {
            if (list != null && heroID >= 0 && !list.Contains(heroID))
                list.Add(heroID);
        }

        private static string[] SplitRelationEntries(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new string[0];

            return raw.Split(new char[] { ';', '-' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        private static string[] SplitRelationTargets(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new string[0];

            return raw.Split(new char[] { '/' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        private static void ApplyEnterArea(GameController gc, WorldData world, HeroData hero, GenerateNewHeroParams p, AreaData explicitEnterArea, string fucName)
        {
            AreaData area = explicitEnterArea;

            if (area == null && hero.belongForceID >= 0)
            {
                ForceData force = world.GetForce(hero.belongForceID);
                if (force != null && force.mainAreaID >= 0)
                    area = world.GetArea(force.mainAreaID);
            }

            if (area == null)
                area = PickRandomArea(world);

            if (area == null)
            {
                LoggerManager.Warning($"{fucName}: 未找到角色 {hero.heroName}(ID={hero.heroID}) 的有效进入区域");
                return;
            }

            gc.HeroEnterArea(hero, area);
        }

        private static int PickRandomForceID(WorldData world)
        {
            if (world?.Forces == null || world.Forces.Count == 0)
                return -1;

            int count = 0;
            for (int i = 0; i < world.Forces.Count; i++)
            {
                ForceData force = world.Forces[i];
                if (force != null && force.forceID >= 0)
                    count++;
            }

            if (count <= 0)
                return -1;

            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < world.Forces.Count; i++)
            {
                ForceData force = world.Forces[i];
                if (force == null || force.forceID < 0)
                    continue;

                if (pick == 0)
                    return force.forceID;
                pick--;
            }

            return -1;
        }

        private static AreaData PickRandomArea(WorldData world)
        {
            int cityCount = world?.cityAreaID != null ? world.cityAreaID.Count : 0;
            int villageCount = world?.villageAreaID != null ? world.villageAreaID.Count : 0;
            int total = cityCount + villageCount;
            if (total <= 0)
                return null;

            int pick = UnityEngine.Random.Range(0, total);
            int areaID = pick < cityCount
                ? world.cityAreaID[pick]
                : world.villageAreaID[pick - cityCount];

            return world.GetArea(areaID);
        }

        private static void AssignGeneratedHeroToContext(PlotController pc, string contextTarget, HeroData hero, string fucName)
        {
            if (pc == null || hero == null || string.IsNullOrWhiteSpace(contextTarget))
                return;

            string target = contextTarget.Trim();
            string lower = target.ToLowerInvariant();

            if (lower == "sourceinteracthero")
            {
                pc.sourceInteractHero = hero;
                return;
            }

            if (lower == "targetinteracthero")
            {
                pc.targetInteractHero = hero;
                return;
            }

            if (lower == "choosehero" || lower == "chosenhero")
            {
                ChooseController chooseController = ChooseController._instance;
                HeroIconController icon = chooseController?.chooseResult?.GetComponent<HeroIconController>();
                if (icon == null)
                    LoggerManager.Warning($"{fucName}: chooseHero 上下文不可用");
                else
                    icon.heroData = hero;
                return;
            }

            if (lower.StartsWith("tempplothero"))
            {
                if (pc.tempPlotHero == null)
                    pc.tempPlotHero = new List<HeroData>();
                AssignToHeroList(pc.tempPlotHero, target, hero, fucName, "TempPlotHero");
                return;
            }

            if (lower.StartsWith("plotinteracthero") || lower.StartsWith("plotinteractherolist"))
            {
                if (pc.plotInteractHeroList == null)
                    pc.plotInteractHeroList = new List<HeroData>();
                AssignToHeroList(pc.plotInteractHeroList, target, hero, fucName, "PlotInteractHero");
                return;
            }

            if (lower == "missioneventtargethero" || lower == "missioneventsourcehero")
            {
                LoggerManager.Warning($"{fucName}: {contextTarget} 不支持作为写入上下文");
                return;
            }

            LoggerManager.Warning($"{fucName}: 未知上下文对象 \"{contextTarget}\"");
        }

        private static void AssignToHeroList(List<HeroData> list, string target, HeroData hero, string fucName, string label)
        {
            if (!TryParseIndex(target, out int index))
            {
                list.Add(hero);
                return;
            }

            if (index < 0)
            {
                LoggerManager.Warning($"{fucName}: {label} 索引 {index} 无效");
                return;
            }

            if (index < list.Count)
                list[index] = hero;
            else
            {
                list.Add(hero);
                LoggerManager.Debug($"{fucName}: {label} 索引 {index} 超出范围，已追加到 [{list.Count - 1}]");
            }
        }

        private static bool TryParseIndex(string target, out int index)
        {
            index = -1;
            int pos = target.IndexOf(':');
            if (pos < 0)
                pos = target.IndexOf('-');
            if (pos < 0)
                return false;

            string indexPart = target.Substring(pos + 1).Trim();
            return int.TryParse(indexPart, out index);
        }

        private static List<int> ParseHobbyList(string val, string fucName)
        {
            List<int> result = new List<int>();
            if (string.IsNullOrWhiteSpace(val))
                return result;

            string normalized = val.Replace('－', '-').Replace('—', '-');
            string[] parts = normalized.Split('-');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (string.IsNullOrEmpty(part))
                    continue;

                if (int.TryParse(part, out int hobbyID))
                    result.Add(hobbyID);
                else
                    LoggerManager.Warning($"{fucName}: hobby 解析失败：{part}");
            }

            return result;
        }

        private static void NormalizeHeroName(ref GenerateNewHeroParams p)
        {
            if (!p.hasHeroName || string.IsNullOrWhiteSpace(p.heroName))
                return;

            string raw = p.heroName.Trim();
            int split = raw.IndexOf('.');
            if (split < 0)
                split = raw.IndexOf('。');
            if (split <= 0 || split >= raw.Length - 1)
                return;

            string familyName = raw.Substring(0, split).Trim();
            string givenName = raw.Substring(split + 1).Trim();
            if (string.IsNullOrWhiteSpace(familyName) || string.IsNullOrWhiteSpace(givenName))
                return;

            p.heroFamilyName = familyName;
            p.hasHeroFamilyName = true;
            p.heroName = familyName + givenName;
        }

    }
}
