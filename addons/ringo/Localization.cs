#if TOOLS
using System.Collections.Generic;
using Godot;

namespace Ringo;

/// <summary>
/// Minimal bilingual (English / Simplified Chinese) localization helper for the
/// ringo editor plugin. Keys are the English source strings; when the editor
/// language starts with "zh" the Chinese translation is returned instead.
/// The editor language is read on every call so no restart of the plugin is
/// required after the editor itself applies a language change.
/// </summary>
public static class L10n
{
    private static readonly Dictionary<string, string> Zh = new()
    {
        // Menu
        { "Import by Loop Time", "按照循环时间导入" },
        { "Import by Loop Measure", "按照循环小节位置导入" },

        // Common dialog UI
        { "Audio Resource:", "音频资源：" },
        { "OK", "确定" },
        { "Cancel", "取消" },
        { "Success", "成功" },
        { "Error", "错误" },

        // Loop mode
        { "Loop Mode:", "循环模式：" },
        { "Detect From Cue Points", "从 Cue 点检测" },
        { "Disabled", "禁用" },
        { "Forward", "正向" },
        { "Ping-Pong", "乒乓" },
        { "Backward", "反向" },

        // Time dialog
        { "Specify Loop Start Time (s):", "指定循环开始时间（秒）：" },
        { "Specify Loop End Time (s):", "指定循环结束时间（秒）：" },

        // Measure dialog
        { "BPM:", "BPM：" },
        { "Time Signature:", "拍号：" },
        { "Beats per Measure:", "每小节拍数：" },
        { "Beat Note Value:", "拍子音符：" },
        { "Specify Loop Start Measure:", "指定循环开始小节：" },
        { "Specify Loop End Measure:", "指定循环结束小节：" },

        // Info / status messages
        { "Please select an audio resource to preview the loop samples.", "请选择音频资源以预览循环采样数。" },
        { "Please select an audio resource.", "请选择一个音频资源。" },
        { "Unsupported audio format (supported: WAV, OGG, MP3):", "不支持的音频格式（支持 WAV、OGG、MP3）：" },
        { "Could not determine the sample rate of the file.", "无法确定文件的采样率。" },
        { "This format does not support a loop end point; only the loop start was applied.", "该格式不支持循环结束点，仅应用了循环开始位置。" },
        { "This format only supports a simple forward loop.", "该格式仅支持简单的正向循环。" },
        { "Loop end must be after loop start.", "循环结束位置必须晚于循环开始位置。" },
        { "BPM must be greater than 0.", "BPM 必须大于 0。" },
        { "Sample Rate:", "采样率：" },
        { "Loop Start:", "循环开始：" },
        { "Loop End:", "循环结束：" },
        { "samples", "采样" },
        { "end of file", "文件末尾" },
        { "Import settings updated, file reimported:", "导入配置已更新并重新导入：" },
        { "Could not read the .import file:", "无法读取 .import 文件：" },
        { "Could not save the .import file:", "无法保存 .import 文件：" },
    };

    /// <summary>True when the editor UI language is a Chinese variant.</summary>
    public static bool IsChinese()
    {
        var lang = EditorInterface.Singleton
            .GetEditorSettings()
            .GetSetting("interface/editor/editor_language")
            .AsString();
        return lang.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Translate an English source string according to the editor language.</summary>
    public static string Tr(string english)
    {
        if (!IsChinese())
            return english;
        return Zh.TryGetValue(english, out var zh) ? zh : english;
    }
}
#endif
