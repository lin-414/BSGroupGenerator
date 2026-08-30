BS Group Generator 首个公开发布版本。

读取 Mod Organizer 2 安装的模组，把服装（BodySlide 滑块组）批量划进分组，生成 BodySlide 原生格式的分组文件（SliderGroups）。

## 功能

- 自动发现 MO2 实例与 Profile，自动定位 BodySlide（含手动指定兜底）
- 按 MO2 左侧栏顺序的树形勾选归组：分隔符分组、服装/模组名过滤、仅看未分配
- 规则归组：按包含/排除关键字批量加入或移出（支持匹配所属模组名、仅未分配、实时预览命中）
- 每个组一个以组名命名的独立文件（BodySlide 原生格式，自动清理旧文件）
- 五种输出位置：自动 / BodySlide 程序目录 / MO2 专用模组 / 游戏真实 Data / 自定义目录
- 查看组成员（树形预览、批量移出）、导入现有分组文件
- 未保存退出拦截、30 步撤销（Ctrl+Z）、程序内详细使用说明（F1）

## 要求

- Windows 10 1809 或更高版本
- 已安装 BodySlide and Outfit Studio（配合 MO2 使用）
- 无需安装任何运行时——单文件，下载后双击即用

## 说明

- 下载 BSGroupGenerator.exe 放到任意目录运行即可；建议在 MO2 的可执行文件列表里注册，方便从 MO2 直接启动
- 程序内按 F1 查看完整使用说明
- 作者：lin-414　·　仓库：https://github.com/lin-414/BSGroupGenerator
