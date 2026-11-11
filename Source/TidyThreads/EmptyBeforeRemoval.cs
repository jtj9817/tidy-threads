using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TidyThreads
{
    /// <summary>
    /// TT-001: empties a dresser or wardrobe before it is deconstructed or uninstalled (which reinstalling does too).
    /// Otherwise vanilla <c>Building.DeSpawn</c> drops the overflow itself: one item per cell stays, and each other item
    /// is taken off the map and put back with <c>GenPlace.TryPlaceThing(Near)</c>, whose result it ignores. With no free
    /// cell within 12.9 cells, that item is lost with no error. Emptied first, the furniture leaves nothing for it to drop.
    /// </summary>
    public static class EmptyBeforeRemoval
    {
        /// <summary>A Near search reaches 12.9 cells from its center, so centers this far apart cover the whole map.</summary>
        private const int FallbackStep = 18;

        /// <summary>
        /// Moves every item off <paramref name="building"/>'s cells when it is our spawned apparel furniture: next to it
        /// as vanilla would, else at the nearest free spot anywhere on the map. An item with no spot anywhere stays put
        /// and is reported.
        /// </summary>
        public static void Empty(Thing building, string why)
        {
            if (building == null || !building.Spawned || !building.def.HasModExtension<ApparelStorageExtension>())
                return;
            Map map = building.Map;
            CellRect rect = building.OccupiedRect();
            List<Thing> items = rect.Cells.SelectMany(c => c.GetThingList(map))
                .Where(t => t.def.category == ThingCategory.Item).ToList();
            if (items.Count == 0)
                return;
            bool Outside(IntVec3 c) => !rect.Contains(c);
            List<IntVec3> centers = null;
            int far = 0;
            foreach (Thing item in items)
            {
                IntVec3 from = item.Position;
                item.DeSpawn();
                if (GenPlace.TryPlaceThing(item, from, map, ThingPlaceMode.Near, null, Outside))
                    continue;
                centers ??= FallbackCenters(map, rect.CenterCell);
                if (centers.Any(c => GenPlace.TryPlaceThing(item, c, map, ThingPlaceMode.Near, null, Outside)))
                {
                    far++;
                    continue;
                }
                // It just left this cell, so the cell has room for it again.
                GenSpawn.Spawn(item, from, map);
                TT.Error($"no free cell on the map for {item} from {building} ({why}); it stays on the furniture and may be lost");
            }
            TT.Log($"emptied {items.Count} item(s) from {building} before {why}" + (far > 0 ? $", {far} beyond the nearby cells" : ""));
        }

        /// <summary>Grid points over the whole map, nearest to <paramref name="origin"/> first.</summary>
        private static List<IntVec3> FallbackCenters(Map map, IntVec3 origin)
        {
            var centers = new List<IntVec3>();
            for (int x = FallbackStep / 2; x < map.Size.x + FallbackStep / 2; x += FallbackStep)
                for (int z = FallbackStep / 2; z < map.Size.z + FallbackStep / 2; z += FallbackStep)
                    centers.Add(new IntVec3(Mathf.Min(x, map.Size.x - 1), 0, Mathf.Min(z, map.Size.z - 1)));
            return centers.OrderBy(c => c.DistanceToSquared(origin)).ToList();
        }
    }

    /// <summary>Every deconstruction (the job, and god mode's instant one) ends in <c>Destroy(Deconstruct)</c>.</summary>
    [HarmonyPatch(typeof(Building_Storage), nameof(Building_Storage.Destroy))]
    public static class EmptyBeforeDeconstruct
    {
        public static void Prefix(Building_Storage __instance, DestroyMode mode)
        {
            if (mode == DestroyMode.Deconstruct)
                EmptyBeforeRemoval.Empty(__instance, "deconstruction");
        }
    }

    /// <summary>
    /// Uninstalling (the job), reinstalling (<c>Toils_Construct.UninstallIfMinifiable</c>) and refunds all minify
    /// through <c>MakeMinified</c>.
    /// </summary>
    [HarmonyPatch(typeof(MinifyUtility), nameof(MinifyUtility.MakeMinified))]
    public static class EmptyBeforeMinify
    {
        public static void Prefix(Thing thing) => EmptyBeforeRemoval.Empty(thing, "uninstalling");
    }
}
