# 龙之书

《龙胤立志传》的 MelonLoader 数据 Mod 框架。

龙之书的目标是让玩家和 Mod 作者不直接修改游戏资源，也能查看、扩展和覆盖游戏数据。当前主要支持：

- 导出游戏原始配置表与场景内复杂数据。
- 从 `ModsOfLong` 加载数据 Mod。
- 对 CSV 配置表做增量补丁。
- 对任务、世界剧情触发器等复杂 JSON 数据做补丁。
- 用 `modXXX` 这类符号 ID 避免新增内容的数字 ID 冲突。

## 安装

前提：

1. 已安装《龙胤立志传》。
2. 已安装 MelonLoader。
3. 已获得或编译出 `TheBookOfLong.dll`。

安装方式：

1. 将 `TheBookOfLong.dll` 放入游戏目录的 `Mods` 文件夹。
2. 启动游戏。
3. 首次启动后，龙之书会自动创建导出目录和数据 Mod 目录。

常用目录：

```text
<游戏目录>\Mods\TheBookOfLong.dll
<游戏目录>\Mods\ModsOfLong
<游戏目录>\DataDump\Latest
<游戏目录>\UserData\TheBookOfLong.ModLoadConfig.json
```

## 使用

- 进游戏后会自动弹出一次配置界面。
- 默认按 `F4` 打开或关闭配置界面。
- 导出的游戏数据位于 `<游戏目录>\DataDump\Latest`。
- 数据 Mod 放在 `<游戏目录>\Mods\ModsOfLong` 下，每个以 `mod` 开头的文件夹会被识别为一个数据 Mod。
- Mod 的启用状态和加载顺序由 `<游戏目录>\UserData\TheBookOfLong.ModLoadConfig.json` 控制，修改后需要重启游戏。

一个最小数据 Mod 目录示例：

```text
ModsOfLong
└─ modMyFirstMod
   ├─ Info.json
   ├─ Data
   │  └─ PlotData.csv
   └─ ComplexData
      └─ WorldPlotEventController_WorldPlotEventDataBase.json
```

`Info.json` 可选：

```json
{
  "Name": "我的第一个数据 Mod",
  "Version": "1.0.0"
}
```

## 文档

- [快速入门](Doc/快速入门.md)：从导出数据开始，快速做出第一个数据 Mod。
- [完整功能说明](Doc/完整功能说明.md)：按运行顺序说明当前全部功能、目录、补丁规则和符号 ID 规则。
- [开发者调研指南](Doc/开发者调研指南.md)：给继续开发龙之书的人看，说明代码入口、调研路径、构建验证和常用搜索方向。
- [剧情文件结构](Doc/剧情文件结构.md)：任务、世界剧情触发器、剧情内容结构的详细整理。

## 从源码构建

确认 `Directory.Build.props` 中的游戏路径符合本机安装位置，然后执行：

```powershell
dotnet build D:\codes\LongYin\TheBookOfLong\TheBookOfLong.sln
```

构建完成后，项目会自动把 `TheBookOfLong.dll` 和 `TheBookOfLong.pdb` 复制到游戏 `Mods` 目录。
