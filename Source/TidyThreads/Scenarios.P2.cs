using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TidyThreads
{
    /// <summary>Phase 2: our wardrobe, loaded from NoVFE/ when VFE is absent (T2).</summary>
    public static partial class Scenarios
    {
        private const string VfeCore = "VanillaExpanded.VFECore";

        /// <summary>
        /// T2. Precondition: VFE not active, a clear site. Trigger: wardrobes made in wood, steel, granite and wood, one in
        /// each rotation, then 30 shirts put in one. Expected: the wardrobe def is ours from NoVFE/, linked once to beds;
        /// each is Critical apparel storage for 30 with our texture in its rotation; the full one holds 30 undrawn.
        /// Failure mode: the NoVFE folder doesn't load, a texture is missing, or the capacity is wrong.
        /// </summary>
        private static void T2BuildAWardrobeVfeNotActive()
        {
            var f = new Fixture();
            var wardrobes = new List<string>();
            var scenario = new Scenario("T2 Build a wardrobe VFE not active", 2000).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Expect("VFE is not active and the wardrobe is ours", () =>
                {
                    if (ModsConfig.IsActive(VfeCore))
                        return Ok("VFE is active: T2 needs it off");
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Table_Wardrobe");
                    if (def == null)
                        return Ok("no Table_Wardrobe def: NoVFE/1.6/Defs didn't load");
                    if (def.modContentPack?.PackageId != TidyThreadsMod.PackageId || def.graphicData.texPath != "TidyThreads/Furniture/Wardrobe")
                        return Ok($"Table_Wardrobe comes from {def.modContentPack?.PackageId} with texPath {def.graphicData.texPath}");
                    int links = Def("Bed").GetCompProperties<CompProperties_AffectedByFacilities>()?.linkableFacilities
                        .Count(d => d == def) ?? 0;
                    return Ok(links == 1 ? null : $"the bed links the wardrobe {links} times, expected once");
                })
                .Do("four wardrobes, one per rotation", () =>
                {
                    problem = null;
                    CellRect site = f.Site(11, 4);
                    string[] stuffs = { "WoodLog", "Steel", "BlocksGranite", "WoodLog" };
                    Rot4[] rots = { Rot4.South, Rot4.East, Rot4.North, Rot4.West };
                    for (int i = 0; i < rots.Length; i++)
                        wardrobes.Add(f.Spawn(Def("Table_Wardrobe"), new IntVec3(site.minX + 1 + 3 * i, 0, site.minZ + 1),
                            Def(stuffs[i]), rots[i]).ThingID);
                })
                .Expect("every wardrobe is Critical apparel storage for 30",
                    () => Ok(wardrobes.Select(id => StorageProblem(Fixture.ById(id), 30)).FirstOrDefault(p => p != null)))
                .Expect("every rotation draws our texture", () => Ok(wardrobes.Select(id =>
                {
                    Thing w = Fixture.ById(id);
                    Material mat = w.Graphic.MatAt(w.Rotation, w);
                    string tex = mat?.mainTexture?.name;
                    return mat != null && mat != BaseContent.BadMat && tex != null && tex.StartsWith("Wardrobe_")
                        ? null : $"{w} facing {w.Rotation} draws '{tex ?? "nothing"}'";
                }).FirstOrDefault(p => p != null)))
                .Do("30 shirts into the first wardrobe", () =>
                {
                    Thing wardrobe = Fixture.ById(wardrobes[0]);
                    List<IntVec3> cells = wardrobe.OccupiedRect().Cells.ToList();
                    for (int i = 0; i < 30; i++)
                    {
                        Thing shirt = ThingMaker.MakeThing(Def("Apparel_BasicShirt"), Def("Cloth"));
                        GenSpawn.Spawn(scenario.Track(shirt), cells[i % cells.Count], Map);
                    }
                })
                .Expect("it holds 30, shown as 30 / 30, none drawn", () =>
                {
                    Thing wardrobe = Fixture.ById(wardrobes[0]);
                    int stored = StoredItems(wardrobe).Count;
                    if (stored != 30)
                        return Ok($"{stored} items on the wardrobe, expected 30");
                    if (!wardrobe.GetInspectString().Contains("30 / 30"))
                        return Ok($"inspect pane: '{wardrobe.GetInspectString().Replace('\n', ' ')}'");
                    bool? shown = ShowsContainedItems(wardrobe);
                    return Ok(shown == false ? null : $"ASF ShowContainedItems is {shown?.ToString() ?? "unreadable"}");
                })
                .Start();
        }
    }
}
