#if TOOLS
using Godot;

namespace Ringo;

/// <summary>
/// "Import by Loop Time" dialog: pick an audio file, optionally specify loop start
/// and/or end times in seconds; the loop sample counts are derived from the
/// file's sample rate and written into its import configuration on OK.
/// </summary>
[Tool]
public partial class LoopTimeImportDialog : LoopImportDialogBase
{
    private CheckBox _useStart;
    private SpinBox _startTime;
    private CheckBox _useEnd;
    private SpinBox _endTime;

    public LoopTimeImportDialog() : base("Import by Loop Time")
    {
        (_useStart, _startTime) = AddOptionalValueRow(
            "Specify Loop Start Time (s):", 0.0, 86400.0, 0.001, 0.0);
        (_useEnd, _endTime) = AddOptionalValueRow(
            "Specify Loop End Time (s):", 0.0, 86400.0, 0.001, 1.0);
    }

    protected override bool TryGetLoopTimes(out double beginSec, out double endSec, out string errorKey)
    {
        errorKey = null;
        beginSec = _useStart.ButtonPressed ? _startTime.Value : 0.0;
        endSec = _useEnd.ButtonPressed ? _endTime.Value : -1.0;
        return true;
    }

    protected override void PopulateLoopPoints(ImportLoopSettings settings, int? sampleRate)
    {
        if (settings.IsWav)
        {
            // WAV stores loop points in samples; convert back to seconds.
            if (!sampleRate.HasValue)
                return;
            if (settings.LoopBegin > 0)
            {
                _useStart.ButtonPressed = true;
                _startTime.Value = settings.LoopBegin / (double)sampleRate.Value;
            }
            if (settings.LoopEnd >= 0)
            {
                _useEnd.ButtonPressed = true;
                _endTime.Value = settings.LoopEnd / (double)sampleRate.Value;
            }
        }
        else if (settings.Loop && settings.LoopOffset > 0.0)
        {
            // OGG/MP3 only have a loop start offset in seconds (no loop end).
            _useStart.ButtonPressed = true;
            _startTime.Value = settings.LoopOffset;
        }
    }
}
#endif
