using LocalScribe.Core.Model;
namespace LocalScribe.Core.Transcription;

/// <summary>The chosen engine configuration: backend + ggml model name (spec section 3).
/// CpuThreads rides along on every plan (the worker's VRAM-OOM floor fall flips Backend to Cpu
/// via `with {}` without re-running Select) but only takes effect on the CPU backend -
/// EffectiveThreads is what the engine actually applies; null keeps whisper.cpp defaults.</summary>
public sealed record BackendPlan(Backend Backend, string ModelName, int? CpuThreads = null)
{
    public int? EffectiveThreads => Backend == Backend.Cpu ? CpuThreads : null;
}

/// <summary>Pure spec-section 3 selection: probe order CUDA -> Vulkan -> CPU, model per tier,
/// explicit user overrides always win, .en weights when the session language is English.</summary>
public static class BackendSelector
{
    // Worst -> best. Auto never exceeds the per-backend ceiling; it downgrades within this ladder
    // to the best model actually present on disk (design section 1).
    // Final-review Finding 3: intentionally English-only - fetch-models ships only .en weights and
    // English is the primary use case (Webex/Zoom lawyer-jail calls). A non-English `auto` session
    // with only multilingual models present would refuse rather than downgrade; that's a Stage-7
    // concern (multilingual downgrade ladder), not a bug in this ladder.
    private static readonly string[] Ladder = ["tiny", "base", "small"];

    public static (BackendPlan Plan, string? DowngradedFrom) Select(
        HardwareInfo hw, Settings settings, IReadOnlySet<string> availableModels)
    {
        Backend backend = settings.Backend != Backend.Auto
            ? settings.Backend
            : hw.HasCuda && hw.CudaVramMb >= 4096 ? Backend.Cuda
            : hw.HasVulkan ? Backend.Vulkan
            : Backend.Cpu;

        string? downgradedFrom = null;
        string model;
        if (settings.Model != "auto")
        {
            // Explicit: canonical NAME (quant suffix is a file detail - AvailableModels and the
            // Start presence gate both speak canonical names, so a persisted "small.en-q8_0"
            // must not be refused as "not downloaded" while its file sits on disk; review
            // finding 2026-07-13). ModelFileResolver picks the FILE per backend; unknown
            // suffixes pass through verbatim and load as raw names. Start validates presence.
            model = ModelFileResolver.CanonicalName(settings.Model);
        }
        else
        {
            string ceilingStem = backend switch
            {
                Backend.Cuda => "small",
                Backend.Vulkan => "base",
                _ => hw.FastCores >= 8 ? "small" : "base",
            };
            bool needsMultilingual = settings.Language is not ("en" or "auto");
            model = BestPresentAtOrBelow(ceilingStem, availableModels, needsMultilingual);
            string ceiling = needsMultilingual ? ceilingStem : ceilingStem + ".en";
            if (model != ceiling) downgradedFrom = ceiling;   // record the downgrade for a Start notice
        }

        bool english = settings.Language is "en" or "auto";
        if (!english && model.EndsWith(".en", StringComparison.Ordinal))
            model = model[..^3];                        // multilingual weights (spec 3)

        return (new BackendPlan(backend, model, AutoCpuThreads(hw.FastCores)), downgradedFrom);
    }

    /// <summary>whisper.cpp thread count for CPU inference: fastCores - 2 leaves headroom for
    /// the live call + WASAPI capture + UI on big machines, but never below whisper.cpp's own
    /// default of min(4, logical cores) (logical ~= 2 * fastCores; review finding 2026-07-13:
    /// a bare fastCores - 2 halved throughput on quad-core laptops vs the default it exists to
    /// beat). Cap 8: whisper.cpp is memory-bandwidth bound past that.</summary>
    public static int AutoCpuThreads(int fastCores)
        => Math.Clamp(Math.Max(Math.Min(4, 2 * fastCores), fastCores - 2), 2, 8);

    private static string BestPresentAtOrBelow(string ceilingStem, IReadOnlySet<string> available,
        bool needsMultilingual)
    {
        int ceilingRank = Array.IndexOf(Ladder, ceilingStem);
        for (int r = ceilingRank; r >= 0; r--)
        {
            string stem = Ladder[r];
            if (needsMultilingual)
            {
                if (available.Contains(stem)) return stem;
            }
            else
            {
                if (available.Contains(stem + ".en")) return stem + ".en";
                if (available.Contains(stem)) return stem; // English also works with multilingual weights.
            }
        }
        // Nothing present at/below the ceiling: return the ceiling name unchanged so Start's
        // fail-fast (Task 3) refuses with a clear "not downloaded" message.
        return needsMultilingual ? ceilingStem : ceilingStem + ".en";
    }
}
