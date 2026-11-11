using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TidyThreads
{
    /// <summary>
    /// Phase 5: the wardrobe with VFE, and VFE added to or removed from a save (T15, T16, T17). T15 and T16 run in two
    /// halves through <c>autotest.py run --switch-mods</c>: the first builds and saves the handover save, the second
    /// loads it under the other mod set (<see cref="AutoTest.LoadedSave"/> tells them apart).
    /// </summary>
    public static partial class Scenarios
    {
        /// <summary>The save <c>autotest.py run --switch-mods</c> loads for a scenario's second half.</summary>
        private const string HandoverSave = "tidythreads_handover";
        private const string VfeWardrobeTexPath = "NewThings/Furniture/Wardrobe/Wardrobe";
        private const string OurWardrobeTexPath = "TidyThreads/Furniture/Wardrobe";

        private static bool SecondHalf => AutoTest.LoadedSave == HandoverSave;

        private static string HandoverFile(string scenario) =>
            Path.Combine(AutoTest.Dir ?? throw new InvalidOperationException("needs autotest.py run --switch-mods"),
                $"handover-{scenario.Split(' ')[0]}.txt");

        /// <summary>Null when Table_Wardrobe is VFE's (or ours when <paramref name="vfe"/> is false), with that texture.</summary>
        private static string WardrobeDefProblem(bool vfe)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail("Table_Wardrobe");
            if (def == null)
                return "no Table_Wardrobe def";
            string pack = def.modContentPack?.PackageId ?? "none";
            bool fromVfe = string.Equals(pack, VfeCore, StringComparison.OrdinalIgnoreCase);
            string tex = def.graphicData?.texPath;
            if (fromVfe != vfe || tex != (vfe ? VfeWardrobeTexPath : OurWardrobeTexPath))
                return $"Table_Wardrobe comes from {pack} with texPath {tex}; expected {(vfe ? "VFE's" : "ours")}";
            return null;
        }

        /// <summary>Null when <paramref name="thing"/> draws a real texture (not the missing-texture material).</summary>
        private static string DrawProblem(Thing thing)
        {
            Material mat = thing.Graphic.MatAt(thing.Rotation, thing);
            return mat != null && mat != BaseContent.BadMat && mat.mainTexture != null ? null : $"{thing} draws no texture";
        }

        /// <summary>
        /// T15 (<paramref name="vfeFirst"/> false: VFE off, then on) and T16 (true: VFE on, then off). First half: two
        /// wardrobes of the active def, 30 shirts each, the first set to Normal, then the handover save. Second half, on
        /// that save under the other mod set: both wardrobes are the other def with its texture, hold the same 30 shirts,
        /// and keep Normal and Critical; the harness fails the run on any new red error. Failure mode: a defName mismatch
        /// deletes wardrobes, settings are lost, or (T16) leftover VFE data raises red errors.
        /// </summary>
        private static void WardrobeAcrossVfe(string name, bool vfeFirst)
        {
            if (!SecondHalf)
                WardrobeFirstHalf(name, vfeFirst);
            else
                WardrobeSecondHalf(name, !vfeFirst);
        }

        private static void WardrobeFirstHalf(string name, bool vfe)
        {
            var f = new Fixture();
            var wardrobes = new List<string>();
            var contents = new List<List<string>>();
            var scenario = new Scenario(name, 2000).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Expect($"first half: VFE {(vfe ? "on" : "off")}, the wardrobe is {(vfe ? "VFE's" : "ours")}", () =>
                    Ok(VfeActive != vfe ? $"VFE is {(vfe ? "off" : "on")}: run with --switch-mods" : WardrobeDefProblem(vfe)))
                .Do("2 wardrobes, 30 shirts each, the first set to Normal", () =>
                {
                    problem = null;
                    CellRect site = f.Site(7, 3);
                    for (int i = 0; i < 2; i++)
                    {
                        Thing w = f.Spawn(Def("Table_Wardrobe"), new IntVec3(site.minX + 2 + 3 * i, 0, site.minZ + 1), Def("WoodLog"), Rot4.South);
                        wardrobes.Add(w.ThingID);
                        List<IntVec3> cells = w.OccupiedRect().Cells.ToList();
                        var ids = new List<string>();
                        for (int n = 0; n < 30; n++)
                            ids.Add(Put(scenario, MakeApparel("Apparel_BasicShirt"), cells[n % cells.Count]));
                        contents.Add(ids);
                    }
                    ((Building_Storage)Fixture.ById(wardrobes[0])).GetStoreSettings().Priority = StoragePriority.Normal;
                })
                .Expect("both hold 30 at their priority", () => Ok(wardrobes.Select((id, i) =>
                {
                    Thing w = Fixture.ById(id);
                    return StorageProblem(w, 30, i == 0 ? StoragePriority.Normal : StoragePriority.Critical)
                        ?? (CountOn(w, contents[i]) == 30 ? null : $"{w} holds {CountOn(w, contents[i])}/30 of its shirts");
                }).FirstOrDefault(p => p != null)))
                .Do("write the handover save", () =>
                {
                    File.WriteAllLines(HandoverFile(name), wardrobes.Select((id, i) => $"{id}\t{string.Join(",", contents[i])}"));
                    GameDataSaveLoader.SaveGame(HandoverSave);
                })
                .Expect("the handover save exists", () => Ok(File.Exists(GenFilePaths.FilePathForSavedGame(HandoverSave)) ? null : "no handover save"))
                .Start();
        }

        private static void WardrobeSecondHalf(string name, bool vfe)
        {
            List<(string id, HashSet<string> shirts)> before = null;
            var scenario = new Scenario(name, 2000);
            scenario.Summary(() => problem)
                .Expect($"second half: VFE {(vfe ? "on" : "off")}, the wardrobe is {(vfe ? "VFE's" : "ours")}", () =>
                {
                    if (VfeActive != vfe)
                        return Ok($"VFE is {(vfe ? "off" : "on")} in the second half");
                    before = File.ReadAllLines(HandoverFile(name)).Where(l => l.Length > 0).Select(l => l.Split('\t'))
                        .Select(p => (p[0], new HashSet<string>(p[1].Split(',')))).ToList();
                    foreach (Thing t in before.SelectMany(w => w.shirts.Append(w.id)).Select(Fixture.ById).Where(t => t != null))
                        scenario.Track(t);
                    return Ok(WardrobeDefProblem(vfe));
                })
                .Expect("both wardrobes survived with their texture, shirts and priority", () => Ok(before.Select((w, i) =>
                {
                    Thing wardrobe = Fixture.ById(w.id);
                    if (wardrobe == null)
                        return $"wardrobe {w.id} is gone";
                    string why = StorageProblem(wardrobe, 30, i == 0 ? StoragePriority.Normal : StoragePriority.Critical) ?? DrawProblem(wardrobe);
                    if (why != null)
                        return why;
                    var now = new HashSet<string>(StoredItems(wardrobe).Select(t => t.ThingID));
                    return now.SetEquals(w.shirts) ? null : $"{wardrobe} holds {now.Count}, {now.Intersect(w.shirts).Count()} of its 30 shirts";
                }).FirstOrDefault(p => p != null)))
                .Start();
        }

        private static void T15AddVfeToASave() => WardrobeAcrossVfe("T15 Add VFE to a save", vfeFirst: false);

        private static void T16RemoveVfeFromASave() => WardrobeAcrossVfe("T16 Remove VFE from a save", vfeFirst: true);

        /// <summary>
        /// T17. Precondition: VFE on, a fresh quicktest map, a clear site. Trigger: build a wardrobe and put 30 shirts in.
        /// Expected: Table_Wardrobe is VFE's def with VFE's texture; exactly one build designator places it; this mod
        /// loaded no NoVFE folder and defines no ThingDef of its own; the wardrobe is Critical apparel storage holding
        /// 30. Failure mode: two wardrobe defs, a texture path collision, or wrong capacity.
        /// </summary>
        private static void T17FreshGameWithVfe()
        {
            var f = new Fixture();
            string wardrobeId = null;
            var shirts = new List<string>();
            var scenario = new Scenario("T17 Fresh game with VFE", 2000).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Expect("VFE is on and Table_Wardrobe is VFE's", () =>
                    Ok(!VfeActive ? "VFE is off: T17 needs MOD_EXTRA_MODS with VFE" : WardrobeDefProblem(vfe: true)))
                .Expect("one wardrobe in Architect; no NoVFE folder or ThingDef of ours loaded", () =>
                {
                    int designators = DefDatabase<DesignationCategoryDef>.AllDefs.SelectMany(c => c.AllResolvedDesignators)
                        .OfType<Designator_Build>().Count(d => d.PlacingDef is ThingDef td && td.defName == "Table_Wardrobe");
                    if (designators != 1)
                        return Ok($"{designators} build designators place Table_Wardrobe");
                    ModContentPack ours = LoadedModManager.GetMod<TidyThreadsMod>().Content;
                    string noVfe = ours.foldersToLoadDescendingOrder.FirstOrDefault(d => d.Replace('\\', '/').Contains("/NoVFE"));
                    if (noVfe != null)
                        return Ok($"our load folders include {noVfe}");
                    ThingDef mine = DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d => d.modContentPack == ours);
                    return Ok(mine == null ? null : $"this mod defines ThingDef {mine.defName} with VFE on");
                })
                .Do("build a wardrobe and put 30 shirts in", () =>
                {
                    problem = null;
                    CellRect site = f.Site(4, 3);
                    Thing w = f.Spawn(Def("Table_Wardrobe"), new IntVec3(site.minX + 2, 0, site.minZ + 1), Def("WoodLog"), Rot4.South);
                    wardrobeId = w.ThingID;
                    List<IntVec3> cells = w.OccupiedRect().Cells.ToList();
                    for (int n = 0; n < 30; n++)
                        shirts.Add(Put(scenario, MakeApparel("Apparel_BasicShirt"), cells[n % cells.Count]));
                })
                .Expect("it is Critical apparel storage holding 30, with VFE's texture", () =>
                {
                    Thing w = Fixture.ById(wardrobeId);
                    string why = StorageProblem(w, 30) ?? DrawProblem(w);
                    if (why != null)
                        return Ok(why);
                    return Ok(CountOn(w, shirts) == 30 && w.GetInspectString().Contains("30 / 30")
                        ? null : $"{CountOn(w, shirts)}/30 shirts; inspect '{w.GetInspectString().Replace('\n', ' ')}'");
                })
                .Start();
        }
    }
}
