using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TidyThreads
{
    /// <summary>
    /// The plan's test scenarios T1–T17 (no T8), named "T&lt;n&gt; &lt;plan title&gt;", and nothing else. Each setup builds
    /// its own fixtures on the current (quicktest) map. Per-phase scenarios live in Scenarios.P&lt;n&gt;.cs.
    /// </summary>
    public static partial class Scenarios
    {
        public static readonly List<(string name, Action setUp)> All = new List<(string, Action)>
        {
            // Phase 1: the dresser
            ("T1 Build a dresser", T1BuildADresser),
            ("T3 Minified and reinstalled", T3MinifiedAndReinstalled),
            ("T4 Blueprint settings", T4BlueprintSettings),
            // Phase 2: the wardrobe without VFE
            ("T2 Build a wardrobe VFE not active", T2BuildAWardrobeVfeNotActive),
            // Phase 3: hauling apparel into the furniture
            ("T5 Loose apparel mixed types", T5LooseApparelMixedTypes),
            ("T6 From lower storage", T6FromLowerStorage),
            ("T9 Non-apparel rejected", T9NonApparelRejected),
            ("T10 Linking and capacity", T10LinkingAndCapacity),
            ("T11 Unreachable dresser", T11UnreachableDresser),
            // Phase 4: dressers from pre-mod saves, and the room role
            ("T7 Conversion", T7Conversion),
            ("T12 Bedroom and comfort", T12BedroomAndComfort),
            ("T13 Uninstall and deconstruct while full", T13UninstallAndDeconstructWhileFull),
            ("T14 Save and reload", T14SaveAndReload),
        };

        private const string AsfThingClass = "AdaptiveStorage.ThingClass";

        /// <summary>The last reason a storage check failed, appended to the FAIL line.</summary>
        private static string problem;

        private static Map Map => Find.CurrentMap;
        private static Pawn Worker => Map.mapPawns.FreeColonistsSpawned.First();
        private static ThingDef Def(string defName) => DefDatabase<ThingDef>.GetNamed(defName);

        /// <summary>True when <paramref name="why"/> is null; otherwise records it in <see cref="problem"/>.</summary>
        private static bool Ok(string why)
        {
            problem = why;
            return why == null;
        }

        /// <summary>
        /// Null when <paramref name="thing"/> is ASF apparel storage with the given priority, an apparel-only filter
        /// (fixed and current), a Storage tab and the given total capacity in its inspect pane; otherwise why not.
        /// </summary>
        private static string StorageProblem(Thing thing, int capacity, StoragePriority priority = StoragePriority.Critical)
        {
            if (thing == null || !thing.Spawned)
                return "not spawned";
            if (thing.GetType().FullName != AsfThingClass || !(thing is Building_Storage storage))
                return $"{thing} is a {thing.GetType().FullName}, not {AsfThingClass}";
            StorageSettings settings = storage.GetStoreSettings();
            if (settings.Priority != priority)
                return $"{thing} priority {settings.Priority}, expected {priority}";
            ThingDef nonApparel = storage.GetParentStoreSettings()?.filter.AllowedThingDefs.FirstOrDefault(d => !d.IsApparel);
            if (storage.GetParentStoreSettings() == null || nonApparel != null)
                return $"{thing} fixed filter allows {nonApparel?.defName ?? "everything (no fixed settings)"}";
            if (settings.filter.AllowedThingDefs.FirstOrDefault(d => !d.IsApparel) is ThingDef bad)
                return $"{thing} filter allows non-apparel {bad.defName}";
            if (!settings.filter.Allows(Def("Apparel_BasicShirt")))
                return $"{thing} filter doesn't allow shirts";
            if (!storage.GetInspectTabs().Any(t => t is ITab_Storage))
                return $"{thing} has no Storage tab";
            string inspect = storage.GetInspectString();
            if (!inspect.Contains($" / {capacity}"))
                return $"{thing} inspect pane doesn't show a {capacity}-item limit: '{inspect.Replace('\n', ' ')}'";
            return null;
        }

        /// <summary>ASF's renderer flag for drawing stored items on top (read by reflection: no compile-time ASF reference).</summary>
        private static bool? ShowsContainedItems(Thing thing)
        {
            object renderer = thing.GetType().GetProperty("Renderer")?.GetValue(thing);
            return renderer?.GetType().GetProperty("ShowContainedItems")?.GetValue(renderer) as bool?;
        }

        /// <summary>The items lying on <paramref name="building"/>'s cells.</summary>
        private static List<Thing> StoredItems(Thing building) =>
            building.OccupiedRect().Cells.SelectMany(c => c.GetThingList(building.Map))
                .Where(t => t.def.category == ThingCategory.Item).ToList();

        /// <summary>
        /// Runs the scenarios <paramref name="filter"/> names (comma-separated). A part equal to a whole scenario name
        /// selects only that scenario; any other part is a substring. An empty filter is never sent (AGENTS.md).
        /// </summary>
        public static void RunAll(string filter, bool testSaveLoad, Action<int, List<string>> done)
        {
            string[] parts = (filter ?? "").Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
            bool Exact(string part) => All.Any(s => string.Equals(s.name, part, StringComparison.OrdinalIgnoreCase));
            bool Selected(string name) => parts.Any(p => Exact(p)
                ? string.Equals(name, p, StringComparison.OrdinalIgnoreCase)
                : name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
            Scenario.RunAll(All.Where(s => parts.Length == 0 || Selected(s.name)), testSaveLoad, done);
        }
    }
}
