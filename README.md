# ringo

Godot .NET editor plugin for setting audio loop points (WAV / OGG / MP3).

## 功能 / Features

在 **项目 > 工具 > ringo** 菜单下提供两个窗口（界面随编辑器语言自动切换中英双语）：

Under **Project > Tools > ringo** (UI follows the editor language, English / 中文):

- **按照循环时间导入 / Import by Loop Time** — 选择音频资源，用复选框决定是否指定循环开始/结束时间（秒），按音频采样率换算为循环采样数，确定后写入导入配置并重新导入。
  Pick an audio resource, optionally specify loop start/end times in seconds (checkboxes), convert to sample counts via the file's sample rate, then update the import configuration and reimport.
- **按照循环小节位置导入 / Import by Loop Measure** — 同上，但以乐曲拍号与 BPM 指定循环开始/结束小节（音符仅支持 2、4、8、16、32；每小节拍数为任意大于 2 的数）。循环结束点为结束小节的开头位置。
  Same idea, but loop points are given as 1-based measure numbers using a BPM and a time signature (note values 2/4/8/16/32; beats per measure must be greater than 2). The loop end is the start position of the end measure.

两个窗口都会实时显示采样率与换算结果。按格式写入不同的导入参数：

- **WAV**：`edit/loop_mode`（0=从 Cue 点检测、1=禁用、2=正向、3=乒乓、4=反向）、`edit/loop_begin`、`edit/loop_end`（采样数，-1 表示文件末尾）；
- **OGG / MP3**：`loop=true`、`loop_offset`（秒）。这两种格式的导入器只支持循环开始，不支持循环结束点；OGG/MP3 的采样率从文件头解析（用于采样数预览）。

Both dialogs live-preview the sample rate and computed loop points. Import options written per format:

- **WAV**: `edit/loop_mode` (0=Detect From Cue Points, 1=Disabled, 2=Forward, 3=Ping-Pong, 4=Backward), `edit/loop_begin`, `edit/loop_end` (samples; -1 = end of file);
- **OGG / MP3**: `loop=true`, `loop_offset` (seconds). These importers only support a loop start; the sample rate is probed from the file header for the sample-count preview.

## 使用 / Usage

1. 用 Godot 4.7 (.NET) 打开本工程，编辑器会自动构建 C# 程序集。
   Open this project in Godot 4.7 (.NET); the editor builds the C# assembly automatically.
2. 插件已启用（见 `project.godot` 的 `[editor_plugins]`）；若未生效，在 **项目 > 项目设置 > 插件** 中启用 **ringo**。
   The plugin is enabled via `[editor_plugins]` in `project.godot`; otherwise enable **ringo** under **Project > Project Settings > Plugins**.
3. 打开 **项目 > 工具 > ringo**，选择需要的导入方式。
   Open **Project > Tools > ringo** and pick an import mode.

## 测试与 CI / Testing & CI

回归测试（`tests/LoopImportRegressionTest.cs`）在编辑器内验证完整链路：小节→秒换算（4/4、120 BPM 素材：第 5 小节 = 8s）、按时间与按小节应用循环（第 5–9 小节 = 384000–768000 采样）、引擎资源实际生效（轮询确认），最后把素材恢复为原状（Detect From Cue Points）并再次校验。

The regression test (`tests/LoopImportRegressionTest.cs`) verifies the whole chain inside the editor: measure-to-seconds math, time-based and measure-based applies, engine-side application (polled), then restores the asset to its original state and verifies the restore.

本地运行 / Run locally:

```powershell
$env:RINGO_RUN_TESTS = "1"
& "C:\Program Files\Godot\Godot_v4.7-stable_mono_win64_console.exe" --headless -e --path .
# 结果见 test_result.txt（RINGO_TEST_RESULT: PASS/FAIL），退出码 0=通过。
```

GitHub Actions 工作流见 `.github/workflows/regression-test.yml`（push / PR 时自动运行：下载 Godot 4.7 .NET → dotnet build → 导入资源 → 跑回归测试）。
The GitHub Actions workflow is `.github/workflows/regression-test.yml` (runs on push/PR: downloads Godot 4.7 .NET, builds, imports, runs the test).
