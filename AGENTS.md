# AGENTS.md

本文件面向在本仓库中工作的 AI 编码代理，说明项目结构、构建方式与代码约定。

## 项目概述

**ringo** 是一个 Godot 4.7 (.NET) 编辑器插件，用于为音频（WAV / OGG / MP3）设置循环点。
它在「项目 > 工具 > ringo」菜单下提供两个双语（中/英）对话框：

- **按照循环时间导入**：按秒指定循环开始/结束时间；
- **按照循环小节位置导入**：按 BPM + 拍号指定循环开始/结束小节（1 起始，结束点为结束小节开头）。

WAV 按音频采样率把时间换算为采样数，写入 `.import` 的 `edit/loop_mode`（0=检测Cue点、1=禁用、2=Forward、3=乒乓、4=反向）、`edit/loop_begin`、`edit/loop_end`（-1 表示文件末尾）；OGG/MP3 导入器只支持秒级循环开始，写入 `loop=true` 与 `loop_offset`（秒），不支持循环结束点（应用时给出提示）。最后调用 `EditorFileSystem.ReimportFiles()` 重新导入。OGG/MP3 的采样率由 `AudioSampleRateProbe` 解析文件头获得（其 AudioStream 不暴露采样率）。

## 技术栈与环境

- Godot **4.7** .NET 版，C# / `net8.0`，SDK 为 `Godot.NET.Sdk/4.7.0`（见 `ringo.csproj`）。
- 渲染：Forward+，Windows 驱动 d3d12；物理：Jolt。插件本身不依赖这些设置。
- 工程通过 `project.godot` 的 `[editor_plugins]` 默认启用插件。

## 仓库结构

```
project.godot          # 工程配置（含 [editor_plugins] 启用项）
ringo.csproj / ringo.sln
addons/ringo/
  plugin.cfg               # 插件清单，script 指向 RingoPlugin.cs
  RingoPlugin.cs           # EditorPlugin：添加 ringo 工具子菜单与两个菜单项
  Localization.cs          # L10n：英->中字典 + Tr()，跟随编辑器语言
  LoopImportDialogBase.cs  # 两个对话框的共用基类（资源选择、采样预览、确认应用）
  LoopTimeImportDialog.cs  # 按循环时间导入窗口
  LoopMeasureImportDialog.cs # 按循环小节位置导入窗口（BPM、拍号）
  LoopImportApplier.cs     # 写 .import 参数并触发重新导入（按扩展名分支）
  AudioSampleRateProbe.cs  # 从 OGG/MP3 文件头解析采样率
  LoopMath.cs              # 小节→秒换算（对话框与回归测试共用）
tests/
  LoopImportRegressionTest.cs # 回归测试：RINGO_RUN_TESTS=1 时在编辑器内运行
.github/workflows/
  regression-test.yml      # GitHub CI：构建 + 导入 + 跑回归测试
```

## 构建与验证

- 编译验证：`dotnet build ringo.csproj -c Debug`（要求 0 错误 0 警告）。
- 回归测试：`RINGO_RUN_TESTS=1` 启动编辑器（**必须 --headless**，非 headless 在本机连续重新导入时会崩溃）：
  `& godot_console.exe --headless -e --path .`，结果写入 `test_result.txt`（`RINGO_TEST_RESULT: PASS/FAIL`，退出码 0/1）。
  测试会修改并恢复 `little star demo.wav` 的导入配置（恢复原状依赖引擎轮询，约 20s）。CI 见 `.github/workflows/regression-test.yml`。
- 手动功能验证：打开工程 → 项目 > 工具 > ringo → 选择菜单项 → 选 WAV 文件观察采样数预览与重新导入结果。
- `.godot/` 已被 `.gitignore` 忽略，不要提交；Godot 自动生成的 `*.cs.uid` 文件应保留并提交。
- 注意：若 Godot 编辑器正在运行，`project.godot` 可能被编辑器重写；编辑前务必重新读取文件。

## 代码约定

- 所有插件代码用 `#if TOOLS` 包裹（避免导出构建失败），类标记 `[Tool]`，命名空间统一为 `Ringo`。
- UI 文本一律以**英文原句为键**调用 `L10n.Tr()`；新增可见文本时必须同步在 `Localization.cs` 的 `Zh` 字典中添加中文翻译。
- 新对话框继承 `LoopImportDialogBase`：用 `AddOptionalValueRow`（复选框+SpinBox，勾选才可编辑）或 `AddLabeledRow` 添加输入行，实现 `TryGetLoopTimes(out beginSec, out endSec, out errorKey)`（endSec = -1 表示文件末尾；errorKey 须为可翻译的英文键）。采样换算、结束>开始校验、导入应用由基类完成。
- 音频选择器 BaseType 为 `AudioStream`（WAV/OGG/MP3）。WAV 写采样级 `edit/loop_mode` / `edit/loop_begin` / `edit/loop_end`；OGG/MP3 只写 `loop` / `loop_offset`（秒，仅循环开始）。新增格式支持时必须同步修改 `LoopImportApplier.Apply` 的分支与文档。
- 导入参数写入 `<资源路径>.import` 的 `[params]` 段。
- 对话框必须挂到 `EditorInterface.Singleton.GetBaseControl()` 下，不要挂到 `EditorPlugin` 节点上，否则弹窗无法正常交互/关闭。

## Godot 4.7 API 注意事项（已踩过的坑）

- `EditorPlugin.RemoveToolSubmenuItem` 已从 4.7 的 C# 绑定移除；插件卸载清理用 `RemoveToolMenuItem("ringo")`（4.5 起官方文档即如此说明）。
- `AddToolSubmenuItem(name, submenu)` 要求传入的 `PopupMenu` **没有父节点**（编辑器会自行挂到 Tools 菜单下）；提前 `AddChild` 会触发 `editor_node.cpp: Condition "p_submenu->get_parent() != nullptr" is true` 错误导致菜单不出现。
- `Control.SizeFlags` 枚举在对象初始化器等位置必须写全限定名 `Control.SizeFlags.ExpandFill`，否则 CS0103。
- 编辑器语言读取：`EditorInterface.Singleton.GetEditorSettings().GetSetting("interface/editor/editor_language")`。
- 插件弹窗必须 `Exclusive = false`：编辑器自身弹窗（如 Quick Open）已是 exclusive 子窗口，否则报 `window.cpp: parent window already has another exclusive child`。
- `ConfirmationDialog` 按确定后自动隐藏；要保持打开需在 `Confirmed` 里 `CallDeferred(Window.MethodName.Show)`。结果与计算详情用 `GD.Print`/`GD.PrintErr` 输出到编辑器「输出」面板，对话框内不放内联信息区（自动换行的中文 Label 会把窗口最小高度撑到全屏），不弹窗。
- 无头验证：`--quit-after N` 的 N 是**帧数**不是秒数；插件里 GD.Print 不一定到 stdout，可用 `FileAccess` 写 `res://` 文件做日志；`--script` 跑 C# SceneTree 在无头模式会崩溃（signal 11），改用环境变量触发插件内自检。
- 选中资源后用 `LoopImportApplier.ReadSettings()` 读取现有导入配置回填对话框（含循环模式），应用时写用户选择的循环模式，不得硬编码覆盖。
- **关键**：`.import` 的 WAV `edit/loop_mode` 枚举是 0=Detect From Cue Points、1=Disabled、2=Forward、3=Ping-Pong、4=Backward（见 resource_importer_wav.cpp，`< 2` 时隐藏 begin/end），比 `AudioStreamWav.LoopMode`（0=Disabled..3=Backward）**偏移 1**；UI 下拉框必须使用前者，混淆会导致引擎显示的模式总比设置值“小一档”。
- 编辑器内自动化测试的坑：编辑器启动时 C# 热重载会把同一定时器信号投递给新旧两个程序集（间隔毫秒级），测试主体必须用**文件系统标记**做幂等去重（静态标志跨不了 AssemblyLoadContext）；`ReimportFiles` 是异步的，校验引擎侧状态要延迟轮询；WAV 导入器仅在 `edit/loop_mode >= 2`（启用循环）时才应用 loop_begin/loop_end 到引擎资源。
- 回填防踩踏：`ReimportFiles` 会异步再次触发 `EditorResourcePicker.ResourceChanged`，回填必须按资源路径去重（`PopulateFromCurrentSettings(force: false)`），否则会覆盖用户未确认的下拉框修改（表现为“确认写入的是上一次的选择”）；对话框每次打开时（`VisibilityChanged`）再强制回填一次以反映外部改动。
