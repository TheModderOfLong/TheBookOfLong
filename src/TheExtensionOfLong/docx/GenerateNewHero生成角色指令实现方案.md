# GenerateNewHero 生成角色指令实现方案

## 1. 目标

新增剧情指令：

```text
GenerateNewHero*对象#可选命名参数
```

该指令用于动态生成一个新 `HeroData`，并根据参数决定它是永久世界角色还是临时角色。命名上参考已有 `GenerateRandomItem`，但使用 `NewHero` 而不是 `RandomHero`，因为该指令不仅支持随机生成，也支持指定名称、势力、性别、年龄、性格、资质、立场、区域等。

核心目标：

- 生成新角色。
- 可选择注册为永久角色或临时角色。
- 可选择写入指定剧情上下文对象。
- 默认不污染剧情上下文。
- 后续可继续扩展命名参数。

## 2. 指令格式

```text
GenerateNewHero*对象#key=value#key=value
```

对象参数是第一个参数。若第一个参数为空、为 `null`，或第一个参数本身就是 `key=value`，则视为不设置上下文对象。

示例：

```text
GenerateNewHero*
GenerateNewHero*null#heroName=小白
GenerateNewHero*targetInteractHero#heroName=小白#belongForceID=-1
GenerateNewHero*PlotInteractHero#heroName=神秘剑客#heroForceLv=3#enterAreaID=100
GenerateNewHero*TempPlotHero#isTempHero=true#isRandomEnemy=true#heroForceLv=4
GenerateNewHero*TempPlotHero:0#heroName=刺客#isTempHero=true
```

参数值如果需要包含 `#`，使用现有 `(())` 分隔符保护机制：

```text
GenerateNewHero*targetInteractHero#heroName=((名字#带井号))
```

## 3. 上下文对象

对象参数用于决定生成后的角色写入哪里。对象解析语义应参考 `CommonHandlers.ResolveHeroSource`，但本指令需要的是“写入目标”，不是“读取目标”，因此建议单独实现一个 `AssignGeneratedHeroToContext`。

支持对象：

| 对象 | 行为 |
| --- | --- |
| `null` / 空 / 省略 | 不设置上下文，默认行为。 |
| `sourceInteractHero` | 写入 `PlotController.sourceInteractHero`。 |
| `targetInteractHero` | 写入 `PlotController.targetInteractHero`。 |
| `chooseHero` | 写入当前选择器选中的 `HeroIconController.heroData`。 |
| `TempPlotHero` | 追加到 `PlotController.tempPlotHero`。 |
| `TempPlotHero:Index` | 设置 `PlotController.tempPlotHero[Index]`，越界则追加。 |
| `PlotInteractHero` | 追加到 `PlotController.plotInteractHeroList`。 |
| `PlotInteractHero:Index` | 设置 `PlotController.plotInteractHeroList[Index]`，越界则追加。 |
| `MissionEventTargetHero` | 写入当前任务第一个 `missionTargetDatas[0].tirggerTargetID` 指向的角色槽位不合适，建议暂不支持写入，或仅作为后续扩展。 |
| `MissionEventSourceHero` | 写入 `nowMission.sourceHeroID` 指向的角色槽位不合适，建议暂不支持写入，或仅作为后续扩展。 |

说明：

- `ResolveHeroSource` 适合“读取现有角色”，可以借鉴关键字、索引解析和日志风格。
- `sourceInteractHero`、`targetInteractHero`、`TempPlotHero`、`PlotInteractHero` 都是安全的写入目标。
- `MissionEventTargetHero` / `MissionEventSourceHero` 本质是任务数据里的角色 ID 引用，直接改写会影响任务结构，风险高。若确实需要，建议做成显式参数，例如 `allowMissionContextWrite=true` 后再允许。
- `chooseHero` 依赖选择器当前 `chooseResult` 是否存在，若不存在应记录 Warning，并不回退到其他对象。

推荐默认：

```text
GenerateNewHero*
```

只生成并注册角色，不写入任何剧情上下文。

## 4. 参数设计

命名参数使用 `key=value`，多个参数用 `#` 分隔。

| 参数 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `heroName` | `string` | 本体随机命名 | 角色完整名称。不指定时传 `null` 给本体生成逻辑，并尽量避免与已有角色重名。支持 `姓.名` 格式，例如 `heroName=姜.映泉` 会解析为 `heroFamilyName=姜`、`heroName=姜映泉`。 |
| `heroFamilyName` / `familyName` / `surname` | `string` | 本体随机姓氏 | 角色姓氏。单独指定且未指定 `heroName` 时，会使用该姓氏重新随机完整姓名。 |
| `belongForceID` | `int` | 指令随机势力 | 角色所属势力。`-1` 表示无势力；不指定时由指令先随机有效势力，再传入本体生成逻辑。 |
| `heroForceLv` | `int/float` | 指令随机职级 | 角色职级/生成强度。不指定时由指令先随机 `0..4`，再传入本体生成逻辑，避免误生成掌门级角色。 |
| `isTempHero` | `bool` | `false` | 是否临时角色。临时角色走 `AddTempHero`，一般不进入世界区域。 |
| `sexLimit` | `SexLimit` | `None` | 性别限制。支持 `0/1/2` 和 `None/Male/Female`；`None` 时交由本体按势力性别比例/默认概率随机。 |
| `isRandomEnemy` | `bool` | `false` | 是否按随机敌人生成。普通 NPC 不建议开启。 |
| `nature` | `int` | 本体生成结果 | 性格，对应 `HeroData.nature`。不指定则保留本体随机结果。 |
| `age` | `int` | 本体生成结果 | 年龄，对应 `HeroData.age`。不指定则保留本体随机结果。 |
| `talent` | `int` | 本体生成结果 | 资质，对应 `HeroData.talent`。不指定则保留本体随机结果。 |
| `chaos` | `float/int` | 本体生成结果 | 立场，对应 `HeroData.chaos`。不指定则保留本体随机结果；指定时限制到 `0..100`。 |
| `evil` | `float/int` | 本体生成结果 | 邪恶，对应 `HeroData.evil`。不指定则保留本体按势力风格生成的结果；指定时限制到 `0..100`。 |
| `enterAreaID` | `int` | 自动决定 | 永久角色加入世界时的目标区域。指定后优先使用。 |
| `hide` | `bool` | `false` | 是否隐藏。即使不指定也会设置为 `false`。 |
| `recruitAble` | `bool` | 生成默认值 | 是否可招募。不指定则保留 `GenerateHeroData` 的结果。 |
| `loveAble` | `bool` | 生成默认值 | 是否可亲密/恋爱。不指定则保留 `GenerateHeroData` 的结果。 |
| `inActive` | `bool` | 不设置 | 是否生成后设为不活跃。支持 `0/1/true/false`；`1/true` 表示不活跃，`0/false` 表示活跃并删除不活跃 key。 |
| `heroNickName` | `string` | 生成默认值 | 外号/昵称。不指定则保留生成默认值。 |
| `hobby` | `List<int>` | 生成默认值 | 爱好 ID 列表，多个爱好用 `-` 分隔，例如 `hobby=1-3-5`。 |
| `fame` | `float/int` | 生成默认值 | 名望，对应 `HeroData.fame`。 |
| `loyal` | `float/int` | 生成默认值 | 忠诚，对应 `HeroData.loyal`。 |

## 5. 生成与注册流程

推荐流程：

```text
解析对象参数
解析命名参数
GenerateHeroData(heroID=-1, ...)
  -> isTempHero=true  : WorldData.AddTempHero(hero)
  -> isTempHero=false : WorldData.AddNewHero(hero)
  -> 永久角色必要时 HeroEnterArea(hero, area)
仅在显式传参时应用 nature/age/talent/chaos/evil/recruitAble/loveAble/heroNickName/hobby/fame/loyal 等覆盖参数；hide 固定默认 false
CountHeroData(hero)
按对象参数写入剧情上下文
```

伪代码：

```csharp
public static void TryCall(PlotController pc, string fucName, string[] fucParams)
{
    string contextTarget = "null";
    int paramStartIdx = 0;

    if (fucParams.Length > 0 && !string.IsNullOrWhiteSpace(fucParams[0]))
    {
        string first = fucParams[0].Trim();
        if (!first.Contains("="))
        {
            contextTarget = first;
            paramStartIdx = 1;
        }
    }

    var args = ParseNamedArgs(fucParams, paramStartIdx);

    GameController gc = GameController.Instance;
    WorldData world = gc.worldData;

    bool isTempHero = args.GetBool("isTempHero", false);
    int belongForceID = args.Has("belongForceID")
        ? args.GetInt("belongForceID")
        : PickRandomForceID(world);
    float heroForceLv = args.Has("heroForceLv")
        ? args.GetFloat("heroForceLv")
        : PickRandomHeroForceLv();

    // 注意：底层 GenerateHeroData 固定按临时生成方式调用，避免 heroID=-1 时误触发本体非临时注册路径。
    // 是否注册为永久角色由后续 AddNewHero / AddTempHero 决定。
    HeroData hero = gc.GenerateHeroData(
        args.GetString("heroName", null),
        -1,
        belongForceID,
        heroForceLv,
        null,
        true,
        args.GetEnum("sexLimit", SexLimit.None),
        args.GetBool("isRandomEnemy", false),
        false);

    if (isTempHero)
    {
        world.AddTempHero(hero);
    }
    else
    {
        world.AddNewHero(hero);
        ApplyEnterArea(gc, world, hero, args);
    }

    ApplyOverrideParams(hero, args);
    gc.CountHeroData(hero);

    AssignGeneratedHeroToContext(pc, contextTarget, hero);
}
```

## 6. 上下文写入规则

### 6.1 null / 省略

不写入任何上下文。

```text
GenerateNewHero*null#heroName=小白
GenerateNewHero*heroName=小白
GenerateNewHero*
```

注意：这种情况下如果剧情后续要引用这个角色，需要另行保存 ID。建议后续增加：

```text
saveHeroIDKey=SomeKey
```

生成后写入 `PlotEventLogData`：

```text
SomeKey = hero.heroID
```

### 6.2 sourceInteractHero / targetInteractHero

```csharp
pc.sourceInteractHero = hero;
pc.targetInteractHero = hero;
```

### 6.3 PlotInteractHero

`PlotInteractHero` 不带索引时追加到末尾：

```csharp
if (pc.plotInteractHeroList == null)
    pc.plotInteractHeroList = new List<HeroData>();

pc.plotInteractHeroList.Add(hero);
```

`PlotInteractHero:Index` 带索引时：

- index 存在：替换该位置。
- index 越界：追加到末尾并记录 Debug。
- index 解析失败：Warning 并不写入。

### 6.4 TempPlotHero

`TempPlotHero` 不带索引时追加到末尾：

```csharp
if (pc.tempPlotHero == null)
    pc.tempPlotHero = new List<HeroData>();

pc.tempPlotHero.Add(hero);
```

`TempPlotHero:Index` 带索引时同 `PlotInteractHero:Index`。

注意：

- `PlotController.tempPlotHero` 是 `List<HeroData>`，不是单个字段。
- 即使生成的是永久角色，也可以写入 `TempPlotHero` 作为剧情上下文引用；但语义上不推荐。
- 即使生成的是临时角色，也可以写入 `targetInteractHero`；但默认由对象参数显式决定，不自动写入。

### 6.5 chooseHero

```csharp
ChooseController chooseController = ChooseController._instance;
HeroIconController icon = chooseController?.chooseResult?.GetComponent<HeroIconController>();
if (icon == null)
{
    LoggerManager.Warning("GenerateNewHero: chooseHero 目标不存在");
    return;
}

icon.heroData = hero;
```

如果当前选择器没有打开或选中对象不是角色图标，应 Warning，不回退到其他上下文。

### 6.6 MissionEventTargetHero / MissionEventSourceHero

这两个对象在 `ResolveHeroSource` 中可以读取，但写入语义更危险：

- `MissionEventTargetHero` 涉及 `nowMission.missionTargetDatas[0].tirggerTargetID`。
- `MissionEventSourceHero` 涉及 `nowMission.sourceHeroID`。

建议第一版不要支持写入这两个目标。若用户传入，应 Warning：

```text
GenerateNewHero: MissionEventTargetHero 暂不支持作为写入目标
```

如果后续确实需要，可以增加显式参数并单独评估任务结构影响。

## 7. 默认值策略

### heroName

不指定时传 `null` 给 `GenerateHeroData`，使用本体随机命名逻辑。指令层只额外做最多 20 次重试，尽量避免与已有角色重名。

显式指定 `heroName` 时不强制去重，但如果已存在同名角色，应记录 Warning。

`heroName` 按运行时语义表示完整姓名。为兼容 `SpeHeroData.csv` 的配置习惯，支持 `姓.名` 格式：

```text
heroName=姜.映泉
```

解析后写入：

```text
heroFamilyName=姜
heroName=姜映泉
```

也可以单独指定 `heroFamilyName`：

```text
heroFamilyName=司马
```

若只指定 `heroFamilyName` 且不指定 `heroName`，指令会先调用本体生成角色，再用该姓氏重新随机一个完整姓名。

### belongForceID

`belongForceID` 是 `GenerateHeroData` 的入口参数，本体不会在该参数缺省时自动“随机势力”。因此不指定时由指令先随机一个有效势力，再传入本体生成逻辑。

建议语义：

```text
参数不存在：随机有效势力
belongForceID=-1：无势力
belongForceID>=0：指定势力
```

随机势力最低要求：

```csharp
force != null && force.forceID >= 0
```

### heroForceLv

`heroForceLv` 也是 `GenerateHeroData` 的入口参数，会影响年龄、强度、技能、名望等生成结果。本体不会在该参数缺省时自动“随机职级”，因此不指定时由指令先随机，再传入本体。

建议语义：

```text
参数不存在：随机 0..4
参数存在：按传入值，最终 Clamp 到 -1..5
```

默认不建议随机到 `5`，避免误触发掌门/首领相关逻辑。

### sexLimit / nature / age / talent / chaos / evil

这些字段本体已经有完整生成逻辑：

- `sexLimit=None` 时，本体按势力性别比例或默认概率决定性别。
- `nature`、`age`、`talent`、`chaos` 不指定时，保留本体随机结果。
- `evil` 不指定时，保留本体按势力风格生成并随机波动后的结果。

因此这些参数不需要指令层重新计算默认值，只在显式传参时覆盖。

### enterAreaID

永久角色加入世界时使用。

优先级：

```text
1. enterAreaID 参数存在且区域有效：进入指定区域
2. 有势力且不是临时角色：进入势力 mainAreaID
3. 无势力或势力外：从 cityAreaID / villageAreaID 随机
4. 仍无法获得区域：只注册角色，不进入区域，并记录 Warning
```

临时角色 `isTempHero=true` 时默认不处理 `enterAreaID`。如果用户同时传入 `isTempHero=true#enterAreaID=...`，建议 Warning 并忽略。

## 8. 参数覆盖

这些参数应在 `GenerateHeroData` 后覆盖：

```csharp
if (args.TryGetInt("nature", out int nature))
    hero.nature = nature;

if (args.TryGetInt("age", out int age))
    hero.age = age;

if (args.TryGetInt("talent", out int talent))
    hero.talent = talent;

if (args.TryGetFloat("chaos", out float chaos))
    hero.chaos = Mathf.Clamp(chaos, 0f, 100f);

if (args.TryGetFloat("evil", out float evil))
    hero.evil = Mathf.Clamp(evil, 0f, 100f);

hero.hide = args.GetBool("hide", false);

if (args.TryGetBool("recruitAble", out bool recruitAble))
    hero.recruitAble = recruitAble;

if (args.TryGetBool("loveAble", out bool loveAble))
    hero.loveAble = loveAble;

if (args.TryGetString("heroNickName", out string heroNickName))
    hero.heroNickName = heroNickName;

if (args.TryGetIntList("hobby", '-', out List<int> hobby))
    hero.hobby = hobby;

if (args.TryGetFloat("fame", out float fame))
    hero.fame = fame;

if (args.TryGetFloat("loyal", out float loyal))
    hero.loyal = loyal;
```

覆盖后调用：

```csharp
GameController.Instance.CountHeroData(hero);
```

## 9. 与本体方法的关系

不建议直接调用本体 `GameController.WorldAddNewHero`。

原因：

- 它不能指定名称。
- 它不能指定性别。
- 它不能指定年龄、性格、立场、邪恶、资质。
- 它不能灵活指定上下文对象。
- 它的出生区域逻辑固定。

本指令应复用 `GameController.GenerateHeroData`，再自行处理注册、进入区域、字段覆盖和上下文写入。

## 10. 推荐命名

最终建议指令名：

```text
GenerateNewHero
```

理由：

- 与已有 `GenerateRandomItem` 风格一致。
- 比 `GenerateRandomHero` 更准确，因为它支持指定参数，不只是随机。
- 比 `WorldAddNewHero` 更宽泛，能覆盖临时角色和永久角色。
- 比 `GenerateHeroData` 更适合剧情指令，避免与本体底层方法混淆。

## 11. 示例

生成随机永久角色，不写入上下文：

```text
GenerateNewHero*
```

生成无势力永久角色，并设为当前目标交互角色：

```text
GenerateNewHero*targetInteractHero#heroName=小白#belongForceID=-1#enterAreaID=100
```

生成临时敌人，并加入 `tempPlotHero`：

```text
GenerateNewHero*TempPlotHero#heroName=刺客#isTempHero=true#isRandomEnemy=true#heroForceLv=4
```

生成指定女性角色，并追加到 `plotInteractHeroList`：

```text
GenerateNewHero*PlotInteractHero#heroName=侠女#sexLimit=Female#age=18#nature=2#talent=2#chaos=90#evil=10
```

生成指定外号、爱好、名望和忠诚的可招募角色：

```text
GenerateNewHero*targetInteractHero#heroName=无名刀客#heroNickName=断水#hobby=1-3-5#recruitAble=true#loveAble=false#fame=120#loyal=80
```

生成角色并替换 `plotInteractHeroList[1]`：

```text
GenerateNewHero*PlotInteractHero:1#heroName=新同伴#heroForceLv=2
```

## 12. 中文参数解析与 SpeHeroData 字段语义方案

本指令后续扩展参数时，参考 `SpeHeroData.csv` 的字段语义，但不走特殊角色模板克隆逻辑。也就是说，`GenerateNewHero` 仍然先生成一个普通新角色，再按命名参数覆盖字段。

### 12.1 通用解析原则

多数参数支持“数字 ID / 中文字符串”双写法：

```text
如果参数值能解析为 int 或 float，则按数值处理。
否则按中文名称、枚举名或配置表显示文本解析。
```

解析失败时记录 Warning，并跳过该参数，不静默吞掉。

### 12.2 性别 sexLimit

为了匹配 `SexLimit` 枚举语义，支持：

```text
0 / None / 无  -> SexLimit.None
1 / Male / 男  -> SexLimit.Male
2 / Female / 女 -> SexLimit.Female
```

`sexLimit=无` 表示不强制性别，交由本体按势力性别比例或默认概率随机。

### 12.3 门派与武学流派

`belongForceID` 表示所属势力，支持：

```text
belongForceID=-1
belongForceID=3
belongForceID=仙霞派
belongForceID=无
```

其中 `无` 等价于 `-1`。

`skillForceID` / `skillForce` 表示武学流派，对应 `HeroData.skillForceID`，支持：

```text
skillForceID=3
skillForce=仙霞派
skillForce=默认
```

`默认` 表示不覆盖，保留本体生成结果。普通生成逻辑中 `skillForceID` 通常等于 `belongForceID`，但 `SpeHeroData.csv` 中存在“门派=无，武学流派=仙霞派”的语义，因此这两个参数应分开。

第一版建议：`skillForceID/skillForce` 只修改 `hero.skillForceID`；`kungfuFocus/livingFocus` 是否覆盖专精列表由显式参数决定，不由 `skillForce` 自动重算。

### 12.4 等级与实力等级

`heroForceLv` 对应 `HeroData.heroForceLv`，表示职级/门派辈分。支持数字，也建议支持常见中文等级名，例如：

```text
heroForceLv=3
heroForceLv=掌门
heroForceLv=长老
heroForceLv=亲传弟子
heroForceLv=正式弟子
```

具体中文到数值的映射应优先参考本体 `GlobalData.HeroForceLvName` 或同类文本表，不建议凭感觉硬编码。

`heroStrengthLv` / `strengthLv` 对应 `HeroData.heroStrengthLv`，表示实力等级，支持：

```text
heroStrengthLv=6
strengthLv=15
```

不指定时保留本体生成结果。指定后应在 `GenerateHeroData` 后覆盖，并重新 `CountHeroData(hero)`。

### 12.5 性格、资质、立场、邪恶

`nature` 支持数字与中文性格名：

```text
nature=2
nature=稳重
nature=叛逆
```

中文到数值应从本体文本列表解析，例如 `GlobalData.NatureText`。

`talent` 支持数字与中文资质名：

```text
talent=1
talent=聪颖
talent=平平
```

`SpeHeroData.csv` 的“立场”列同时表达 `chaos` 与 `evil` 两类语义，但 `GenerateNewHero` 指令已经把它们拆成两个独立参数，因此中文解析也保持分开：

```text
chaos=50
chaos=平常
chaos=戒律
chaos=无序

evil=25
evil=善良
evil=中立
evil=冷酷
```

不建议新增一个 `standpoint=平常/善良` 作为主参数。若以后需要兼容配置表整格写法，可以作为额外快捷参数实现，但底层仍应拆分为 `chaos` 与 `evil`。

### 12.6 专精、绝学、标签

`kungfuFocus` 参考 `SpeHeroData.csv` 的“擅长武学”，对应 `HeroData.kungfuSkillFocus`：

```text
kungfuFocus=内功/剑法
kungfuFocus=内功-剑法
kungfuFocus=0-2
```

`livingFocus` 参考“擅长技艺”，对应 `HeroData.livingSkillFocus`：

```text
livingFocus=医术/学识
livingFocus=医术-学识
livingFocus=1-4
```

专精字段的分隔符兼容 `/` 与 `-`。

`uniqueSkill` 参考“绝学”，支持武学 ID 或武学名称：

```text
uniqueSkill=1001
uniqueSkill=无影乱剑
```

实现时应找到或创建对应 `KungfuSkillLvData`，加入角色武学列表后设为 `hero.uniqueSkill`。

`tags` 参考“标签”，支持标签 ID 或标签名。分隔符兼容 `;`、`-`：

```text
tags=领袖;御剑;内家
tags=领袖-御剑-内家
tags=1-2-3
```

实现时优先调用 `hero.AddTag(tagID, -1, null, false, false)`，让本体处理标签替换和互斥逻辑。

### 12.7 出师角色与关系设定

`teacher` 参考“出师角色”，支持 heroID、角色名称和上下文角色引用：

```text
teacher=1
teacher=姜映泉
teacher=targetInteractHero
```

该参数应在角色注册进世界后执行。推荐调用老师角色的 `AddStudent(hero.heroID, false)`，让本体双向写入 `Teacher/Students`。

`relations` 参考“关系设定”，保留配置表风格，使用 `;` 分隔多项，使用 `:` 分隔关系类型与目标角色：

```text
relations=朋友:2;亲属:7;仇人:65
relations=朋友:姜映泉;仇人:陆仁谦
```

支持的关系类型建议包括：

```text
朋友 / friend      -> AddFriend
仇人 / hater       -> AddHater
亲属 / relative    -> Relatives 双向写入，或后续封装专门方法
结义 / brother     -> AddBrother
夫妻 / lover       -> SetLover
恋人 / prelover    -> AddPrelover
出师 / teacher     -> 老师 AddStudent
```

关系设定只建议对永久角色生效。临时角色 `isTempHero=true` 时应 Warning 并跳过，因为本体大量关系方法会检查 `isTempHero == false`。

### 12.8 皮肤与特殊 skeleton

`skinID` 参考“皮肤ID”，支持：

```text
skinID=-1
skinID=-7
skinID=10
skinLv=0
```

实现时推荐调用：

```csharp
hero.SetSkin(skinID, skinLv);
```

`speSkeleton` 参考“特殊skeleton”，对应特殊骨骼显示语义：

```text
speSkeleton=true
speSkeleton=false
speSkeleton=1
speSkeleton=0
```

该参数很可能需要设置 `hero.speHero`，它会影响 `GenerateHeroSkeleton` 的资源路径和部分角色默认逻辑。资源不存在时可能显示异常，因此应在文档中标为高级参数。

### 12.9 分隔符规则

按字段语义控制分隔符，不做全局混用：

| 字段 | 推荐分隔符 | 兼容分隔符 |
| --- | --- | --- |
| `kungfuFocus` | `/` | `-` |
| `livingFocus` | `/` | `-` |
| `tags` | `;` | `-` |
| `hobby` | `-` | 无需兼容 `/` |
| `relations` | `;` 分隔关系项，`:` 分隔类型和值 | 不建议兼容 `-`，避免与角色名或负数冲突 |

参数值如需包含 `#`，继续使用现有 `((...))` 保护机制。

### 12.10 推荐实现顺序

生成后、注册前应用：

```text
skillForceID / skillForce
heroStrengthLv / strengthLv
kungfuFocus
livingFocus
uniqueSkill
tags
skinID / skinLv
speSkeleton
```

注册后、仅永久角色应用：

```text
teacher
relations
```

最后统一执行：

```csharp
GameController.Instance.CountHeroData(hero);
```

### 12.11 CommonHandlers 公共解析方法命名

为保持与现有 `CommonHandlers` 风格一致，公共解析方法不采用 `TryResolveXxx(out ...)` 命名，而采用现有的 `Resolve...` / `Parse...` 风格。

现有风格示例：

```csharp
ResolveHeroSource(...)
ResolveHeroId(...)
ResolveItemSource(...)
ResolveKungfuSkillSource(...)
ParseChooseType(...)
ParseChooseFilterType(...)
```

新增方法建议放在 `CommonHandlers` 中：

```csharp
public enum FocusListKind
{
    Kungfu,
    Living
}

public static int ResolveForceID(string raw, int defaultForceID = -1)
public static int ResolveSkillID(string raw, int defaultSkillID = -1)
public static int ResolveTagID(string raw, int defaultTagID = -1)
public static List<int> ResolveTagList(string raw)
public static List<int> ResolveFocusList(string raw, FocusListKind kind)

public static SexLimit ParseSexLimit(string raw, SexLimit defaultValue = SexLimit.None)
public static int ParseNature(string raw, int defaultValue = -1)
public static int ParseTalent(string raw, int defaultValue = -1)
public static float ParseHeroForceLv(string raw, float defaultValue = -1f)
```

命名规则：

- `Resolve...`：需要查询世界数据、数据库、角色上下文或配置表名称映射，例如门派、标签、武学、专精列表。
- `Parse...`：主要是固定枚举、固定文本表或数值语义转换，例如性别、性格、资质、职级。

失败处理也与现有 `CommonHandlers` 保持一致：

- 返回传入的默认值，或返回空列表。
- 在方法内部记录 `LoggerManager.Warning`。
- 调用方不需要写 `out` 分支，只需按返回值继续处理。

调用示例：

```csharp
p.belongForceID = CommonHandlers.ResolveForceID(val, -1);
p.sexLimit = CommonHandlers.ParseSexLimit(val, SexLimit.None);
p.nature = CommonHandlers.ParseNature(val, p.nature);
p.heroForceLv = CommonHandlers.ParseHeroForceLv(val, p.heroForceLv);
p.kungfuFocus = CommonHandlers.ResolveFocusList(val, FocusListKind.Kungfu);
p.tagIDs = CommonHandlers.ResolveTagList(val);
```

`ResolveForceID` 语义建议：

```text
-1 / 无 / None / none -> -1
数字 -> worldData.GetForce(id) 有效时返回该 ID
中文或字符串 -> worldData.GetForce(name) 有效时返回 force.forceID
失败 -> defaultForceID
```

`ResolveFocusList` 不使用裸字符串 `focusType`，而使用 `FocusListKind`，避免调用方手写字符串造成拼写错误。
