#if TOOLS
using Godot;

namespace Ringo.Tests;

/// <summary>
/// Regression test for the ringo loop import plugin. Runs inside the editor
/// when started with RINGO_RUN_TESTS=1 (optionally RINGO_TEST_PATH to override
/// the test asset, default res://little star demo.wav — 4/4, 120 BPM, 24 s).
///
/// Steps: verify measure-to-seconds math, apply a time-based loop and a
/// measure-based loop (checking the written .import params each time), poll
/// the engine resource until the reimport is observable, then restore the
/// original .import and verify the restore. Writes a log to
/// res://test_result.txt, prints RINGO_TEST_RESULT: PASS/FAIL and quits with
/// exit code 0/1.
/// </summary>
public static class LoopImportRegressionTest
{
    private const string ResultPath = "res://test_result.txt";
    private const string StartedMarker = "RINGO_TEST_STARTED";
    private const string LockDir = "res://.ringo_test_lock";
    private const string LockPidFile = "res://.ringo_test_lock/pid";
    private const string LockRunningFile = "res://.ringo_test_lock/running";
    private const int PollAttempts = 20; // 20 x 1 s = 20 s per wait

    private static Node _host;
    private static string _path;
    private static string _importPath;
    private static string _originalImportText;
    private static ImportLoopSettings _original;
    private static int _rate;
    private static bool _failed;

    private static void Trace(string line)
    {
        using var f = FileAccess.Open("res://test_trace.txt", FileAccess.ModeFlags.ReadWrite)
                      ?? FileAccess.Open("res://test_trace.txt", FileAccess.ModeFlags.Write);
        if (f == null)
            return;
        f.SeekEnd();
        f.StoreLine(Time.GetTicksMsec() + " pid=" + OS.GetProcessId() + " " + line);
    }

    public static void Schedule(Node host)
    {
        // Single-run guard: assembly reloads (same process) and editor
        // self-restarts (new process) can schedule the test more than once.
        // An atomic lock directory marks the owning process; a duplicate
        // schedule in the same process is ignored silently, one from another
        // process quits that process so only one test ever runs.
        long pid = OS.GetProcessId();
        var mkResult = DirAccess.MakeDirAbsolute(LockDir);
        Trace("Schedule: MakeDir=" + mkResult + " lockExists=" + DirAccess.DirExistsAbsolute(LockDir));
        if (mkResult != Error.Ok)
        {
            long ownerPid = -1;
            if (FileAccess.FileExists(LockPidFile))
            {
                using var pf = FileAccess.Open(LockPidFile, FileAccess.ModeFlags.Read);
                if (pf != null)
                    long.TryParse(pf.GetAsText().Trim(), out ownerPid);
            }
            Trace("Schedule: skipping (owner=" + ownerPid + ")");
            if (ownerPid != pid)
                host.GetTree().CreateTimer(1.0).Timeout += () => host.GetTree().Quit(0);
            return;
        }
        Trace("Schedule: won lock, arming Run");
        using (var pf = FileAccess.Open(LockPidFile, FileAccess.ModeFlags.Write))
            pf?.StoreString(pid.ToString());

        _host = host;
        using (var f = FileAccess.Open(ResultPath, FileAccess.ModeFlags.Write))
            f?.StoreLine(StartedMarker + " pid=" + pid);

        // Wait for the editor file system scan, then run.
        host.GetTree().CreateTimer(4.0).Timeout += Run;
        // Safety net so CI can never hang.
        host.GetTree().CreateTimer(120.0).Timeout += () =>
        {
            Log("FAIL: global timeout");
            Finish();
        };
    }

    private static void Log(string line)
    {
        GD.Print(line);
        using var f = FileAccess.Open(ResultPath, FileAccess.ModeFlags.ReadWrite);
        if (f == null)
            return;
        f.SeekEnd();
        f.StoreLine(line);
    }

    private static void ExpectEq(string name, long actual, long expected)
    {
        bool ok = actual == expected;
        Log((ok ? "PASS: " : "FAIL: ") + name + " (expected " + expected + ", got " + actual + ")");
        if (!ok)
            _failed = true;
    }

    private static void ExpectApprox(string name, double actual, double expected)
    {
        bool ok = System.Math.Abs(actual - expected) < 0.0001;
        Log((ok ? "PASS: " : "FAIL: ") + name + " (expected " + expected + ", got " + actual + ")");
        if (!ok)
            _failed = true;
    }

    private static void ApplyAndCheckFile(
        string tag, double beginSec, double endSec, int mode, long expectedBegin, long expectedEnd)
    {
        string error = LoopImportApplier.Apply(_path, beginSec, endSec, _rate, mode, out _);
        if (error != null)
        {
            Log("FAIL: " + tag + " apply error: " + error);
            _failed = true;
            return;
        }
        var s = LoopImportApplier.ReadSettings(_path);
        ExpectEq(tag + " file loop_mode", s.LoopMode, mode);
        ExpectEq(tag + " file loop_begin", s.LoopBegin, expectedBegin);
        ExpectEq(tag + " file loop_end", s.LoopEnd, expectedEnd);
    }

    /// <summary>
    /// Engine-side check: the imported AudioStreamWav must reflect the .import
    /// params. The file's loop_mode option (0=Detect,1=Disabled,2=Forward,...)
    /// maps to AudioStreamWav.LoopMode shifted by one; loop_end = -1 resolves
    /// to the data length at import time and is not checked.
    /// </summary>
    private static bool EngineMatches(long fileMode, long begin, long end)
    {
        var wav = ResourceLoader.Load<AudioStreamWav>(_path, null, ResourceLoader.CacheMode.Ignore);
        if (wav == null)
            return false;
        long expectedMode = fileMode == 0 ? 0 : fileMode - 1;
        if ((int)wav.LoopMode != expectedMode)
            return false;
        // The importer only applies loop points when looping is enabled
        // (file loop_mode >= 2, i.e. Forward/Ping-Pong/Backward).
        if (fileMode >= 2)
        {
            if (wav.LoopBegin != begin)
                return false;
            if (end >= 0 && wav.LoopEnd != end)
                return false;
        }
        return true;
    }

    private static void PollUntil(string tag, System.Func<bool> check, int attemptsLeft, System.Action onDone)
    {
        if (check())
        {
            Log("PASS: " + tag);
            onDone();
            return;
        }
        if (attemptsLeft <= 0)
        {
            Log("FAIL: " + tag + " (engine state did not converge within timeout)");
            _failed = true;
            onDone();
            return;
        }
        _host.GetTree().CreateTimer(1.0).Timeout += () => PollUntil(tag, check, attemptsLeft - 1, onDone);
    }

    private static void Run()
    {
        // Idempotency guard: an assembly hot-reload at editor startup can
        // deliver the same timer signal to both the old and the reloaded
        // assembly a few milliseconds apart. A static flag cannot dedupe
        // across assembly load contexts, so use the filesystem.
        if (FileAccess.FileExists(LockRunningFile))
        {
            Trace("Run: duplicate invocation ignored");
            return;
        }
        using (var rf = FileAccess.Open(LockRunningFile, FileAccess.ModeFlags.Write))
            rf?.StoreString("1");
        Trace("Run: entered");
        _path = OS.GetEnvironment("RINGO_TEST_PATH");
        if (string.IsNullOrEmpty(_path))
            _path = "res://little star demo.wav";
        _importPath = _path + ".import";

        using (var f = FileAccess.Open(_importPath, FileAccess.ModeFlags.Read))
            _originalImportText = f?.GetAsText();
        if (_originalImportText == null)
        {
            Log("FAIL: cannot read " + _importPath);
            Finish();
            return;
        }
        _original = LoopImportApplier.ReadSettings(_path);

        if (ResourceLoader.Load(_path) is not AudioStreamWav wav)
        {
            Log("FAIL: cannot load test asset " + _path);
            Finish();
            return;
        }
        _rate = wav.MixRate;
        Log("test asset: " + _path + " | sample rate: " + _rate
            + " | original: mode=" + _original.LoopMode + " begin=" + _original.LoopBegin
            + " end=" + _original.LoopEnd);

        // The test asset is 4/4 at 120 BPM: one measure = 2 s, 24 s = 12 measures.
        ExpectApprox("measure 5 start @120BPM 4/4", LoopMath.MeasureStartSeconds(5, 120, 4, 4), 8.0);
        ExpectApprox("measure 9 start @120BPM 4/4", LoopMath.MeasureStartSeconds(9, 120, 4, 4), 16.0);

        // Time-based import: 2 s..4 s, Forward.
        ApplyAndCheckFile("time 2s..4s forward", 2.0, 4.0, 2, 2L * _rate, 4L * _rate);

        // Space out the applies: back-to-back reimports can destabilize the
        // editor (heap corruption observed in sandboxed environments).
        _host.GetTree().CreateTimer(3.0).Timeout += () =>
        {
            // Measure-based import: measures 5..9 (= 8 s..16 s), Ping-Pong.
            ApplyAndCheckFile("measure 5..9 ping-pong", 8.0, 16.0, 3, 8L * _rate, 16L * _rate);

            // The reimport is asynchronous: give it a head start (loading the
            // resource while the importer is mid-write can crash the editor),
            // then poll until the engine resource reflects the applied state.
            _host.GetTree().CreateTimer(3.0).Timeout += () =>
                PollUntil("engine applied measure 5..9 ping-pong",
                    () => EngineMatches(3, 8L * _rate, 16L * _rate), PollAttempts, RestoreStep);
        };
    }

    private static void RestoreStep()
    {
        using (var f = FileAccess.Open(_importPath, FileAccess.ModeFlags.Write))
            f?.StoreString(_originalImportText);
        try
        {
            EditorInterface.Singleton?.GetResourceFilesystem().ReimportFiles(new[] { _path });
        }
        catch (System.Exception) { }

        _host.GetTree().CreateTimer(3.0).Timeout += () =>
            PollUntil("engine restored to original",
                () => EngineMatches(_original.LoopMode, _original.LoopBegin, _original.LoopEnd),
                PollAttempts, VerifyRestoredFile);
    }

    private static void VerifyRestoredFile()
    {
        var s = LoopImportApplier.ReadSettings(_path);
        ExpectEq("restored file loop_mode", s.LoopMode, _original.LoopMode);
        ExpectEq("restored file loop_begin", s.LoopBegin, _original.LoopBegin);
        ExpectEq("restored file loop_end", s.LoopEnd, _original.LoopEnd);
        Finish();
    }

    private static void Finish()
    {
        Log(_failed ? "RINGO_TEST_RESULT: FAIL" : "RINGO_TEST_RESULT: PASS");
        if (FileAccess.FileExists(LockPidFile))
            DirAccess.RemoveAbsolute(LockPidFile);
        DirAccess.RemoveAbsolute(LockDir);
        _host.GetTree().Quit(_failed ? 1 : 0);
    }
}
#endif
