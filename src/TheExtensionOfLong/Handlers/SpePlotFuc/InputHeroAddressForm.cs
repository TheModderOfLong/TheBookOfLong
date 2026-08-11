using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppSystem.Collections.Generic;
using System.Reflection;
using System;

namespace TheExtensionOfLong
{
    [SpePlotFuc("InputHeroAddressForm")]
    public static class SpePlotFucInputHeroAddressForm
    {
        /// <summary>
        /// 显示文本输入弹窗，确认后设置源角色对目标角色的称呼
        /// 会根据角色关系进行禁用词校验：不是恋人禁止使用"娘子"等，不是师徒禁止使用"师父"等，不是结拜禁止使用"大哥"等
        /// 校验通过跳转TruePlotId(可选)，校验失败提示用户并可重新输入，取消跳转CancelPlotId(可选)；均可省略或为空表示不跳转
        /// 格式: InputHeroAddressForm*标题#sourceHeroId(可选)#targetHeroId(可选)#TruePlotId-CancelPlotId(可选)
        ///   省略剧情参数: InputHeroAddressForm*标题 或 InputHeroAddressForm*标题#sourceId#targetId
        ///   仅取消跳转: InputHeroAddressForm*标题#sourceId#targetId#-CancelPlotId
        /// </summary>
        public static void TryCall(PlotController __instance, string fucName, string[] fucParams)
        {
            if (fucParams.Length < 1)
            {
                LoggerManager.Warning($"{fucName}: 参数不足，格式[{fucName}*标题#sourceHeroId(可选)#targetHeroId(可选)#TruePlotId-CancelPlotId(可选)]");
                return;
            }

            string title = fucParams[0];

            HeroData sourceHeroData = CommonHandlers.ResolveHeroId(__instance, fucParams.Length > 1 ? fucParams[1] : null, __instance.sourceInteractHero);
            if (sourceHeroData == null)
            {
                LoggerManager.Warning($"{fucName}: 源角色不存在，无法设置称呼");
                return;
            }

            HeroData targetHeroData = CommonHandlers.ResolveHeroId(__instance, fucParams.Length > 2 ? fucParams[2] : null, __instance.targetInteractHero);
            if (targetHeroData == null)
            {
                LoggerManager.Warning($"{fucName}: 目标角色不存在，无法设置称呼");
                return;
            }

            string[] plotParams = fucParams.Length > 3 ? fucParams[3].Split('-') : new string[0];

            // 捕获变量用于闭包
            string sourceHeroIdStr = sourceHeroData.heroID.ToString();
            string targetHeroIdStr = targetHeroData.heroID.ToString();
            string sourceHeroName = sourceHeroData.heroName;
            string targetHeroName = targetHeroData.heroName;
            string truePlotId = plotParams.Length >= 1 ? plotParams[0] : null;
            string cancelPlotId = plotParams.Length >= 2 ? plotParams[1] : null;

            // 预计算关系（闭包中使用）
            bool isLover = sourceHeroData.Lover == targetHeroData.heroID;
            bool isPreLover = false;
            bool isTeacherStudent = false;
            bool isBrother = false;
            try { isPreLover = sourceHeroData.HavePrelover(targetHeroData.heroID); } catch { }
            try { isTeacherStudent = sourceHeroData.HaveTeacherStudentRelation(targetHeroData.heroID); } catch { }
            try { isBrother = sourceHeroData.HaveBrother(targetHeroData.heroID); } catch { }
            // 预计算目标性别
            bool isTargetFemale = targetHeroData.isFemale;

            // 读取当前已有称呼作为默认值
            PlotEventLogData logData = CommonHandlers.GetPlotEventLogData();
            // 由于新角色可能因本体角色的增加而改变id，因此改为使用heroName作为key
            string key = "HeroAddressForm_" + sourceHeroIdStr + "_" + targetHeroIdStr;
            // string key = "HeroAddressForm_" + sourceHeroName + "_" + targetHeroName;
            string defaultText = "";
            if (logData != null && logData.HaveKey(key))
                defaultText = logData.Get(key) ?? "";

            TextInputDialog.Show(title, defaultText, "请输入称呼...", (text) =>
            {
                // 禁用词校验
                string forbiddenWord = FindForbiddenWord(text, isLover, isPreLover, isTeacherStudent, isBrother, isTargetFemale);
                if (forbiddenWord != null)
                {
                    // 校验失败：提示用户，弹窗停留可重新输入
                    InfoController infoController = InfoController._instance;
                    if (infoController != null)
                    {
                        infoController.AddInfoTab("<color=#FF4444>称呼与关系不符，无法使用此称呼</color>", "UIAtlas", null, "Woosh");
                    }
                    LoggerManager.Debug($"{fucName}: 称呼\"{text}\"包含禁用词\"{forbiddenWord}\"，与角色关系不符({sourceHeroName}→{targetHeroName})");
                    return;
                }

                // 校验通过，设置称呼
                string[] setParams = new string[] { text, sourceHeroIdStr, targetHeroIdStr };
                SpePlotFucSetHeroAddressForm.TryCall(__instance, "SetHeroAddressForm", setParams);

                // 成功提示
                if (logData != null)
                {
                    InfoController infoController = InfoController._instance;
                    if (infoController != null)
                    {
                        string displayText = text;
                        if (string.IsNullOrEmpty(displayText))
                            displayText = GameController.Instance?.GetHeroName(sourceHeroData, targetHeroData) ?? displayText;
                        infoController.AddInfoTab(
                            $"<color=#FF4444>{sourceHeroName}</color>对<color=#FF4444>{targetHeroName}</color>的称呼已变更为<color=#FFA500>{displayText}</color>",
                            "UIAtlas", null, "Woosh");
                    }
                }

                if (!string.IsNullOrWhiteSpace(truePlotId))
                {
                    __instance.ChangePlotDataBase(truePlotId);
                }
            },
            () =>
            {
                // 取消回调
                if (!string.IsNullOrWhiteSpace(cancelPlotId))
                {
                    __instance.ChangePlotDataBase(cancelPlotId);
                }
            });
        }

        /// <summary>根据角色关系和目标性别检查输入称呼是否包含禁用词，返回匹配到的禁用词；无匹配返回 null</summary>
        private static string FindForbiddenWord(string text, bool isLover, bool isPreLover, bool isTeacherStudent, bool isBrother, bool isTargetFemale)
        {
            // 不是恋人 → 禁止恋人专属称呼
            if (!isLover)
            {
                foreach (var word in _loverForbiddenWords)
                {
                    if (text.Contains(word)) return word;
                }
            }

            // 不是准恋人且不是恋人 → 禁止准恋人专属称呼（恋人可使用准恋人称呼）
            if (!isPreLover && !isLover)
            {
                foreach (var word in _preLoverForbiddenWords)
                {
                    if (text.Contains(word)) return word;
                }
            }

            // 不是师徒 → 禁止师徒专属称呼
            if (!isTeacherStudent)
            {
                foreach (var word in _teacherStudentForbiddenWords)
                {
                    if (text.Contains(word)) return word;
                }
            }

            // 不是结拜 → 禁止结拜专属称呼
            if (!isBrother)
            {
                foreach (var word in _brotherForbiddenWords)
                {
                    if (text.Contains(word)) return word;
                }
            }

            // 目标为女性 → 禁止男性专属称呼
            if (isTargetFemale)
            {
                foreach (var word in _maleForbiddenWords)
                {
                    if (text.Contains(word)) return word;
                }
            }

            // 目标为男性 → 禁止女性专属称呼
            if (!isTargetFemale)
            {
                foreach (var word in _femaleForbiddenWords)
                {
                    if (text.Contains(word)) return word;
                }
            }

            return null;
        }

private static readonly string[] _loverForbiddenWords = new string[]
        {
            // 妻子称谓
            "妻", "娘子", "老婆", "良人", "媳妇", "浑家", "内人", "拙荆", "糟糠",
            "内子", "贱内", "夫人", "结发妻", "正室", "婆娘", "娘亲", "妾", "太太", "贤内助",
            "孩儿她妈", "孩儿他妈", "婆娘", "老婆子", 
            // 丈夫称谓
            "丈夫", "相公", "老公", "夫君", "夫婿", "官人", "郎君", "夫主", "外子", "当家的",
            "孩儿她爸", "孩儿他爸",
        };

        /// <summary>准恋人专属称呼（非准恋人且非恋人关系禁用；恋人可使用准恋人称呼）</summary>
        private static readonly string[] _preLoverForbiddenWords = new string[]
        {
            // 准恋人暧昧称谓
            "情郎", "情人", "卿卿", "我卿", "心头肉",
            "挚爱", "爱人", "亲亲", "心肝", "亲爱", "心爱",
            "意中人", "心上人", "暗恋", "倾慕", "倾心", "心动", "钟情",
            // 暧昧亲昵称谓
            "冤家", "死鬼", "讨厌鬼", "小坏蛋", "宝贝", "宝宝", "臭宝", "大猪蹄子", 
            // 婚约/定情称谓
            "未婚夫", "未婚妻", "未过门", "许配", "定亲", "定情", "婚约", "指腹为婚",
            "娃娃亲", "青梅", "竹马", "两小无猜"
        };

        /// <summary>师徒专属称呼（非师徒关系禁用）</summary>
        private static readonly string[] _teacherStudentForbiddenWords = new string[]
        {
            // 对师父的称谓
            "师父", "师傅", "恩师", "师尊", "师公", "师祖", "师翁", "老师",
            // 对师母的称谓
            "师娘", "师母", "师太",
            // 对徒弟的称谓
            "徒弟", "徒儿", "弟子", "徒孙", "亲传", "嫡传", "关门", "入室",
        };

        /// <summary>结拜专属称呼（非结拜关系禁用）</summary>
        private static readonly string[] _brotherForbiddenWords = new string[]
        {
            // 结拜称谓
            "结拜", "结义", "金兰", "拜把子", "兄弟", "八拜之交", "换帖",
            // 义兄妹称谓
            "义兄", "义弟", "义姐", "义妹", "义姊",
            // 兄弟互称
            "贤弟", "愚兄", "贤兄", "愚弟", "贤姊", "贤妹", "愚姐", "愚妹",
            "结拜兄弟", "结拜姐妹", "拜把兄弟"
        };

        /// <summary>男性专属称呼（目标为女性时禁用）</summary>
        private static readonly string[] _maleForbiddenWords = new string[]
        {
            // 丈夫称谓
            "丈夫", "相公", "老公", "夫君", "夫婿", "官人", "郎君", "夫主", "外子", "当家的",
            "孩儿她爸", "孩儿他爸",
            // 未婚夫
            "未婚夫",
            // 男师称谓
            "师父", "师傅", "恩师", "师尊", "师公", "师祖", "师翁", "老夫子",
            // 男徒称谓
            "徒儿", "亲传", "嫡传", "关门弟子", "弟子", "徒孙",
            // 师兄弟
            "师兄", "师弟",
            // 义兄弟
            "义兄", "义弟",
            // 兄弟互称（男性向）
            "贤弟", "愚兄", "贤兄", "愚弟",
            "结拜兄弟", "拜把兄弟", "袍泽", "同袍",
            // 准恋人男性称谓
            "情郎", "意中人", "心上人",
            // 竹马（指男性青梅竹马）
            "竹马",
            // 其他通称
            "爸", "爷", "爹", "公", "伯", "叔", "侄", "哥", "兄", "弟", "爷", "郎", "雄",
        };

        /// <summary>女性专属称呼（目标为男性时禁用）</summary>
        private static readonly string[] _femaleForbiddenWords = new string[]
        {
            // 妻子称谓
            "妻", "娘子", "老婆", "良人", "媳妇", "浑家", "内人", "拙荆", "糟糠",
            "内子", "贱内", "夫人", "结发妻", "正室", "婆娘", "娘亲", "妾", "太太", "贤内助",
            "孩儿她妈", "孩儿他妈", "老婆子",
            // 未婚妻
            "未婚妻",
            // 师母称谓
            "师娘", "师母", "师太",
            // 师姐妹
            "师姐", "师妹",
            // 义姐妹
            "义姐", "义妹", "义姊",
            // 姐妹互称（女性向）
            "贤姊", "贤妹", "愚姐", "愚妹",
            "结拜姐妹",
            // 青梅（指女性青梅竹马）
            "青梅",
            // 其他通称
            "娘", "妈", "母", "妇", "姑", "姨", "婶", "嫂", "姐", "妹", "女", "妃", "雌",
            "女士", "千金", "闺秀", "淑女",
            "丫头", "妮子", "闺女",
            "红颜", "佳人", "美人",
            "令爱", "令嫒", "妾身",
            "奴家", "贱妾"
        };

        /// <summary>
        /// 尝试调用"SetTargetHero"功能，将指定角色设置为当前剧情环境中的角色对象
        /// 格式: SetTargetHero*角色ID/角色名称/关键字角色#对象(可选)
        ///   角色ID/角色名称/关键字角色: "NULL"(忽略大小写)时进入清除模式，将目标槽位设为null
        ///   对象(可选): 要设置的目标槽位，默认为targetInteractHero
        ///     支持的关键字: targetInteractHero/目标互动角色, sourceInteractHero/源互动角色,
        ///                  PlotInteractHero[-Index]/剧情互动角色[-索引]
        ///   PlotInteractHero行为(正常模式)：
        ///     不指定Index → 追加到PlotInteractHeroList末尾
        ///     指定Index且Index存在 → 替换该位置
        ///     指定Index但Index越界 → 追加到末尾
        ///   PlotInteractHero行为(NULL模式)：
        ///     不指定Index → 清空plotInteractHeroList
        ///     指定Index且Index存在 → 移除该位置的角色
        ///     指定Index但Index越界 → 不操作并警告
        /// 示例: SetTargetHero*小白                              → 将小白设为targetInteractHero
        ///       SetTargetHero*player#sourceInteractHero         → 将玩家设为sourceInteractHero
        ///       SetTargetHero*ChooseHero                        → 将选中角色设为targetInteractHero
        ///       SetTargetHero*小白#PlotInteractHero             → 将小白追加到PlotInteractHeroList
        ///       SetTargetHero*小白#PlotInteractHero-1           → 将小白设为PlotInteractHeroList[1]，越界则追加
        ///       SetTargetHero*NULL#targetInteractHero           → 将targetInteractHero设为null
        ///       SetTargetHero*NULL#PlotInteractHero             → 清空plotInteractHeroList
        ///       SetTargetHero*NULL#PlotInteractHero-0           → 移除plotInteractHeroList[0]
        /// </summary>
    }
}
