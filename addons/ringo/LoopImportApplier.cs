#if TOOLS
using Godot;

namespace Ringo;

/// <summary>Current loop-related import settings of an audio file.</summary>
public sealed class ImportLoopSettings
{
    /// <summary>True when the .import file could be read.</summary>
    public bool Found;

    /// <summary>True for WAV (sample-based loop), false for OGG/MP3 (offset-based).</summary>
    public bool IsWav;

    // WAV: edit/loop_mode (0=None, 1=Forward, 2=Ping-Pong, 3=Backward).
    public int LoopMode;
    // WAV: edit/loop_begin / edit/loop_end in samples (-1 = end of file).
    public long LoopBegin;
    public long LoopEnd = -1;

    // OGG/MP3: loop on/off and loop_offset in seconds.
    public bool Loop;
    public double LoopOffset;
}

/// <summary>
/// Reads and writes loop options in an audio resource's .import file and
/// triggers a reimport. WAV supports sample-exact loop begin/end with a loop
/// mode; OGG Vorbis and MP3 importers only support a loop start offset in
/// seconds (no loop end, forward only).
/// </summary>
public static class LoopImportApplier
{
    /// <summary>Read the current loop import settings of the given audio file.</summary>
    public static ImportLoopSettings ReadSettings(string resourcePath)
    {
        var settings = new ImportLoopSettings();
        if (string.IsNullOrEmpty(resourcePath))
            return settings;

        var config = new ConfigFile();
        if (config.Load(resourcePath + ".import") != Error.Ok)
            return settings;

        settings.Found = true;
        settings.IsWav = resourcePath.GetExtension().ToLowerInvariant() == "wav";
        if (settings.IsWav)
        {
            settings.LoopMode = (int)config.GetValue("params", "edit/loop_mode", 0L).AsInt64();
            settings.LoopBegin = config.GetValue("params", "edit/loop_begin", 0L).AsInt64();
            settings.LoopEnd = config.GetValue("params", "edit/loop_end", -1L).AsInt64();
        }
        else
        {
            settings.Loop = config.GetValue("params", "loop", false).AsBool();
            settings.LoopOffset = config.GetValue("params", "loop_offset", 0.0).AsDouble();
        }
        return settings;
    }

    /// <summary>
    /// Update the import configuration of the audio file at <paramref name="resourcePath"/>.
    /// </summary>
    /// <param name="resourcePath">res:// path of the audio file.</param>
    /// <param name="beginSec">Loop start in seconds.</param>
    /// <param name="endSec">Loop end in seconds, or -1 for the file end.</param>
    /// <param name="sampleRate">Sample rate of the file (required for WAV).</param>
    /// <param name="loopMode">WAV edit/loop_mode option value: 0=Detect From
    /// Cue Points, 1=Disabled, 2=Forward, 3=Ping-Pong, 4=Backward (shifted by
    /// one vs AudioStreamWav.LoopMode). For OGG/MP3 only 1 disables looping,
    /// anything else enables a simple forward loop.</param>
    /// <param name="note">Receives a non-fatal localized note (e.g. loop end unsupported).</param>
    /// <returns>null on success, otherwise a localized error message.</returns>
    public static string Apply(
        string resourcePath, double beginSec, double endSec, int? sampleRate,
        int loopMode, out string note)
    {
        note = null;
        if (string.IsNullOrEmpty(resourcePath))
            return L10n.Tr("Please select an audio resource.");

        string ext = resourcePath.GetExtension().ToLowerInvariant();
        bool isWav = ext == "wav";
        bool isOffsetFormat = ext == "ogg" || ext == "mp3";
        if (!isWav && !isOffsetFormat)
            return L10n.Tr("Unsupported audio format (supported: WAV, OGG, MP3):") + " " + resourcePath;

        if (isWav && !sampleRate.HasValue)
            return L10n.Tr("Could not determine the sample rate of the file.");

        var importPath = resourcePath + ".import";
        var config = new ConfigFile();
        var err = config.Load(importPath);
        if (err != Error.Ok)
            return L10n.Tr("Could not read the .import file:") + " " + importPath;

        if (isWav)
        {
            long loopBegin = (long)System.Math.Round(beginSec * sampleRate.Value);
            long loopEnd = endSec < 0 ? -1 : (long)System.Math.Round(endSec * sampleRate.Value);
            config.SetValue("params", "edit/loop_mode", (long)loopMode);
            config.SetValue("params", "edit/loop_begin", loopBegin);
            config.SetValue("params", "edit/loop_end", loopEnd);
        }
        else
        {
            config.SetValue("params", "loop", loopMode != 1);
            config.SetValue("params", "loop_offset", System.Math.Round(beginSec, 6));
            if (loopMode > 2)
                note = L10n.Tr("This format only supports a simple forward loop.");
            if (endSec >= 0)
            {
                string endNote = L10n.Tr("This format does not support a loop end point; only the loop start was applied.");
                note = note == null ? endNote : note + " " + endNote;
            }
        }

        err = config.Save(importPath);
        if (err != Error.Ok)
            return L10n.Tr("Could not save the .import file:") + " " + importPath;

        // EditorInterface.Singleton is only available in the editor context;
        // guard it so this stays callable from headless test scripts.
        try
        {
            EditorInterface.Singleton?.GetResourceFilesystem().ReimportFiles(new[] { resourcePath });
        }
        catch (System.Exception)
        {
            // Non-editor context: skip reimport, the .import file is already updated.
        }
        return null;
    }
}
#endif
