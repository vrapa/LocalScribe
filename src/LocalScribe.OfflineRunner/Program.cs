// src/LocalScribe.OfflineRunner/Program.cs
using LocalScribe.Core.Audio;
using LocalScribe.Core.Model;
using LocalScribe.Core.Pipeline;
using LocalScribe.Core.Storage;
using LocalScribe.Core.Transcription;
using LocalScribe.Core.Vad;

static string? Arg(string[] args, string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

string? local = Arg(args, "--local");
string? remote = Arg(args, "--remote");
if (local is null && remote is null)
{
    Console.Error.WriteLine("usage: LocalScribe.OfflineRunner --local <wav> [--remote <wav>] " +
        "[--settings <json>] [--out <storageRoot>] [--language <code|auto>] [--model <name>] " +
        "[--backend auto|cuda|vulkan|cpu] [--vram <mb>] [--cores <n>]");
    return 2;
}

string? settingsOverride = Arg(args, "--settings");
if (Array.IndexOf(args, "--settings") >= 0 && settingsOverride is null)
{
    Console.Error.WriteLine("--settings requires a value.");
    return 2;
}
if (settingsOverride is not null && !File.Exists(settingsOverride))
{
    Console.Error.WriteLine($"--settings file not found: {settingsOverride}");
    return 2;
}
var settingsPath = settingsOverride ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LocalScribe", "settings.json");
var settingsStore = new SettingsStore(settingsPath);
var settings = await settingsStore.LoadOrDefaultAsync(persistMigration: settingsOverride is null, default);
if (Arg(args, "--out") is { } outRoot) settings = settings with { StorageRoot = outRoot };
if (Arg(args, "--language") is { } language)
    settings = settings with { Language = string.IsNullOrWhiteSpace(language) ? "auto" : language.Trim() };
if (Arg(args, "--model") is { } model) settings = settings with { Model = model };
if (Arg(args, "--backend") is { } backend)
{
    if (!Enum.TryParse<Backend>(backend, ignoreCase: true, out var parsedBackend))
    {
        Console.Error.WriteLine("--backend must be auto, cuda, vulkan, or cpu.");
        return 2;
    }
    settings = settings with { Backend = parsedBackend };
}

// Native backend order from the RESOLVED setting (spec 3 cascade for auto, constrained for an
// explicit pick), set once and after --backend has been applied. Whisper.net probes this order and
// falls through when a runtime cannot load, and only honours it before the first WhisperFactory.
Whisper.net.LibraryLoader.RuntimeOptions.RuntimeLibraryOrder =
    WhisperRuntimeOrder.For(settings.Backend);

var hardware = new StaticHardwareProbe(new HardwareInfo(
    HasCuda: int.TryParse(Arg(args, "--vram"), out int vram) && vram > 0,
    CudaVramMb: vram,
    HasVulkan: false,
    FastCores: int.TryParse(Arg(args, "--cores"), out int cores) ? cores : Environment.ProcessorCount / 2));

var runner = new OfflinePipelineRunner(
    new StoragePaths(settings.StorageRoot), settings,
    new WhisperEngineFactory(),
    () => new SileroVadModel(ModelPaths.Require("silero_vad.onnx")),
    hardware, new StopwatchClock(), TimeProvider.System,
    appVersion: typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

string id = await runner.RunAsync(new OfflineRunOptions { LocalWavPath = local, RemoteWavPath = remote }, default);
var paths = new StoragePaths(settings.StorageRoot);
Console.WriteLine($"session: {id}");
Console.WriteLine($"folder:  {paths.SessionDir(id)}");
Console.WriteLine($"read:    {paths.TranscriptMd(id)}");
return 0;
