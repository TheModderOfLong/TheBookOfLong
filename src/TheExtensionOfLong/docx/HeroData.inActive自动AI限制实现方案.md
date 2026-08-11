# HeroData.{heroID}.inActive 自动 AI 限制实现方案

## 1. 目标

新增一个 MOD 侧角色状态 `inActive`，用于限制 NPC 的自动 AI 行为。

语义定义：

```text
HeroData.{heroID}.inActive = "1"  => 不活跃，限制自动 AI
HeroData.{heroID}.inActive = "0"  => 活跃
key 不存在                       => 活跃
```

`inActive` 只限制自动 AI，不等同于本体的 `hide` 或 `needRemove`：

- 不影响角色可见性。
- 不影响剧情引用角色。
- 不影响玩家/剧情/MOD 代码主动安排角色行为。
- 不打断已经在大地图上移动的角色；角色走完当前移动后，不再由自动 AI 发起新移动。

## 2. 总体方案

最终采用两个核心 Patch：

1. Patch `AIController.StartMoveToAnotherArea`
   - 禁止 `inActive` 角色发起新的区域移动。
   - 阻止写入 `MoveOnBigMap`、加入 `needLeaveHero`、离开区域、创建大地图 NPC 图标。

2. Patch `AIController.SetAIStuff`
   - 限制 `inActive` 角色被设置为禁止行为。
   - 禁止带跨区域目标的任务。
   - 将非法任务替换为安全任务，例如 `Free` / `Rest` / `CureSelf`，或直接阻止。

关键原则：

```text
inActive 角色默认不能通过 StartMoveToAnotherArea / SetAIStuff 执行禁止行为。
后续新增“改变角色行为”的指令时，由指令参数决定是否为强制行为。
强制行为使用 hero.SetHeroAIData(aiData) 直接写入当前 AI，绕过 SetAIStuff 的限制。
非强制行为使用 AIController.SetAIStuff(hero, aiData)，遵守 inActive 限制。
```

因此本方案不引入自动 AI 上下文。`inActive` 的语义是：

```text
不活跃角色不接受普通/非强制行为调度；
只有显式声明为“强制”的剧情或 MOD 指令，才能绕过限制。
```

## 3. 状态存储

使用本体会随存档保存的 `PlotEventLogData`：

```text
HeroData.{heroID}.inActive
```

读取规则：

```csharp
public static bool IsInActive(HeroData hero)
{
    if (hero == null)
        return false;

    PlotEventLogData log = CommonHandlers.GetPlotEventLogData();
    if (log == null)
        return false;

    string key = GetKey(hero);
    if (!log.HaveKey(key))
        return false;

    return log.Get(key) == "1";
}

public static string GetKey(HeroData hero)
{
    return $"HeroData.{hero.heroID}.inActive";
}
```

设置规则：

```csharp
public static void SetInActive(HeroData hero, bool inActive)
{
    PlotEventLogData log = CommonHandlers.GetPlotEventLogData();
    if (hero == null || log == null)
        return;

    log.Set(GetKey(hero), inActive ? "1" : null);
}
```

说明：

- key 不存在代表活跃。
- `PlotEventLogData.Set(key, null)` 会移除 key，可用于把角色恢复为活跃。
- 读取时只有值为 `"1"` 才视为不活跃；`"0"`、`false`、key 不存在都视为活跃。

## 4. 强制行为策略

后续新增改变角色行为的剧情指令时，建议增加 `force` 参数。

示例语义：

```text
SetHeroAIStuff*角色#行为#目标#持续时间#force
```

执行规则：

```csharp
if (force)
{
    // 强制行为：直接写入当前 AI，不走 SetAIStuff。
    hero.SetHeroAIData(aiData);
}
else
{
    // 非强制行为：走本体调度入口，遵守 inActive 限制。
    aiController.SetAIStuff(hero, aiData, false);
}
```

这样语义最直接：

- `force = false`：尊重 `inActive`，不活跃角色不应执行禁止行为。
- `force = true`：剧情或 MOD 明确要求角色执行，允许绕过 `SetAIStuff` 限制。

注意：

- `hero.SetHeroAIData(aiData)` 是裸写入，不会执行 `SetAIStuff` 的旧任务结算、互动任务反向绑定、`AttackEnemy` 特殊通知等副作用。
- 如果强制行为是普通状态修改，例如 `Rest`、`CureSelf`、`Free`、指定修炼行为，使用 `SetHeroAIData` 是合适的。
- 如果强制行为是“跨区域移动”，不能只写 `SetHeroAIData(MoveOnBigMap)`，详见第 5 节。

## 5. Patch StartMoveToAnotherArea

本体关系：

```text
StartMoveToAnotherArea(hero, targetID)
  -> new HeroAIData(MoveOnBigMap, targetID.ToString(), 99)
  -> SetAIStuff(hero, moveData, false)
  -> needLeaveHero.Add(hero)
  -> AIController.Update 后续让角色离开区域并创建大地图图标
```

自动调用来源主要有三类：

1. 当前任务的 `bigMapTargetID` 指向别的区域。
2. NPC 离开势力主区域后，按概率回势力主区域。
3. 普通日常 AI 随机游走换区。

Patch 目标：

```csharp
[HarmonyPatch(typeof(AIController), nameof(AIController.StartMoveToAnotherArea))]
public static class AIControllerStartMoveToAnotherAreaPatch
{
    [HarmonyPrefix]
    public static bool Prefix(HeroData hero, int targetID)
    {
        if (!HeroInActiveManager.IsInActive(hero))
            return true;

        LoggerManager.Debug(
            $"inActive: 阻止换区 hero={hero?.heroName}(ID={hero?.heroID}), targetArea={targetID}");

        return false;
    }
}
```

效果：

- `inActive` 角色不再能通过 `StartMoveToAnotherArea` 发起新换区。
- 不会加入 `needLeaveHero`。
- 不会创建新的大地图 NPC 图标。
- 已经在大地图上的角色不会被打断，因为这时 `StartMoveToAnotherArea` 已经执行完毕。
- 如果剧情或 MOD 需要强制移动，不能直接调用 `StartMoveToAnotherArea`，需要提供专门的强制移动实现。

强制跨区域移动建议：

```csharp
public static void ForceStartMoveToAnotherArea(AIController aiController, HeroData hero, int targetID)
{
    if (aiController == null || hero == null)
        return;

    HeroAIData moveData = new HeroAIData(AIStuffType.MoveOnBigMap, targetID.ToString(), 99);

    // 这里不能只调用 hero.SetHeroAIData(moveData) 后结束；
    // 区域内 NPC 要真正进入大地图，还必须加入 needLeaveHero。
    hero.SetHeroAIData(moveData);
    aiController.needLeaveHero.Add(hero);
}
```

说明：

- `StartMoveToAnotherArea` 的关键副作用是 `needLeaveHero.Add(hero)`。
- 只写 `hero.SetHeroAIData(MoveOnBigMap)` 会导致角色 AI 状态像是在移动，但不会进入 `AIController.Update` 的离区队列，可能出现半残状态。
- 因此强制移动要么临时绕过 Patch 调用完整 `StartMoveToAnotherArea`，要么手动补齐 `SetHeroAIData + needLeaveHero.Add(hero)`。

## 6. Patch SetAIStuff

`SetAIStuff` 是本体正常设置 AI 任务的调度入口。

它不仅调用：

```csharp
hero.SetHeroAIData(aiData);
```

还会处理：

- 旧任务 `FinishAIStuff`。
- `AttackEnemy` 特殊通知。
- `setInteractTarget == true` 时强制 `keepWorkingTimeLeft = 99`。
- 互动任务给目标角色设置被动 AI。

本方案会拦截 `inActive` 角色的 `SetAIStuff`，因为 `SetAIStuff` 被定义为“非强制/普通调度入口”。

如果剧情或 MOD 代码需要强制改变角色行为，应直接调用 `hero.SetHeroAIData(aiData)`，或者调用专门的强制行为工具方法。

Patch 规则：

```text
当 hero 为 inActive 时，限制 SetAIStuff。
```

### 6.1 禁止条件

如果满足任一条件，视为非法任务：

```csharp
aiData.aiStuffType == AIStuffType.MoveOnBigMap
aiData.bigMapTargetID >= 0
!IsAllowedAutoAIStuff(aiData.aiStuffType)
```

其中 `bigMapTargetID >= 0` 是额外保险：

- 即使任务类型看起来安全，只要带跨区域目标，就不允许通过普通调度设置。

### 6.2 白名单建议

初版允许：

```text
None
Free
Rest
CureSelf
StudyLivingSkill
MakeMoney
CraftFood
CraftMed
CraftEquip
ReduceBadFame
AddAreaState
```

初版禁止：

```text
StudyFightSkill
StudyNewFightSkill
MoveOnBigMap
Explore
CollectResource
MakeFriend
StudyFight
AttackEnemy
FinishMission
RandomSpeEvent
ReduceAreaState
Prison
Trade
```

说明：

- `Prison` 属于本体特殊状态，通常不建议由 MOD 的 inActive 逻辑主动干预。
- 如果角色已经入狱，应优先尊重本体 `inPrison` 逻辑。
- `Trade` 初版按禁止处理，因为它可能牵涉区域商业行为和目标选择；后续确认不会导致乱跑后再放入白名单。
- 如果后续确认某些行为不会导致角色乱跑，可以逐项加入白名单。

### 6.3 替换策略

非法任务不建议继续调用原 `SetAIStuff`，因为原方法会触发旧任务结算、互动绑定等副作用。

建议直接使用 `hero.SetHeroAIData()` 写入安全 AI：

```csharp
private static HeroAIData CreateSafeAIData(HeroData hero)
{
    if (hero != null && hero.GetTotalInjury() > 50)
        return new HeroAIData(AIStuffType.CureSelf, 99);

    if (hero != null && (hero.GetHpPercent() < 0.8f || hero.GetManaPercent() < 0.8f))
        return new HeroAIData(AIStuffType.Rest, 1);

    return new HeroAIData(AIStuffType.Free, 1);
}
```

Patch 示例：

```csharp
[HarmonyPatch(typeof(AIController), nameof(AIController.SetAIStuff))]
public static class AIControllerSetAIStuffPatch
{
    [HarmonyPrefix]
    public static bool Prefix(HeroData hero, ref HeroAIData aiData, bool setInteractTarget)
    {
        if (!HeroInActiveManager.IsInActive(hero))
            return true;

        if (aiData == null || HeroInActiveManager.IsAllowedAutoAIData(aiData))
            return true;

        HeroAIData safeData = HeroInActiveManager.CreateSafeAIData(hero);
        hero.SetHeroAIData(safeData);

        LoggerManager.Debug(
            $"inActive: 替换AI行为 hero={hero?.heroName}(ID={hero?.heroID}), " +
            $"blocked={aiData.aiStuffType}, safe={safeData.aiStuffType}");

        return false;
    }
}
```

注意：

- `setInteractTarget == true` 的被动互动任务也会进入此 Patch。
- 如果被动目标是 `inActive`，初版也建议限制，避免其他 NPC 的互动把它拉入禁止行为。
- 如果后续希望允许“别人找不活跃 NPC 互动”，可以对 `setInteractTarget == true` 单独放宽。
- 强制行为不要调用 `SetAIStuff`，直接使用 `SetHeroAIData` 或强制移动工具。

## 7. 剧情指令与查询接口

### 7.1 设置指令

```text
SetHeroInActive*角色ID/角色#状态值
```

参数：

| 参数 | 说明 |
| --- | --- |
| `角色ID/角色` | 支持 `player`、`sourceInteractHero`、`targetInteractHero`、heroID、heroName |
| `状态值` | `1` / `true` 表示不活跃；`0` / `false` 表示活跃 |

示例：

```text
SpePlotFuc;SetHeroInActive*小白#1
SpePlotFuc;SetHeroInActive*targetInteractHero#true
SpePlotFuc;SetHeroInActive*1001#0
SpePlotFuc;SetHeroInActive*sourceInteractHero#false
```

设置规则：

- 设置为不活跃时：`PlotEventLogData.Set("HeroData.{heroID}.inActive", "1")`。
- 设置为活跃时：`PlotEventLogData.Set("HeroData.{heroID}.inActive", null)`，直接删除 key。
- 删除 key 后，旧存档和未设置角色都会自然视为活跃。

### 7.2 查询指令

查询直接走已有 `HeroData` 查询类型，新增 `inActive` 查询名：

```text
[$HeroData:inActive$]
[$HeroData:inActive:角色ID/角色$]
```

返回：

```text
1 => 不活跃
0 => 活跃
```

示例：

```text
[$HeroData:inActive$]
[$HeroData:inActive:小白$]
[$HeroData:inActive:targetInteractHero$]
[$HeroData:inActive:1001$]
```

实现建议：

- 在 `Handlers/ConditionQuery/HeroData.cs` 的 `CompositeMethods` 中新增 `inActive`。
- `CompositeInActive` 调用 `HeroInActiveManager.IsInActive(hero)`，返回 `"1"` 或 `"0"`。
- 不再新增独立的 `HeroInActive` 查询类型，避免查询入口分散。

## 8. 推荐文件结构

新增：

```text
TheExtensionOfLong/
  Handlers/
    HeroInActiveManager.cs
    SpePlotFuc/
      SetHeroInActive.cs
  Patches/
    AIController/
      StartMoveToAnotherAreaInActivePatch.cs
      SetAIStuffInActivePatch.cs
```

并在 `TheExtensionOfLong.csproj` 中加入对应 `Compile Include`。

## 9. 验证用例

### 9.1 不活跃角色不再新发起换区

步骤：

1. 选择一个普通 NPC。
2. 执行 `SetHeroInActive*NPC#1`。
3. 观察多日。

预期：

- 不再触发 `StartMoveToAnotherArea`。
- 不进入 `needLeaveHero`。
- 不新建大地图 NPC 图标。

### 9.2 已经在路上的角色继续走完

步骤：

1. 找一个正在大地图移动的 NPC。
2. 执行 `SetHeroInActive*NPC#1`。

预期：

- 当前大地图移动不被打断。
- 到达区域后，后续自动 AI 不再发起新移动。

### 9.3 强制行为仍可改变角色 AI

步骤：

1. 将 NPC 设置为 `inActive=1`。
2. 使用带 `force=true` 的测试指令改变角色行为。

预期：

- 普通行为使用 `hero.SetHeroAIData(aiData)` 后成功写入。
- 强制跨区域移动使用 `ForceStartMoveToAnotherArea` 后成功加入 `needLeaveHero`。
- 直接调用 `StartMoveToAnotherArea` 或 `SetAIStuff` 仍会被 `inActive` Patch 阻止。

### 9.4 非强制非法行为被替换

步骤：

1. 将 NPC 设置为 `inActive=1`。
2. 通过日志观察 `SetAIStuff`。

预期：

- `SetAIStuff` 试图设置 `Explore`、`Trade`、`MakeFriend`、`AttackEnemy` 等行为时，被替换为安全行为。
- 安全行为不带 `bigMapTargetID`。

## 10. 风险与后续调整

1. `SetAIStuff` 是高频核心方法，日志必须使用 `Debug`，避免正式游玩刷屏。

2. 白名单需要保守起步。
   - 初版宁可少放行。
   - 后续通过日志确认哪些行为不会移动，再逐项放开。

3. `Prison` 不建议由 inActive 逻辑接管。
   - 入狱是本体特殊状态，应优先尊重 `hero.inPrison` 与 `ManageAIInPrison`。

4. 如果发现本体在 `ManageAIOneDay` 内直接调用 `hero.SetHeroAIData` 设置禁止行为，需要额外 Patch 或在 `ManageAIOneDay` Postfix 做一次状态修正。
   - 已知本体在入狱分支会直接 `hero.SetHeroAIData(Prison)`。
   - 初版不处理该分支。

5. 强制行为绕过 `SetAIStuff`，也会绕过 `SetAIStuff` 的本体副作用。
   - 互动任务不会自动给目标角色设置被动 AI。
   - `AttackEnemy` 不会触发 `SetAIStuff` 内的特殊通知逻辑。
   - 需要这些副作用时，应在强制指令中手动补齐。

6. 如果未来希望 `inActive` 角色只做更少行为，可以把白名单缩到：

```text
None
Free
Rest
CureSelf
```

这是最强限制版本。

## 11. 一句话结论

`inActive` 是“自动 AI 行为限制”，不是“角色冻结”。

实现上用 `PlotEventLogData` 保存 `HeroData.{heroID}.inActive`，通过 `StartMoveToAnotherArea` 阻止新换区，通过 `SetAIStuff` 过滤非强制 AI 行为；后续改变角色行为的指令用 `force` 参数决定是否绕过限制，强制行为直接使用 `hero.SetHeroAIData()` 或专门的强制移动工具。
