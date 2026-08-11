# 剧情文件结构

## 说明

本文只聚焦"剧情选项补丁配置"：

- `PlotChoiceDataController` 是"剧情选项补丁控制器"。其用于实现在PlotController的角色互动方法中新增、移除和修改选项（通过HarmonyPatch时读取nowChoice以实现）

---

### PlotChoiceData 主结构

下面是按 JSON 形式写出的主结构。  
为了可读性，嵌套对象只保留骨架，详细说明放在后文。

```jsonc
[
    {
        "patchFunction": "AskHeroJoinTeam",  // 需要补丁的函数
        "conditionGroup": "[$GetStrVal:自定义选项$][=]1",  // 条件组
        "insertType": {
                "name": "Before",
                "value": 1
            }, // 插入类型
        "insertPos": null,  // 选项插入的位置
        "overwriteChoiceText": "可以加入我的队伍吗？",  // 覆盖选项的选项描述
        "priority": 10,  // 优先级
        "choiceData": {
            "choiceText": "可以加入我的队伍吗？",
            "callFuc": "ChangePlotDataBase",
            "callParam": "110",
            "inited": false,
            "inheritMissionRequirement": false,
            "requirements": [
                {
                    "requireType": {
                        "name": "FavorDegree",
                        "value": 0
                    },
                    "requireNum": 80,
                    "autoChangeReuqireByDifficulty": false
                }
            ],
            "relations": [],
            "autoChangeCostByDifficulty": false,
            "costResource": [
                {
                    "resourceType": 1,
                    "resourceNum": 100
                }
            ],
            "describe": "需要好感度大于80，并消耗100银两",
            "destroyEvent": false,
            "playerInteractionTimeNeed": {
                "name": "ChatTime",
                "value": 1
            }
        } // 选项数据
    }  // 单个选项补丁数据
]
```

### PlotChoiceData 字段说明

#### 根字段

- `patchFunction`
  - 需要补丁的函数，此函数为PlotController类的方法，如AskHeroJoinTeam方法本身会调用剧情对话与选项，而选项补丁会在此方法的原选项列表中新增或修改选项内容
  - 其他属性都基于特定的patchFunction进行描述

- `conditionGroup`
  - 条件组，只有满足条件组的条件表达式时才会生效

- `insertType`
  - 插入的类型，可以选择如下选项值：
  - `0 = Overwrite`
  - 表示重写覆盖原选项的内容。用本choiceData替代原choiceData的内容，此时insertPos属性无效。
  - `1 = Before`
  - 插入到insertPos的索引之前。如insertPos=null、insertType=1，表示插入到最后一个选项之前。
  - `2 = After`
  - 插入到insertPos的索引之后。如insertPos=null、insertType=2，表示插入到最后一个选项之后。
  
- `insertPos`
  - 在原选项列表中插入的位置索引，可以使用如下值：
  - `null` = 不规定索引。此时默认为原选项列表的最后一个选项
  - `int值` = 原选项列表索引。如果超出原选项列表索引范围，则视为第一个或者最后一个。如`0`、`-1`、`-99`都表示第一个

- `overwriteChoiceText`
  - 覆盖选项的选项描述
  - 当`insertType=0`时，用于指定需要覆盖的原选项的选项描述，如果原选项描述不存在，则不做任何覆盖。
  - 当`insertType=1`或`insertType=2`时，则用于指定需要覆盖的其他选项补丁的选项描述，如果同描述选项补丁不存在或优先级低于本选项补丁，则以本选项`insertPos`和`choiceData`作为最终效果
  - 如果与其他选项补丁重复，则按priority进行覆盖
  - `patchFunction`+`overwriteChoiceText`可以确定选项补丁的唯一性

- `priority`
  - 覆盖选项的优先级
  - 当`overwriteChoiceText`和`insertType`同时与其他选项补丁重复时，根据优先级决定最终以哪一个选项补丁生效
  - 当`insertType=1`或`insertType=2`，且`overwriteChoiceText`与其他选项补丁不重复时，如果insertType+insertPos重复，则根据优先级决定选项的先后顺序（比如两个选项补丁参数都为insertPos=null、insertType=2，则优先级高的在前，优先级低的在后）
  - 数字越大，优先级越高

- `choiceData`
  - 选项数据。其结构见`SinglePlotChoiceData`的介绍
  - 其值可以为`null`，即`"choiceData": null`，则表示删除指定选项

---

### SinglePlotChoiceData

```jsonc
{
  "choiceText": "我来帮你。",
  "callFuc": "GetMainMission",
  "callParam": "12",
  "inited": false,
  "inheritMissionRequirement": false,
  "requirements": [
    {
      "requireType": 25,
      "requireNum": 3.0,
      "autoChangeReuqireByDifficulty": false
    }
  ],
  "relations": [0],
  "autoChangeCostByDifficulty": false,
  "costResource": [
    {
      "resourceType": 0,
      "resourceNum": 100.0
    }
  ],
  "describe": "需要势力等级 3",
  "destroyEvent": false,
  "playerInteractionTimeNeed": 0
}
```

- `choiceText`
  - 选项文本。

- `callFuc`
  - 选中后执行的函数。

- `callParam`
  - 函数参数。

- `inited`
  - 运行时初始化状态。

- `inheritMissionRequirement`
  - 是否继承任务要求。

- `requirements`
  - 选项数值门槛列表。

- `relations`
  - 关系门槛列表。

- `autoChangeCostByDifficulty`
  - 是否按难度自动调整消耗。

- `costResource`
  - 选这个选项会消耗的资源列表。

- `describe`
  - 选项描述。

- `destroyEvent`
  - 选中后是否销毁事件。

- `playerInteractionTimeNeed`
  - 要求玩家当前处于某种交互时段。

### 其它常见嵌套结构

#### PlotChoiceRequirement

```jsonc
{
  "requireType": 25,
  "requireNum": 3.0,
  "autoChangeReuqireByDifficulty": false
}
```

- `requireType`
  - 条件类型，见 `ChoiceRequirementType`。

- `requireNum`
  - 需要的数值。

- `autoChangeReuqireByDifficulty`
  - 是否按难度自动调整要求。

#### ResourceData

```jsonc
{
  "resourceType": 0,
  "resourceNum": 100.0
}
```

- `resourceType`
  - 资源类型编号。

- `resourceNum`
  - 数量。

#### ChoiceRequirementType

- `0 = FavorDegree`
  - 好感。
- `1 = Str`
  - 力道。
- `2 = Agl`
  - 身法。
- `3 = Inte`
  - 悟性。
- `4 = Wil`
  - 意志。
- `5 = Con`
  - 体魄。
- `6 = Mag`
  - 内息/真气相关属性。
- `7 = Internal`
  - 内功。
- `8 = Dodge`
  - 闪避。
- `9 = Unique`
  - 特技。
- `10 = Fist`
  - 拳掌。
- `11 = Sword`
  - 剑法。
- `12 = Knife`
  - 刀法。
- `13 = Long`
  - 长兵。
- `14 = Strange`
  - 奇门。
- `15 = Shoot`
  - 暗器/射术。
- `16 = Med`
  - 医术。
- `17 = Poison`
  - 毒术。
- `18 = Knowledge`
  - 学识。
- `19 = Speech`
  - 口才。
- `20 = DigAndCut`
  - 采掘/砍伐。
- `21 = Plant`
  - 种植。
- `22 = CraftEquip`
  - 锻造装备。
- `23 = CraftMed`
  - 炼药。
- `24 = CraftFood`
  - 烹饪。
- `25 = ForceLv`
  - 势力等级。
- `26 = GuardFavor`
  - 守卫/门派守卫好感之类的特殊好感项。（按命名推断）

#### RelationRequirementType

- `0 = isLover`
  - 恋人关系。
- `1 = isBrother`
  - 兄弟/结义关系。
- `2 = isTeacher`
  - 师徒中的"师父"关系。
- `3 = isStudent`
  - 师徒中的"弟子"关系。
- `4 = isHater`
  - 仇敌关系。

#### PlayerInteractionTimeType

- `0 = None`
  - 无时段要求。
- `1 = ChatTime`
  - 交谈时段。
- `2 = StudyFightTime`
  - 练武时段。
- `3 = StudySkillTime`
  - 学技能时段。
- `4 = TeachNewSkillTime`
  - 教授新武学时段。
- `5 = StudyLivingSkillTime`
  - 学生活技艺时段。
- `6 = StudyNewSkillTime`
  - 学习新技能时段。
- `7 = MissionTime`
  - 做任务时段。
- `8 = GiftTime`
  - 送礼时段。
- `9 = IdentifyTime`
  - 鉴定时段。
- `10 = CureByPlayerTime`
  - 玩家替他人治疗时段。
- `11 = CureForPlayerTime`
  - 他人为玩家治疗时段。
- `12 = ShameTime`
  - 羞辱/挑衅相关时段。
- `13 = ReduceLoyalTime`
  - 降忠时段。
- `14 = TeachTime`
  - 传授时段。
- `15 = ForceRewardTime`
  - 势力奖赏时段。
- `16 = DrinkTime`
  - 饮酒时段。
- `17 = GambleTime`
  - 赌博时段。
- `18 = AskGiftTime`
  - 索礼时段。
- `19 = ForceTeachNewSkillTime`
  - 势力内教授新武学时段。
- `20 = ForceTeachTime`
  - 势力内传授时段。
- `21 = ComfortInPrisonTime`
  - 狱中安抚时段。
- `22 = FreeChatTime`
  - 自由闲聊时段。
- `23 = BegTime`
  - 乞讨时段。
- `24 = EnlightTime`
  - 点化/开悟时段。
- `25 = ForceAskGiveItemTime`
  - 势力内索要物品时段。

---