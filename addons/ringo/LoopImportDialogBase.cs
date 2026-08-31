#if TOOLS
using System.Text;
using Godot;

namespace Ringo;

/// <summary>
/// Shared base for the two loop import dialogs: audio resource picker, loop
/// mode selector and applying the import configuration. Selecting a resource
/// reads its current import settings back into the dialog. All feedback is
/// logged to the editor Output panel via GD.Print/GD.PrintErr; the dialog
/// stays open after OK. Derived classes add their own input rows and
/// implement <see cref="TryGetLoopTimes"/>.
/// </summary>
[Tool]
public abstract partial class LoopImportDialogBase : ConfirmationDialog
{
    // WAV import option "edit/loop_mode" enum (resource_importer_wav.cpp):
    // 0=Detect From Cue Points, 1=Disabled, 2=Forward, 3=Ping-Pong, 4=Backward.
    // NOTE: this is shifted by one from AudioStreamWav.LoopMode (0=Disabled..3=Backward).
    private static readonly string[] LoopModeKeys =
        { "Detect From Cue Points", "Disabled", "Forward", "Ping-Pong", "Backward" };

    protected EditorResourcePicker _picker;
    protected VBoxContainer _fields;

    private OptionButton _loopMode;
    private Resource _resource;
    private string _populatedPath;
    private bool _skipPopulateOnce;

    protected LoopImportDialogBase(string titleKey)
    {
        Title = L10n.Tr(titleKey);
        MinSize = new Vector2I(420, 0);
        // Editor popups (Quick Open etc.) are exclusive child windows too;
        // being exclusive ourselves conflicts with them
        // (window.cpp: "parent window already has another exclusive child").
        Exclusive = false;
        // A transient non-exclusive child window gets minimized behind the
        // main window when the resource picker's dialog closes; make this an
        // independent top-level window instead.
        Transient = false;

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        // Audio resource picker row (WAV / OGG / MP3).
        var audioRow = new HBoxContainer();
        audioRow.AddThemeConstantOverride("separation", 8);
        var audioLabel = new Label
        {
            Text = L10n.Tr("Audio Resource:"),
            CustomMinimumSize = new Vector2(200, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _picker = new EditorResourcePicker
        {
            BaseType = "AudioStream",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _picker.ResourceChanged += res =>
        {
            _resource = res;
            // Only populate when the picked file actually changes: reimporting
            // fires ResourceChanged again asynchronously, and re-populating
            // then would stomp any edits the user has not confirmed yet.
            PopulateFromCurrentSettings(force: false);
            // The picker's file dialog may leave this window minimized;
            // bring it back in front.
            CallDeferred(MethodName.RestoreWindow);
        };
        audioRow.AddChild(audioLabel);
        audioRow.AddChild(_picker);
        root.AddChild(audioRow);

        // Field rows: loop mode first, then rows supplied by derived classes.
        _fields = new VBoxContainer();
        _fields.AddThemeConstantOverride("separation", 8);
        root.AddChild(_fields);

        _loopMode = new OptionButton();
        foreach (string key in LoopModeKeys)
            _loopMode.AddItem(L10n.Tr(key));
        _loopMode.Select(2); // Forward
        AddLabeledRow("Loop Mode:", _loopMode);

        GetOkButton().Text = L10n.Tr("OK");
        GetCancelButton().Text = L10n.Tr("Cancel");
        Confirmed += OnConfirmed;
        // Read the latest .import state every time the dialog is opened, so
        // changes made elsewhere (e.g. the Import dock) are reflected.
        VisibilityChanged += () =>
        {
            if (!Visible)
                return;
            if (_skipPopulateOnce)
            {
                _skipPopulateOnce = false;
                return;
            }
            PopulateFromCurrentSettings(force: true);
        };
    }

    /// <summary>The picked audio resource (tracked via signal; picker property as fallback).</summary>
    protected Resource CurrentAudio => _resource != null ? _resource : _picker.EditedResource;

    /// <summary>
    /// Compute the loop points in seconds. Return -1 for the end to mean
    /// "end of file". <paramref name="errorKey"/> receives an English source
    /// string (translatable via <see cref="L10n.Tr"/>) when returning false.
    /// </summary>
    protected abstract bool TryGetLoopTimes(out double beginSec, out double endSec, out string errorKey);

    /// <summary>
    /// Fill dialog inputs from the file's current import settings. The loop
    /// mode selector is handled by the base class; override to fill loop
    /// point inputs. Default does nothing.
    /// </summary>
    protected virtual void PopulateLoopPoints(ImportLoopSettings settings, int? sampleRate) { }

    /// <summary>Create a checkbox + spin box row; the box is editable only while checked.</summary>
    protected (CheckBox Check, SpinBox Spin) AddOptionalValueRow(
        string checkKey, double min, double max, double step, double initial)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var check = new CheckBox
        {
            Text = L10n.Tr(checkKey),
            CustomMinimumSize = new Vector2(240, 0),
        };
        var spin = new SpinBox
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = initial,
            Editable = false,
            AllowGreater = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        check.Toggled += pressed => spin.Editable = pressed;

        row.AddChild(check);
        row.AddChild(spin);
        _fields.AddChild(row);
        return (check, spin);
    }

    /// <summary>Create a label + control row in the fields area.</summary>
    protected HBoxContainer AddLabeledRow(string labelKey, Control control)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new Label
        {
            Text = L10n.Tr(labelKey),
            CustomMinimumSize = new Vector2(140, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        control.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(control);
        _fields.AddChild(row);
        return row;
    }

    /// <summary>
    /// Sample rate of the current resource: taken from the stream for WAV,
    /// probed from the file header for OGG/MP3 (their streams don't expose it).
    /// </summary>
    protected int? GetSampleRate()
    {
        var audio = CurrentAudio;
        if (audio is AudioStreamWav wav)
            return wav.MixRate;
        return AudioSampleRateProbe.Probe(audio?.ResourcePath);
    }

    private void PopulateFromCurrentSettings(bool force)
    {
        var audio = CurrentAudio;
        if (audio == null)
            return;
        if (!force && audio.ResourcePath == _populatedPath)
            return;
        _populatedPath = audio.ResourcePath;

        var settings = LoopImportApplier.ReadSettings(audio.ResourcePath);
        if (!settings.Found)
            return;

        int mode = settings.IsWav ? settings.LoopMode : (settings.Loop ? 1 : 0);
        _loopMode.Select(System.Math.Clamp(mode, 0, LoopModeKeys.Length - 1));
        PopulateLoopPoints(settings, GetSampleRate());
    }

    private static string FormatSeconds(double seconds)
        => seconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " s";

    /// <summary>Restore and refocus this window if it ended up minimized.</summary>
    public void RestoreWindow()
    {
        if (Mode == ModeEnum.Minimized)
            Mode = ModeEnum.Windowed;
        GrabFocus();
    }

    private void OnConfirmed()
    {
        // ConfirmationDialog hides itself when OK is pressed; reopen so the
        // dialog stays open after applying. The re-show must not repopulate:
        // the user's just-applied values are the latest state.
        _skipPopulateOnce = true;
        CallDeferred(Window.MethodName.Show);

        var audio = CurrentAudio;
        if (audio == null)
        {
            GD.PrintErr("[ringo] " + L10n.Tr("Please select an audio resource."));
            return;
        }

        if (!TryGetLoopTimes(out double beginSec, out double endSec, out string errorKey))
        {
            GD.PrintErr("[ringo] " + L10n.Tr(errorKey));
            return;
        }

        if (endSec >= 0 && endSec <= beginSec)
        {
            GD.PrintErr("[ringo] " + L10n.Tr("Loop end must be after loop start."));
            return;
        }

        int? rate = GetSampleRate();

        // Log the computed loop points before applying.
        var sb = new StringBuilder();
        sb.Append("[ringo] ").Append(audio.ResourcePath);
        sb.Append(" | ").Append(L10n.Tr("Sample Rate:")).Append(' ')
            .Append(rate.HasValue ? rate.Value.ToString() : "?").Append(" Hz");
        sb.Append(" | ").Append(L10n.Tr("Loop Mode:")).Append(' ')
            .Append(LoopModeKeys[_loopMode.Selected]);
        sb.Append(" | ").Append(L10n.Tr("Loop Start:")).Append(' ').Append(FormatSeconds(beginSec));
        if (rate.HasValue)
            sb.Append(" = ").Append((long)System.Math.Round(beginSec * rate.Value)).Append(' ')
                .Append(L10n.Tr("samples"));
        sb.Append(" | ").Append(L10n.Tr("Loop End:")).Append(' ');
        if (endSec < 0)
        {
            sb.Append(L10n.Tr("end of file"));
        }
        else
        {
            sb.Append(FormatSeconds(endSec));
            if (rate.HasValue)
                sb.Append(" = ").Append((long)System.Math.Round(endSec * rate.Value)).Append(' ')
                    .Append(L10n.Tr("samples"));
        }
        GD.Print(sb.ToString());

        string error = LoopImportApplier.Apply(
            audio.ResourcePath, beginSec, endSec, rate, _loopMode.Selected, out string note);
        if (error != null)
        {
            GD.PrintErr("[ringo] " + error);
            return;
        }

        string message = L10n.Tr("Import settings updated, file reimported:") + " " + audio.ResourcePath;
        if (note != null)
            message += " " + note;
        GD.Print("[ringo] " + message);

        // Read the .import file back so the written state is visible in the log.
        var written = LoopImportApplier.ReadSettings(audio.ResourcePath);
        GD.Print("[ringo] .import now: " + (written.IsWav
            ? "loop_mode=" + written.LoopMode + " loop_begin=" + written.LoopBegin + " loop_end=" + written.LoopEnd
            : "loop=" + written.Loop + " loop_offset=" + written.LoopOffset));
    }
}
#endif
