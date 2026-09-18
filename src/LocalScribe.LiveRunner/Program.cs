// src/LocalScribe.LiveRunner/Program.cs
//
// Stage 3a manual smoke harness: records a REAL meeting live through the full pipeline.
// MTA main thread (console default, no [STAThread]) - required by ProcessLoopbackCapture.
//
// Keys:  R = start   P = pause/resume   S = stop (finalize)   Q = quit
// Flags: --settings <json>  --out <storageRoot>  --language <code|auto>
//        --model <name>  --backend <auto|cuda|vulkan|cpu>  --vram <mb>  --no-preflight
//        --app <image>   (explicit perProcess target, e.g. CiscoCollabHost)
//        --system-mix    (force full-system EXCLUDE-self remote capture)
//        --finalize-timeout <seconds>  (bounded wait for transcript/session persistence; default 120)
//        --auto <seconds>  (headless smoke affordance: start immediately, record N seconds,
//                           stop, and exit - no console keys required. Prints the same
//                           StateChanged/Notice/ErrorRaised/LineInserted stream as interactive
//                           mode so a smoke run can be captured to a log file.)

using LocalScribe.Core.Audio;
using LocalScribe.Core.Live;
using LocalScribe.Core.Model;
using LocalScribe.Core.Storage;
using LocalScribe.Core.Transcription;
using LocalScribe.Core.Vad;
using Whisper.net.LibraryLoader;

// The native backend order is set BELOW, once settings (and the --backend override) are known -
// it used to be an unconditional literal here, which is why the Backend setting constrained
// nothing. Whisper.net only honours RuntimeOptions before the first WhisperFactory.

string? Arg(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

bool HasArg(string name) => Array.IndexOf(args, name) >= 0;

foreach (string valueFlag in new[] { "--settings", "--out", "--language", "--model", "--backend",
    "--vram", "--app", "--auto", "--finalize-timeout" })
{
    if (HasArg(valueFlag) && Arg(valueFlag) is null)
    {
        Console.Error.WriteLine($"{valueFlag} requires a value.");
        return 2;
    }
}

double finalizeTimeoutSeconds = 120;
if (Arg("--finalize-timeout") is { } timeoutArg &&
    (!double.TryParse(timeoutArg, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out finalizeTimeoutSeconds) ||
     !double.IsFinite(finalizeTimeoutSeconds) || finalizeTimeoutSeconds <= 0))
{
    Console.Error.WriteLine("--finalize-timeout must be a positive number of seconds.");
    return 2;
}

string? explicitSettingsPath = Arg("--settings");
if (explicitSettingsPath is not null && !File.Exists(explicitSettingsPath))
{
    Console.Error.WriteLine($"--settings file not found: {explicitSettingsPath}");
    return 2;
}
var settingsPath = explicitSettingsPath ?? Path.Combine(Environment.GetFolderPath(
    Environment.SpecialFolder.ApplicationData), "LocalScribe", "settings.json");
var settings = await new SettingsStore(settingsPath)
    .LoadOrDefaultAsync(persistMigration: explicitSettingsPath is null, default);
if (Arg("--out") is { } outRoot) settings = settings with { StorageRoot = outRoot };
if (Arg("--language") is { } language)
    settings = settings with { Language = string.IsNullOrWhiteSpace(language) ? "auto" : language.Trim() };
if (Arg("--model") is { } model) settings = settings with { Model = model };
if (Arg("--backend") is { } backend)
{
    if (!Enum.TryParse<Backend>(backend, ignoreCase: true, out var parsedBackend))
    {
        Console.Error.WriteLine("--backend must be auto, cuda, vulkan, or cpu.");
        return 2;
    }
    settings = settings with { Backend = parsedBackend };
}
if (args.Contains("--system-mix"))
    settings = settings with { Remote = settings.Remote with { Mode = RemoteMode.SystemMix } };
else if (Arg("--app") is { } app)
    settings = settings with { Remote = new RemoteSetting { Mode = RemoteMode.PerProcess, App = app } };

// Host responsibility: the native backend order, once per process, from the resolved setting -
// AFTER --backend has been applied, and before anything creates an engine.
RuntimeOptions.RuntimeLibraryOrder = WhisperRuntimeOrder.For(settings.Backend);

IHardwareProbe hardware = Arg("--vram") is { } vram && int.TryParse(vram, out int mb)
    ? new StaticHardwareProbe(new HardwareInfo(mb > 0, mb, false, Environment.ProcessorCount / 2))
    : new LiveHardwareProbe();
var hw = hardware.Probe();
Console.WriteLine($"Hardware: cuda={hw.HasCuda} vram={hw.CudaVramMb}MB vulkan={hw.HasVulkan} fastCores={hw.FastCores}");
Console.WriteLine($"Backend plan: {BackendSelector.Select(hw, settings, ModelPaths.AvailableModels()).Plan}");

string appVersion = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
var controller = new SessionController(
    new StoragePaths(settings.StorageRoot), settings, new WhisperEngineFactory(),
    () => new SileroVadModel(ModelPaths.Require("silero_vad.onnx")),
    hardware, new WasapiCaptureSourceProvider(settings, new WasapiSessionScanner()),
    () => new StopwatchClock(), TimeProvider.System, appVersion);

static string Ts(long ms) => TimeSpan.FromMilliseconds(ms).ToString(
    ms >= 3_600_000 ? @"h\:mm\:ss" : @"mm\:ss", System.Globalization.CultureInfo.InvariantCulture);

controller.StateChanged += s => Console.WriteLine($"-- state: {s}");
controller.Notice += n => Console.WriteLine($"-- notice: {n}");
bool errorRaised = false;
controller.ErrorRaised += e =>
{
    errorRaised = true;
    Console.WriteLine($"-- error: {e}");
};
controller.LineInserted += (_, line) => Console.WriteLine(
    line.Kind == TranscriptKind.Marker
        ? $"  [{Ts(line.StartMs)}] _[{line.Text}]_"
        : $"  [{Ts(line.StartMs)}] {line.SpeakerLabel}: {line.Text}");

var options = new LiveSessionOptions
{ App = AppKind.Webex, RunPreflightProbe = !args.Contains("--no-preflight") };

async Task<bool> AwaitFinalizedAsync(string sessionId)
{
    try
    {
        await controller.PendingFinalize.WaitAsync(TimeSpan.FromSeconds(finalizeTimeoutSeconds));
    }
    catch (TimeoutException)
    {
        Console.WriteLine($"FAULT: finalization did not complete within {finalizeTimeoutSeconds:0.###} seconds.");
        return false;
    }

    string sessionJson = new StoragePaths(settings.StorageRoot).SessionJson(sessionId);
    SessionRecord? record;
    try
    {
        record = await new SessionStore(sessionJson).ReadAsync(
            selfForMigration: null, persistMigration: false, CancellationToken.None);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAULT: finalized session metadata could not be read: {ex.Message}");
        return false;
    }

    if (record?.EndedAtUtc is null)
    {
        Console.WriteLine("FAULT: finalization task completed but session.json has no endedAtUtc.");
        return false;
    }
    if (errorRaised)
    {
        Console.WriteLine("FAULT: the session raised an error; inspect the notices and retained audio above.");
        return false;
    }

    Console.WriteLine($"finalized -> {new StoragePaths(settings.StorageRoot).SessionDir(sessionId)}");
    return true;
}

// --auto <seconds>: headless smoke path (no console keys). Start, wait, stop, exit.
if (Arg("--auto") is { } autoSeconds && double.TryParse(autoSeconds, out double seconds))
{
    try
    {
        string? autoId = await controller.StartAsync(options, default);
        if (autoId is null)
        {
            Console.WriteLine("FAULT: StartAsync returned null (see notices above).");
            return 1;
        }
        Console.WriteLine($"recording -> {autoId}");
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        string? autoStopped = await controller.StopAsync(default);
        if (autoStopped is null) return 1;
        Console.WriteLine("capture stopped; waiting for transcript finalization...");
        return await AwaitFinalizedAsync(autoStopped) ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAULT: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
        return 1;
    }
}

Console.WriteLine("R = start, P = pause/resume, S = stop, Q = quit");
while (true)
{
    var key = Console.ReadKey(intercept: true).Key;
    try
    {
        switch (key)
        {
            case ConsoleKey.R:
                string? id = await controller.StartAsync(options, default);
                if (id is not null) Console.WriteLine($"recording -> {id}");
                break;
            case ConsoleKey.P:
                if (controller.State == SessionState.Paused) await controller.ResumeAsync(default);
                else await controller.PauseAsync(default);
                break;
            case ConsoleKey.S:
                string? stopped = await controller.StopAsync(default);
                if (stopped is not null)
                {
                    Console.WriteLine("capture stopped; waiting for transcript finalization...");
                    await AwaitFinalizedAsync(stopped);
                }
                break;
            case ConsoleKey.Q:
                string? quittingSession = null;
                if (controller.State is SessionState.Recording or SessionState.Paused)
                    quittingSession = await controller.StopAsync(default);
                if (quittingSession is not null && !await AwaitFinalizedAsync(quittingSession))
                    return 1;
                if (!controller.PendingFinalize.IsCompleted)
                {
                    try { await controller.PendingFinalize.WaitAsync(TimeSpan.FromSeconds(finalizeTimeoutSeconds)); }
                    catch (TimeoutException)
                    {
                        Console.WriteLine($"FAULT: finalization did not complete within {finalizeTimeoutSeconds:0.###} seconds.");
                        return 1;
                    }
                }
                return errorRaised ? 1 : 0;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAULT: {ex.GetType().Name}: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }
}
