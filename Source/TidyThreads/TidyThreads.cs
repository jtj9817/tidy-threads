using System.Reflection;
using HarmonyLib;
using Verse;

namespace TidyThreads
{
    /// <summary>Marker on every def this mod turns into apparel storage (the dresser and the wardrobe).</summary>
    public class ApparelStorageExtension : DefModExtension
    {
        /// <summary>Informational. The real capacity is building.maxItemsInCell × the def's cell count.</summary>
        public int capacity = 20;
    }

    public class TidyThreadsMod : Mod
    {
        public const string PackageId = "josh9817.tidythreads";

        public TidyThreadsMod(ModContentPack content) : base(content)
        {
            // Always printed: tools/autotest.py and tools/status.sh compare it with .devlog/last-build.
            Verse.Log.Message($"[TidyThreads] loaded build {TT.BuildId}");
            new Harmony(PackageId).PatchAll();
        }
    }

    /// <summary>The mod's log wrapper, and the test harness hook-up once the game has started.</summary>
    [StaticConstructorOnStartup]
    public static class TT
    {
        public static readonly string BuildId =
            typeof(TT).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

        static TT()
        {
            AutoTest.Init();
        }

        /// <summary>A [TidyThreads] line, only in dev mode or an automated test run.</summary>
        public static void Log(string message)
        {
            if (Prefs.DevMode || AutoTest.Enabled)
                Verse.Log.Message($"[TidyThreads] {message}");
        }

        /// <summary>A red error. tools/autotest.py counts every "[TidyThreads] ERROR" line as a new error.</summary>
        public static void Error(string message) => Verse.Log.Error($"[TidyThreads] ERROR: {message}");
    }
}
