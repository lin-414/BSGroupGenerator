# BS Group Generator

读取 **Mod Organizer 2 (MO2)** 安装的模组，让用户把服装模组批量划进 **BodySlide 分组（SliderGroups）** 的 Windows 独立小工具。

BodySlide 自带的 Group Manager 只有一个服装平铺列表，不知道哪个服装来自哪个模组；本工具补上这一环：**按模组勾选，一键整组归组**，也能展开后逐个服装微调。

## 使用方法

1. 启动后自动发现 MO2 实例（全局实例 + 便携安装）。找不到就用「添加 MO2 目录…」选择含 `ModOrganizer.ini` 的实例目录或含 `ModOrganizer.exe` 的便携安装目录。
2. 选择配置（Profile）——工具解析该 profile 的 `modlist.txt`，列出所有**启用**的模组。
3. BodySlide 通常自动检测成功（在启用模组中寻找含 `BodySlide*.exe` 与 `Config.xml` 的目录）。界面会显示「有效项目路径」——即 BodySlide 实际读取服装/分组的位置，与 BodySlide 自己的 `ProjectUtil::GetProjectPath()` 逻辑完全一致。
4. 在右侧**新建组**并选中它，然后在左侧**勾选**模组（或展开勾选单个服装；**勾选分隔符 = 全选其下所有模组**），再点右侧的 **「✔ 加入当前组」**——这一步才真正把服装写进组；勾错的用「移出当前组」撤销。
   - 已在当前组里的服装显示为**绿色 ✔**，模组标题会附 `[组内 x/总数]`。
   - 模组树遵循 **MO2 左侧栏的顺序**，只显示含 BodySlide 服装的启用模组；你的分隔符显示为灰色分组标题（`_separator` 后缀自动去掉），方便在 MO2 的结构里对照定位。
   - 「规则归组」：按包含/排除关键字（分号分隔，不区分大小写）批量把服装加入或移出某个组；可同时匹配所属模组名、可限定仅未分配服装，实时预览命中数量与样例。
   - 「撤销」（Ctrl+Z）：最近 30 步分组操作可逐步回退，误点不慌。
   - 「查看组」（或双击组名）：预览该组的全部服装，可按名称过滤、可勾选批量移出。
   - 「仅看未分配」：只显示还没进任何组的服装。
   - 「过滤服装名」：按名称实时筛选（连续子串匹配，命中时自动展开并显示"匹配 x/总数"）。
   - 「导入现有组文件…」：把已有分组 XML 合并进来继续编辑。
5. 点击**保存分组文件**（或 Ctrl+S）写出。完成后重启 BodySlide（通过 MO2 启动的话建议连 MO2 一起重启），分组下拉里即可看到新组。

## 写到哪里

保存时**每个组生成一个文件，文件名即组名**（如 `UBE.xml`、`护甲.xml`）——在 BodySlide 的 SliderGroups 目录里一眼看出哪个文件是哪个组。绝不改动其他分组文件；重复保存会覆盖同名组文件，并通过清单自动清理改名/删除组留下的旧文件。输出位置有五种模式：

| 模式 | 位置 | 适用 |
|---|---|---|
| 自动（推荐） | 按下述规则 | 绝大多数情况 |
| BodySlide 程序目录 | `<BodySlide 目录>\SliderGroups\` | BodySlide 目录旁有 SliderSets 的安装 |
| MO2 专用模组 | `<MO2 mods>\BS Group Generator\CalienteTools\BodySlide\SliderGroups\` | 通过 MO2 启动 BodySlide（最干净：可按 profile 开关、重装 BodySlide 不丢） |
| 游戏真实 Data | `<游戏 Data>\CalienteTools\BodySlide\SliderGroups\` | 不经 MO2 启动 BodySlide 时 |
| 自定义（浏览选择） | 「浏览…」选择的任意路径 | 想自己管理文件；注意确认 BodySlide 能读到该位置 |

「自动」的规则：BodySlide 的有效项目路径若是真实目录（如 BodySlide 程序目录），直接写它的 `SliderGroups`；若是 MO2 虚拟 Data 下的 `CalienteTools\BodySlide`（最常见），则写入 MO2 专用模组（无 MO2 时退回游戏真实 Data）。

## 它是如何知道 BodySlide 会显示哪些服装的

工具复刻了 BodySlide（v5.8.2 / dev 分支）源码里的两段关键逻辑：

- **有效项目路径**：`Config.xml` 的 `ProjectPath` → 若 `BodySlide.exe` 旁存在 `SliderSets` 目录则用 exe 目录 → `<GameDataPath>\CalienteTools\BodySlide` → `<GameDataPath>\Tools\BodySlide`。
- **服装清单**：`<有效项目路径>\SliderSets\*.xml|*.osp` 中的 `<SliderSet name="…">` 名称，逐字符原样使用（BodySlide 的成员匹配是大小写敏感的精确比较）。通过 MO2 启动时该目录是虚拟 Data 的汇聚点，工具按 profile 的模组优先级**模拟 USVFS 覆盖**（同名相对路径文件由更强的模组获胜），因此列出的服装与 BodySlide 启动后看到的完全一致。

同名服装出现在多个模组（同名冲突）时，归属给优先级最高的模组并在树里以蓝色标注；这不影响分组的正确性（BodySlide 的组成员本来就是按名称匹配的）。

## 常见问题

**MO2 正在运行时能用吗？** 能。工具读取的是磁盘上的 `ModOrganizer.ini` / `modlist.txt`（MO2 退出时才回写设置，运行中改动可能略有滞后）；写出后需重启 BodySlide，必要时重启 MO2 以刷新虚拟文件系统。

**生成的组在 BodySlide 里看不到？** 1) 确认重启了 BodySlide/MO2；2) 在工具「工具 → 诊断信息」里检查「有效项目路径」和「写出目标」是否对应同一个目录——BodySlide 只从有效项目路径的 `SliderGroups` 读分组。

**支持哪些游戏？** 全部——分组机制与游戏无关（天际 SE/AE、辐射4 等都适用），跟随 MO2 实例与 BodySlide 安装自动适配。

**组和成员可以重名/大小写不同吗？** 组名在工具内忽略大小写（避免混乱）；成员名严格保留原样，与 BodySlide 的精确匹配行为一致。

## 开发

```
dotnet build            # 或打开 src/BSGroupGenerator/BSGroupGenerator.csproj
dotnet test tests/BSGroupGenerator.Tests
publish.ps1 -SelfContained   # 生成 dist\BSGroupGenerator.exe（单文件，已打包运行时，用户无需安装任何依赖）
```

- 技术栈：C# / .NET 10 WinForms，无第三方依赖；发布采用自包含单文件。
- 测试覆盖：modlist.txt 与 ModOrganizer.ini 解析、GetProjectPath 复刻、VFS 覆盖扫描、分组文件读写（UTF-8 BOM）。
- 分组 XML 格式依据 ousnius/BodySlide-and-Outfit-Studio 的源码行为逆向确认（`SliderGroup.cpp` / `BodySlideApp.cpp` / `ProjectUtil.cpp`），未复制其代码。
