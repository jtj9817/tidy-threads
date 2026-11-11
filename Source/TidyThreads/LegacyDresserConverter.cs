using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TidyThreads
{
    /// <summary>
    /// Converts dressers that were in a save before the mod was added: the plan's Option 2, Patch B only (ASF's own
    /// <c>SaveGameCompatibility</c> already swaps the saved <c>Building</c> class for its ThingClass, which is Patch A).
    /// Option 1 alone failed T7: ASF then sets <c>settings ??= new(this)</c>, an empty filter at Normal.
    /// <para>
    /// Detection: a pre-mod dresser was saved as a plain Building, so its node has no <c>settings</c>. This postfix runs
    /// inside ASF's <c>base.ExposeData()</c>, right after vanilla's <c>Scribe_Deep.Look(ref settings, "settings")</c>
    /// and before ASF's fallback, so a null there means exactly that. The plan's PostLoadInit null check never fires,
    /// because ASF has filled settings by then. A dresser saved with the mod has a settings node and is left alone, so a
    /// player's own priority survives every reload (T14).
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.ExposeData))]
    public static class LegacyDresserConverter
    {
        /// <summary>ThingIDs converted in this process, for the scenarios (T7, T14).</summary>
        public static readonly HashSet<string> Converted = new HashSet<string>();

        public static void Postfix(Building_Storage __instance)
        {
            if (Scribe.mode != LoadSaveMode.LoadingVars || __instance.settings != null
                || !__instance.def.HasModExtension<ApparelStorageExtension>())
                return;
            __instance.settings = new StorageSettings(__instance);
            if (__instance.def.building.defaultStorageSettings != null)
                __instance.settings.CopyFrom(__instance.def.building.defaultStorageSettings);
            Converted.Add(__instance.ThingID);
            TT.Log($"converted pre-mod {__instance.def.defName} {__instance.ThingID} into apparel storage ({__instance.settings.Priority})");
        }
    }
}
