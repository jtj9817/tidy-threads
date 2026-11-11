using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TidyThreads
{
    /// <summary>Phase 4: dressers from saves made before the mod (T7, T13, T14), and the room role (T12).</summary>
    public static partial class Scenarios
    {
        /// <summary>The save <c>autotest.py run --premod</c> builds without the mod and loads with it.</summary>
        private const string PreModSave = "tidythreads_premod";

        /// <summary>One row of the pre-mod manifest (columns: AutoTest.PreModManifest).</summary>
        private sealed class PreModThing
        {
            public string tag, id, defName, stuff, rot, quality, title;
            public int x, z, hp;
        }

        /// <summary>
        /// The pre-mod things tagged <paramref name="tag"/>; throws unless this launch loaded the pre-mod save, so a
        /// scenario run on a quicktest map fails at its first step instead of passing on the wrong fixture.
        /// </summary>
        private static List<PreModThing> PreMod(string tag, string spec)
        {
            if (AutoTest.LoadedSave != PreModSave)
                throw new InvalidOperationException($"needs the pre-mod save: tools/autotest.py run --premod Source/TidyThreads/PreMod/{spec} \"<name>\"");
            return AutoTest.PreModManifest().Where(r => r[0] == tag).Select(r => new PreModThing
            {
                tag = r[0], id = r[1], defName = r[2], stuff = r[3], x = int.Parse(r[4]), z = int.Parse(r[5]), rot = r[6],
                hp = int.Parse(r[7]), quality = r[8], title = r[9],
            }).ToList();
        }

        /// <summary>Null when the converted dresser is the pre-mod one, unchanged; otherwise what differs.</summary>
        private static string Changed(PreModThing before)
        {
            Thing t = Fixture.ById(before.id);
            if (t == null)
                return $"{before.defName} {before.id} is gone";
            string quality = t.TryGetQuality(out QualityCategory q) ? q.ToString() : "-";
            string title = t.TryGetComp<CompArt>() is CompArt art && art.Active ? art.Title : "-";
            var diffs = new List<string>();
            if (t.Position.x != before.x || t.Position.z != before.z) diffs.Add($"position {t.Position} (was {before.x},{before.z})");
            if (t.Rotation.ToStringHuman() != before.rot) diffs.Add($"rotation {t.Rotation.ToStringHuman()} (was {before.rot})");
            if ((t.Stuff?.defName ?? "-") != before.stuff) diffs.Add($"stuff {t.Stuff?.defName} (was {before.stuff})");
            if (t.HitPoints != before.hp) diffs.Add($"HP {t.HitPoints} (was {before.hp})");
            if (quality != before.quality) diffs.Add($"quality {quality} (was {before.quality})");
            if (title != before.title) diffs.Add($"art '{title}' (was '{before.title}')");
            return diffs.Count == 0 ? null : $"{t}: {string.Join(", ", diffs)}";
        }

        /// <summary>
        /// T7. Precondition: the pre-mod save (Source/TidyThreads/PreMod/T7.txt), made without the mod, loaded with it:
        /// 2 vanilla dressers, one Excellent with art, and 15 loose shirts. Trigger: the load itself. Expected: both
        /// are converted (and logged) into Critical apparel storage for 20, with position, rotation, stuff, HP, quality
        /// and art title unchanged, and haulers move the 15 shirts in. Failure mode: class not swapped, empty
        /// settings, or lost quality or art.
        /// </summary>
        private static void T7Conversion()
        {
            List<PreModThing> dressers = null, shirts = null;
            var scenario = new Scenario("T7 Conversion", HaulTimeout);
            scenario.Summary(() => problem)
                .Do("the pre-mod save is loaded", () =>
                {
                    problem = null;
                    List<PreModThing> all = PreMod("T7", "T7.txt");
                    dressers = all.Where(t => t.defName == "Dresser").ToList();
                    shirts = all.Where(t => t.defName != "Dresser").ToList();
                    if (dressers.Count != 2 || shirts.Count != 15 || !dressers.Any(d => d.title != "-"))
                        throw new InvalidOperationException($"manifest: {dressers.Count} dressers ({dressers.Count(d => d.title != "-")} with art), {shirts.Count} shirts");
                    foreach (PreModThing t in dressers.Concat(shirts))
                        if (Fixture.ById(t.id) is Thing thing)
                            scenario.Track(thing);
                    RequireHaulers();
                })
                .Expect("both dressers are Critical apparel storage for 20",
                    () => Ok(dressers.Select(d => StorageProblem(Fixture.ById(d.id), 20)).FirstOrDefault(p => p != null)))
                .Expect("position, rotation, stuff, HP, quality and art are unchanged",
                    () => Ok(dressers.Select(Changed).FirstOrDefault(p => p != null)))
                .Expect("the converter logged both", () =>
                {
                    List<string> missing = dressers.Where(d => !LegacyDresserConverter.Converted.Contains(d.id)).Select(d => d.id).ToList();
                    return Ok(missing.Count == 0 ? null : $"no conversion recorded for {string.Join(", ", missing)}");
                })
                .Expect("haulers move the 15 shirts in", () =>
                {
                    int stored = dressers.Sum(d => CountOn(Fixture.ById(d.id), shirts.Select(s => s.id)));
                    problem = $"{stored}/15 shirts in the dressers";
                    return stored == 15;
                })
                .Start();
        }

        /// <summary>
        /// T13. Precondition: the pre-mod save (PreMod/T13-T14.txt) loaded with the mod: 2 converted dressers tagged T13.
        /// Trigger: each gets 20 shirts; one is uninstalled and the other deconstructed. Expected: all 40 shirts are
        /// still there (on the ground nearby, or in a hauler's hands), none destroyed, and the uninstalled dresser is a
        /// minified thing. Failure mode: contents vanish (the 1.6 outfit stand bug).
        /// </summary>
        private static void T13UninstallAndDeconstructWhileFull()
        {
            List<PreModThing> dressers = null;
            var shirts = new List<Thing>();
            var origins = new List<IntVec3>();
            var scenario = new Scenario("T13 Uninstall and deconstruct while full", 3000);
            scenario.Summary(() => problem)
                .Do("2 converted dressers, 20 shirts in each", () =>
                {
                    problem = null;
                    dressers = PreMod("T13", "T13-T14.txt");
                    if (dressers.Count != 2)
                        throw new InvalidOperationException($"manifest: {dressers.Count} T13 dressers, expected 2");
                    foreach (PreModThing d in dressers)
                    {
                        Thing dresser = Fixture.ById(d.id) ?? throw new InvalidOperationException($"dresser {d.id} is gone");
                        origins.Add(dresser.Position);
                        List<IntVec3> cells = dresser.OccupiedRect().Cells.ToList();
                        for (int i = 0; i < 20; i++)
                        {
                            Thing shirt = MakeApparel("Apparel_BasicShirt");
                            GenSpawn.Spawn(scenario.Track(shirt), cells[i % cells.Count], Map);
                            shirts.Add(shirt);
                        }
                    }
                })
                .Expect("both are converted and full", () => Ok(dressers.Select(d =>
                {
                    Thing dresser = Fixture.ById(d.id);
                    return StorageProblem(dresser, 20) ?? (StoredItems(dresser).Count == 20 ? null : $"{dresser} holds {StoredItems(dresser).Count}");
                }).FirstOrDefault(p => p != null)))
                .Do("uninstall the first, deconstruct the second", () =>
                {
                    scenario.Track(Fixture.ById(dressers[0].id).Uninstall());
                    Fixture.ById(dressers[1].id).Destroy(DestroyMode.Deconstruct);
                })
                .Expect("all 40 shirts lie near their dresser's spot", () =>
                {
                    int destroyed = shirts.Count(t => t.Destroyed);
                    int far = shirts.Where(t => t.Spawned).Count(t => !shirts.IndexOf(t).Equals(-1)
                        && !t.Position.InHorDistOf(origins[shirts.IndexOf(t) / 20], 8f));
                    return Ok(destroyed == 0 && far == 0 ? null : $"{destroyed} destroyed, {far} more than 8 cells away");
                })
                .Wait(600)
                .Expect("none deleted after haulers had time", () =>
                {
                    int destroyed = shirts.Count(t => t.Destroyed);
                    int lost = shirts.Count(t => !t.Destroyed && !t.Spawned && !(t.ParentHolder is Pawn_CarryTracker) && !(t.ParentHolder is Pawn_InventoryTracker));
                    return Ok(destroyed == 0 && lost == 0 ? null : $"{destroyed} destroyed, {lost} neither spawned nor carried");
                })
                .Start();
        }

        /// <summary>
        /// T14. Precondition: the pre-mod save (PreMod/T13-T14.txt) loaded with the mod: 2 converted dressers tagged T14.
        /// Trigger: 5 shirts in each, the first set to Normal, then save and reload twice. Expected: each holds the same
        /// shirts; the first stays Normal and the second Critical; no dresser is converted again. Failure mode: the
        /// converter re-runs and overwrites a player's priority.
        /// </summary>
        private static void T14SaveAndReload()
        {
            List<PreModThing> dressers = null;
            var contents = new List<HashSet<string>>();
            int convertedBefore = 0;
            var scenario = new Scenario("T14 Save and reload", 6000);
            scenario.Summary(() => problem)
                .Do("2 converted dressers, 5 shirts each, the first set to Normal", () =>
                {
                    problem = null;
                    dressers = PreMod("T14", "T13-T14.txt");
                    if (dressers.Count != 2)
                        throw new InvalidOperationException($"manifest: {dressers.Count} T14 dressers, expected 2");
                    foreach (PreModThing d in dressers)
                    {
                        Thing dresser = Fixture.ById(d.id) ?? throw new InvalidOperationException($"dresser {d.id} is gone");
                        if (StorageProblem(dresser, 20) is string why)
                            throw new InvalidOperationException(why);
                        List<IntVec3> cells = dresser.OccupiedRect().Cells.ToList();
                        var ids = new HashSet<string>();
                        for (int i = 0; i < 5; i++)
                            ids.Add(Put(scenario, MakeApparel("Apparel_BasicShirt"), cells[i % cells.Count]));
                        contents.Add(ids);
                    }
                    ((Building_Storage)Fixture.ById(dressers[0].id)).GetStoreSettings().Priority = StoragePriority.Normal;
                    convertedBefore = LegacyDresserConverter.Converted.Count;
                })
                .SaveAndReload()
                .SaveAndReload()
                .Expect("same contents; Normal and Critical kept; nothing converted again", () =>
                {
                    for (int i = 0; i < dressers.Count; i++)
                    {
                        Thing dresser = Fixture.ById(dressers[i].id);
                        StoragePriority want = i == 0 ? StoragePriority.Normal : StoragePriority.Critical;
                        if (StorageProblem(dresser, 20, want) is string why)
                            return Ok(why);
                        var now = new HashSet<string>(StoredItems(dresser).Select(t => t.ThingID));
                        if (!now.SetEquals(contents[i]))
                            return Ok($"{dresser} holds {now.Count} items, {now.Intersect(contents[i]).Count()} of its 5 shirts");
                    }
                    int again = LegacyDresserConverter.Converted.Count - convertedBefore;
                    return Ok(again == 0 ? null : $"{again} dressers converted again on reload");
                })
                .Start();
        }
        /// <summary>
        /// T12 (decisions 8, 9). Precondition: a clear site. Trigger: two walled rooms, one with a bed, 2 dressers and a
        /// wardrobe, the other with only 2 dressers and a wardrobe. Expected: the first is a Bedroom (the bed wins over the
        /// storage furniture) and the second a Storeroom. Failure mode: a room's role is wrong because of the apparel
        /// furniture.
        /// </summary>
        private static void T12BedroomAndComfort()
        {
            var f = new Fixture();
            List<FixtureRoom> rooms = null;
            var scenario = new Scenario("T12 Bedroom and comfort", 2000).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Do("a bedroom and a dressers-only room", () =>
                {
                    problem = null;
                    rooms = f.Rooms(2, 7, 5, RoomPower.None);
                    for (int i = 0; i < rooms.Count; i++)
                    {
                        CellRect inside = rooms[i].Inside;
                        f.Spawn(Def("Dresser"), new IntVec3(inside.minX + 2, 0, inside.maxZ), Def("WoodLog"), Rot4.South);
                        f.Spawn(Def("Dresser"), new IntVec3(inside.minX + 4, 0, inside.maxZ), Def("WoodLog"), Rot4.South);
                        f.Spawn(Def("Table_Wardrobe"), new IntVec3(inside.minX + 4, 0, inside.minZ + 1), Def("WoodLog"), Rot4.South);
                        if (i == 0)
                            f.Spawn(Def("Bed"), new IntVec3(inside.minX, 0, inside.minZ + 1), Def("WoodLog"), Rot4.North);
                    }
                    Map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
                })
                .Expect("the furniture is apparel storage in both rooms", () => Ok(rooms.SelectMany(r => r.Room.ContainedAndAdjacentThings)
                    .Where(t => t.def.defName == "Dresser" || t.def.defName == "Table_Wardrobe")
                    .Select(t => StorageProblem(t, t.def.defName == "Dresser" ? 20 : 30)).FirstOrDefault(p => p != null)))
                .Expect("the bed's room is a Bedroom and the other a Storeroom", () =>
                {
                    RoomRoleDef first = rooms[0].Room.Role, second = rooms[1].Room.Role;
                    return Ok(first == RoomRoleDefOf.Bedroom && second?.defName == "Storeroom"
                        ? null : $"roles: {first?.defName ?? "none"} with the bed, {second?.defName ?? "none"} without");
                })
                .Start();
        }
    }
}
