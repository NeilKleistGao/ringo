#if TOOLS
using Godot;

namespace Ringo;

/// <summary>
/// ringo editor plugin. Adds a "ringo" submenu under Project > Tools with two
/// entries that open the loop import dialogs.
/// </summary>
[Tool]
public partial class RingoPlugin : EditorPlugin
{
    private enum MenuId
    {
        ImportByLoopTime = 0,
        ImportByLoopMeasure = 1,
    }

    private PopupMenu _menu;
    private LoopTimeImportDialog _timeDialog;
    private LoopMeasureImportDialog _measureDialog;

    public override void _EnterTree()
    {
        _menu = new PopupMenu();
        _menu.AddItem(L10n.Tr("Import by Loop Time"), (int)MenuId.ImportByLoopTime);
        _menu.AddItem(L10n.Tr("Import by Loop Measure"), (int)MenuId.ImportByLoopMeasure);
        _menu.IdPressed += OnMenuIdPressed;
        // NOTE: AddToolSubmenuItem requires the submenu to have NO parent;
        // the editor itself parents it under the Tools menu.
        AddToolSubmenuItem("ringo", _menu);

        // Regression test entry point: godot --headless -e with RINGO_RUN_TESTS=1.
        // Resolved by reflection so this plugin has NO compile-time dependency
        // on the test code: addons/ringo can be copied into other projects
        // without the tests/ folder and still builds and runs fine.
        if (OS.GetEnvironment("RINGO_RUN_TESTS") == "1")
            TryScheduleRegressionTest();
    }

    private void TryScheduleRegressionTest()
    {
        System.Type type = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            type = assembly.GetType("Ringo.Tests.LoopImportRegressionTest", false);
            if (type != null)
                break;
        }
        var schedule = type?.GetMethod("Schedule",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (schedule == null)
        {
            GD.PrintErr("[ringo] RINGO_RUN_TESTS is set but Ringo.Tests.LoopImportRegressionTest was not found.");
            return;
        }
        schedule.Invoke(null, new object[] { this });
    }

    public override void _ExitTree()
    {
        RemoveToolMenuItem("ringo");
        // The Tools menu owns the submenu after AddToolSubmenuItem; it may
        // already be freed during editor shutdown, so guard the free.
        if (_menu != null && GodotObject.IsInstanceValid(_menu))
            _menu.QueueFree();
        _menu = null;
        _timeDialog?.QueueFree();
        _timeDialog = null;
        _measureDialog?.QueueFree();
        _measureDialog = null;
    }

    private void OnMenuIdPressed(long id)
    {
        switch ((MenuId)id)
        {
            case MenuId.ImportByLoopTime:
                _timeDialog ??= CreateDialog<LoopTimeImportDialog>();
                _timeDialog.PopupCentered();
                break;
            case MenuId.ImportByLoopMeasure:
                _measureDialog ??= CreateDialog<LoopMeasureImportDialog>();
                _measureDialog.PopupCentered();
                break;
        }
    }

    private T CreateDialog<T>() where T : LoopImportDialogBase, new()
    {
        var dialog = new T();
        // Dialogs must be parented to the editor's base control, not to the
        // EditorPlugin node, otherwise popup input/closing misbehaves.
        EditorInterface.Singleton.GetBaseControl().AddChild(dialog);
        return dialog;
    }
}
#endif
