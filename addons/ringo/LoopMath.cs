#if TOOLS
namespace Ringo;

/// <summary>
/// Shared musical-time conversion used by the measure import dialog and by
/// the regression test.
/// </summary>
public static class LoopMath
{
    /// <summary>Duration of one measure in seconds for the given tempo/signature.</summary>
    public static double SecondsPerMeasure(double bpm, int beatsPerMeasure, int noteValue)
    {
        // A quarter note lasts 60/BPM seconds; scale by the beat's note value
        // relative to a quarter note.
        double secondsPerBeat = 60.0 / bpm * (4.0 / noteValue);
        return beatsPerMeasure * secondsPerBeat;
    }

    /// <summary>Start position of a 1-based measure number in seconds.</summary>
    public static double MeasureStartSeconds(int measure, double bpm, int beatsPerMeasure, int noteValue)
        => (measure - 1) * SecondsPerMeasure(bpm, beatsPerMeasure, noteValue);
}
#endif
