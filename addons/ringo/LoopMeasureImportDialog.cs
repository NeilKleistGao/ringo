#if TOOLS
using Godot;

namespace Ringo;

/// <summary>
/// "Import by Loop Measure" dialog: like the time-based dialog, but the loop
/// points are given as measure numbers. Requires the song tempo (BPM) and time
/// signature so measures can be converted to seconds, then to samples via the
/// file's sample rate. The loop end is the start position of the end measure.
/// </summary>
[Tool]
public partial class LoopMeasureImportDialog : LoopImportDialogBase
{
    private static readonly int[] NoteValues = { 2, 4, 8, 16, 32 };

    private SpinBox _bpm;
    private SpinBox _beatsPerMeasure;
    private OptionButton _noteValue;
    private CheckBox _useStart;
    private SpinBox _startMeasure;
    private CheckBox _useEnd;
    private SpinBox _endMeasure;

    public LoopMeasureImportDialog() : base("Import by Loop Measure")
    {
        // Tempo.
        _bpm = new SpinBox { MinValue = 20.0, MaxValue = 600.0, Step = 0.1, Value = 120.0, AllowGreater = true };
        AddLabeledRow("BPM:", _bpm);

        // Time signature: beats per measure / note value.
        var signatureRow = new HBoxContainer();
        signatureRow.AddThemeConstantOverride("separation", 4);
        // Per spec, beats per measure may be any number greater than 2.
        _beatsPerMeasure = new SpinBox { MinValue = 3, MaxValue = 64, Step = 1, Value = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _noteValue = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (int value in NoteValues)
            _noteValue.AddItem(value.ToString());
        _noteValue.Select(1); // Default to quarter notes (4).
        signatureRow.AddChild(_beatsPerMeasure);
        signatureRow.AddChild(new Label { Text = "/", VerticalAlignment = VerticalAlignment.Center });
        signatureRow.AddChild(_noteValue);
        AddLabeledRow("Time Signature:", signatureRow);

        // Loop points as measure numbers (1-based).
        (_useStart, _startMeasure) = AddOptionalValueRow(
            "Specify Loop Start Measure:", 1.0, 999999.0, 1.0, 1.0);
        (_useEnd, _endMeasure) = AddOptionalValueRow(
            "Specify Loop End Measure:", 1.0, 999999.0, 1.0, 2.0);
    }

    protected override bool TryGetLoopTimes(out double beginSec, out double endSec, out string errorKey)
    {
        errorKey = null;

        double bpm = _bpm.Value;
        if (bpm <= 0.0)
        {
            beginSec = 0.0;
            endSec = -1.0;
            errorKey = "BPM must be greater than 0.";
            return false;
        }

        int beatsPerMeasure = (int)_beatsPerMeasure.Value;
        int noteValue = NoteValues[_noteValue.Selected];

        beginSec = _useStart.ButtonPressed
            ? LoopMath.MeasureStartSeconds((int)_startMeasure.Value, bpm, beatsPerMeasure, noteValue)
            : 0.0;
        endSec = _useEnd.ButtonPressed
            ? LoopMath.MeasureStartSeconds((int)_endMeasure.Value, bpm, beatsPerMeasure, noteValue)
            : -1.0;
        return true;
    }
}
#endif
