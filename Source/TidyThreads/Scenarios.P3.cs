using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace TidyThreads
{
    /// <summary>
    /// Phase 3: colonists haul apparel into the furniture (T5, T6, T9, T10, T11). These run live: real haulers on the
    /// quicktest map, so each waits on the outcome with a timeout of thousands of ticks.
    /// </summary>
    public static partial class Scenarios
    {
        private const int HaulTimeout = 30000;

        /// <summary>An apparel item of <paramref name="defName"/>, from its default stuff when it takes one.</summary>
        private static Apparel MakeApparel(string defName)
        {
            ThingDef def = Def(defName);
            return (Apparel)ThingMaker.MakeThing(def, def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null);
        }

        /// <summary>Spawns <paramref name="thing"/> on <paramref name="cell"/> and tracks it; its ThingID.</summary>
        private static string Put(Scenario scenario, Thing thing, IntVec3 cell) =>
            GenSpawn.Spawn(scenario.Track(thing), cell, Map).ThingID;

        /// <summary>Throws unless a free, undrafted colonist has Hauling enabled: the precondition of every live haul.</summary>
        private static void RequireHaulers()
        {
            if (!Map.mapPawns.FreeColonistsSpawned.Any(p => !p.Drafted && !p.Downed
                    && p.workSettings != null && p.workSettings.WorkIsActive(WorkTypeDefOf.Hauling)))
                throw new InvalidOperationException("no free colonist with Hauling enabled");
        }

        /// <summary>How many of <paramref name="ids"/> lie on <paramref name="building"/>'s cells.</summary>
        private static int CountOn(Thing building, IEnumerable<string> ids)
        {
            HashSet<string> on = new HashSet<string>(StoredItems(building).Select(t => t.ThingID));
            return ids.Count(on.Contains);
        }

        /// <summary>How many of <paramref name="ids"/> are spawned (not carried, not destroyed).</summary>
        private static int CountSpawned(IEnumerable<string> ids) => ids.Count(id => Fixture.ById(id) != null);

        /// <summary>
        /// An apparel-only stockpile zone on <paramref name="cells"/> at <paramref name="priority"/>; the fixture kit doesn't
        /// own zones, so the scenario deletes it in <c>OnFinish</c>.
        /// </summary>
        private static Zone_Stockpile ApparelStockpile(IEnumerable<IntVec3> cells, StoragePriority priority)
        {
            var zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Map.zoneManager);
            Map.zoneManager.RegisterZone(zone);
            foreach (IntVec3 c in cells)
                zone.AddCell(c);
            zone.settings.filter.SetDisallowAll();
            zone.settings.filter.SetAllow(ThingCategoryDefOf.Apparel, true);
            zone.settings.Priority = priority;
            return zone;
        }

        /// <summary>
        /// T5. Precondition: a clear site and a free hauler. Trigger: 25 loose apparel (5 each of parka, tribalwear, flak
        /// vest, simple helmet and shield belt; one tainted, one at 10% HP) beside an empty dresser. Expected: every item is
        /// accepted; haulers store exactly 20 and 5 stay outside, also after a further wait. Failure mode: capacity
        /// overflow, or a subtype (tainted, damaged, armour, utility) rejected.
        /// </summary>
        private static void T5LooseApparelMixedTypes()
        {
            var f = new Fixture();
            var items = new List<string>();
            string dresserId = null;
            string[] types = { "Apparel_Parka", "Apparel_TribalA", "Apparel_FlakVest", "Apparel_SimpleHelmet", "Apparel_ShieldBelt" };
            var scenario = new Scenario("T5 Loose apparel mixed types", HaulTimeout).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Do("a dresser and 25 loose apparel beside it", () =>
                {
                    problem = null;
                    RequireHaulers();
                    CellRect site = f.Site(7, 8);
                    dresserId = f.Spawn(Def("Dresser"), new IntVec3(site.minX + 3, 0, site.maxZ - 1), Def("WoodLog"), Rot4.South).ThingID;
                    for (int i = 0; i < 25; i++)
                    {
                        Apparel item = MakeApparel(types[i % types.Length]);
                        if (i == 0)
                            item.WornByCorpse = true;
                        if (i == 1)
                            item.HitPoints = Math.Max(1, item.MaxHitPoints / 10);
                        items.Add(Put(scenario, item, new IntVec3(site.minX + 1 + i % 5, 0, site.minZ + 1 + i / 5)));
                    }
                })
                .Expect("the dresser is Critical apparel storage and accepts all 25 (tainted and damaged too)", () =>
                {
                    var dresser = (Building_Storage)Fixture.ById(dresserId);
                    if (StorageProblem(dresser, 20) is string why)
                        return Ok(why);
                    Thing refused = items.Select(Fixture.ById).FirstOrDefault(t => !dresser.Accepts(t));
                    return Ok(refused == null ? null : $"the dresser refuses {refused} (tainted {(refused as Apparel)?.WornByCorpse}, "
                        + $"HP {refused.HitPoints}/{refused.MaxHitPoints})");
                })
                .Expect("haulers store 20 and leave 5 outside", () =>
                {
                    Thing dresser = Fixture.ById(dresserId);
                    int stored = StoredItems(dresser).Count, ours = CountOn(dresser, items), spawned = CountSpawned(items);
                    problem = $"{stored} on the dresser ({ours} ours), {spawned - ours} of ours outside, {items.Count - spawned} carried";
                    return stored == 20 && ours == 20 && spawned == 25;
                })
                .Wait(1500)
                .Expect("still 20 stored and 5 outside", () =>
                {
                    Thing dresser = Fixture.ById(dresserId);
                    int stored = StoredItems(dresser).Count, spawned = CountSpawned(items);
                    return Ok(stored == 20 && spawned == 25 && dresser.GetInspectString().Contains("20 / 20")
                        ? null : $"after the wait: {stored} stored, {spawned} of 25 spawned");
                })
                .Start();
        }

        /// <summary>
        /// T6. Precondition: a clear site, a free hauler; 10 apparel in a Normal apparel stockpile and 6 on a Preferred
        /// shelf, both settled. Trigger: a dresser is built. Expected: all 16 move into it, and none move back over a
        /// further wait. Failure mode: Critical priority not honoured, or a hauling loop between the storages.
        /// </summary>
        private static void T6FromLowerStorage()
        {
            var f = new Fixture();
            var items = new List<string>();
            Zone_Stockpile zone = null;
            string shelfId = null, dresserId = null;
            CellRect site = default;
            var scenario = new Scenario("T6 From lower storage", HaulTimeout).OnFinish(f.CleanUp).OnFinish(() => zone?.Delete());
            scenario.Summary(() => problem)
                .Do("10 apparel in a Normal stockpile, 6 on a Preferred shelf", () =>
                {
                    problem = null;
                    RequireHaulers();
                    site = f.Site(10, 6);
                    List<IntVec3> zoneCells = Enumerable.Range(0, 10)
                        .Select(i => new IntVec3(site.minX + i % 5, 0, site.minZ + i / 5)).ToList();
                    zone = ApparelStockpile(zoneCells, StoragePriority.Normal);
                    foreach (IntVec3 c in zoneCells)
                        items.Add(Put(scenario, MakeApparel("Apparel_BasicShirt"), c));
                    var shelf = (Building_Storage)f.Spawn(Def("Shelf"), new IntVec3(site.minX + 7, 0, site.minZ), Def("WoodLog"), Rot4.South);
                    shelfId = shelf.ThingID;
                    StorageSettings settings = shelf.GetStoreSettings();
                    settings.filter.SetDisallowAll();
                    settings.filter.SetAllow(ThingCategoryDefOf.Apparel, true);
                    settings.Priority = StoragePriority.Preferred;
                    List<IntVec3> shelfCells = shelf.OccupiedRect().Cells.ToList();
                    for (int i = 0; i < 6; i++)
                        items.Add(Put(scenario, MakeApparel("Apparel_Pants"), shelfCells[i % shelfCells.Count]));
                })
                .Wait(300)
                .Expect("they stay put before the dresser exists", () =>
                {
                    int onShelf = CountOn(Fixture.ById(shelfId), items.Skip(10));
                    int inZone = items.Take(10).Count(id => Fixture.ById(id) is Thing t && zone.ContainsCell(t.Position));
                    return Ok(onShelf == 6 && inZone == 10 ? null : $"before the dresser: {inZone}/10 in the stockpile, {onShelf}/6 on the shelf");
                })
                .Do("build a dresser", () => dresserId = f.Spawn(Def("Dresser"), new IntVec3(site.minX + 7, 0, site.minZ + 3),
                    Def("WoodLog"), Rot4.South).ThingID)
                .Expect("all 16 move into the dresser", () =>
                {
                    int ours = CountOn(Fixture.ById(dresserId), items);
                    problem = $"{ours}/16 in the dresser, {items.Count - CountSpawned(items)} carried";
                    return ours == 16;
                })
                .Wait(2000)
                .Expect("none moved back", () =>
                {
                    int ours = CountOn(Fixture.ById(dresserId), items), spawned = CountSpawned(items);
                    return Ok(ours == 16 && spawned == 16 ? null : $"after the wait: {ours}/16 in the dresser, {spawned}/16 spawned");
                })
                .Start();
        }

        /// <summary>
        /// T9. Precondition: a clear site, a free hauler, no other storage for the items. Trigger: steel, meals and a rifle
        /// beside an empty dresser, plus one shirt as a control that haulers are working. Expected: the filter's root is
        /// Apparel, the dresser refuses the three; the shirt is hauled in and none of the three over a further wait.
        /// Failure mode: the fixed filter isn't enforced.
        /// </summary>
        private static void T9NonApparelRejected()
        {
            var f = new Fixture();
            var others = new List<string>();
            string dresserId = null, shirtId = null;
            var scenario = new Scenario("T9 Non-apparel rejected", HaulTimeout).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Do("a dresser with steel, meals, a rifle and a shirt beside it", () =>
                {
                    problem = null;
                    RequireHaulers();
                    CellRect site = f.Site(6, 5);
                    dresserId = f.Spawn(Def("Dresser"), new IntVec3(site.minX + 2, 0, site.maxZ - 1), Def("WoodLog"), Rot4.South).ThingID;
                    Thing steel = ThingMaker.MakeThing(ThingDefOf.Steel);
                    steel.stackCount = 75;
                    Thing meals = ThingMaker.MakeThing(Def("MealSimple"));
                    meals.stackCount = 10;
                    Thing rifle = ThingMaker.MakeThing(Def("Gun_BoltActionRifle"));
                    Thing[] things = { steel, meals, rifle };
                    for (int i = 0; i < things.Length; i++)
                        others.Add(Put(scenario, things[i], new IntVec3(site.minX + 1 + i, 0, site.minZ + 1)));
                    shirtId = Put(scenario, MakeApparel("Apparel_BasicShirt"), new IntVec3(site.minX + 4, 0, site.minZ + 1));
                })
                .Expect("the filter lists only Apparel and the dresser refuses all three", () =>
                {
                    var dresser = (Building_Storage)Fixture.ById(dresserId);
                    if (StorageProblem(dresser, 20) is string why)
                        return Ok(why);
                    ThingCategoryDef root = dresser.GetParentStoreSettings().filter.DisplayRootCategory?.catDef;
                    if (root != ThingCategoryDefOf.Apparel)
                        return Ok($"the filter's root category is {root?.defName ?? "none"}, not Apparel");
                    Thing accepted = others.Select(Fixture.ById).FirstOrDefault(dresser.Accepts);
                    return Ok(accepted == null ? null : $"the dresser accepts {accepted}");
                })
                .Expect("the shirt is hauled in (haulers are working)", () =>
                {
                    problem = "the control shirt hasn't been hauled in";
                    return CountOn(Fixture.ById(dresserId), new[] { shirtId }) == 1;
                })
                .Wait(2000)
                .Expect("none of the three was hauled in", () =>
                {
                    Thing dresser = Fixture.ById(dresserId);
                    List<Thing> wrong = StoredItems(dresser).Where(t => !t.def.IsApparel).ToList();
                    int spawned = CountSpawned(others);
                    return Ok(wrong.Count == 0 && spawned == 3
                        ? null : $"non-apparel on the dresser: {string.Join(", ", wrong)}; {spawned}/3 spawned");
                })
                .Start();
        }

        /// <summary>
        /// T10. Precondition: a clear site, a free hauler. Trigger: 2 dressers, 1 wardrobe and 1 shelf are selected and the
        /// first dresser's Link gizmo is used; the shelf with one dresser offers no link; then 80 apparel are dropped
        /// beside them. Expected: the three apparel members share one group and the shelf stays out; the group holds 70
        /// and 10 stay outside. Failure mode: a wrong storageGroupTag, or the group's capacity wrong.
        /// </summary>
        private static void T10LinkingAndCapacity()
        {
            var f = new Fixture();
            var members = new List<string>();
            var items = new List<string>();
            string shelfId = null;
            CellRect site = default;
            var scenario = new Scenario("T10 Linking and capacity", HaulTimeout * 2).OnFinish(f.CleanUp)
                .OnFinish(() => Find.Selector.ClearSelection());
            scenario.Summary(() => problem)
                .Do("2 dressers, 1 wardrobe and 1 shelf", () =>
                {
                    problem = null;
                    RequireHaulers();
                    site = f.Site(12, 6);
                    string[] defs = { "Dresser", "Dresser", "Table_Wardrobe" };
                    for (int i = 0; i < defs.Length; i++)
                        members.Add(f.Spawn(Def(defs[i]), new IntVec3(site.minX + 1 + 3 * i, 0, site.maxZ - 1), Def("WoodLog"), Rot4.South).ThingID);
                    var shelf = (Building_Storage)f.Spawn(Def("Shelf"), new IntVec3(site.minX + 10, 0, site.maxZ - 1), Def("WoodLog"), Rot4.South);
                    shelfId = shelf.ThingID;
                    // Kept empty, so the leftover apparel has nowhere else to go.
                    shelf.GetStoreSettings().filter.SetDisallowAll();
                })
                .Do("select all four and press the first dresser's Link", () =>
                {
                    Find.Selector.ClearSelection();
                    foreach (string id in members.Append(shelfId))
                        Find.Selector.Select(Fixture.ById(id), playSound: false);
                    LinkGizmo(Fixture.ById(members[0])).action();
                })
                .Expect("the three share one group and the shelf stays out", () =>
                {
                    List<StorageGroup> groups = members.Select(id => ((IStorageGroupMember)Fixture.ById(id)).Group).ToList();
                    if (groups.Any(g => g == null) || groups.Distinct().Count() != 1)
                        return Ok($"groups after linking: {string.Join(", ", groups.Select(g => g?.loadID.ToString() ?? "none"))}");
                    var shelf = (IStorageGroupMember)Fixture.ById(shelfId);
                    if (shelf.Group != null)
                        return Ok($"the shelf joined group {shelf.Group.loadID} (tag {shelf.StorageGroupTag})");
                    return Ok(groups[0].members.Count == 3 && groups[0].GetStoreSettings().Priority == StoragePriority.Critical
                        ? null : $"the group has {groups[0].members.Count} members at {groups[0].GetStoreSettings().Priority}");
                })
                .Expect("a dresser and the shelf alone get no usable link", () =>
                {
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(Fixture.ById(members[0]), playSound: false);
                    Find.Selector.Select(Fixture.ById(shelfId), playSound: false);
                    Command shelfLink = LinkGizmo(Fixture.ById(shelfId));
                    Find.Selector.ClearSelection();
                    return Ok(shelfLink.Disabled ? null : "the shelf offers to link with a dresser");
                })
                .Do("80 apparel beside them", () =>
                {
                    for (int i = 0; i < 80; i++)
                        items.Add(Put(scenario, MakeApparel("Apparel_BasicShirt"), new IntVec3(site.minX + 1 + i / 4 % 10, 0, site.minZ + 1 + i / 40)));
                })
                .Expect("the group holds 70 and 10 stay outside", () =>
                {
                    int stored = members.Sum(id => CountOn(Fixture.ById(id), items)), spawned = CountSpawned(items);
                    problem = $"{stored}/70 in the group ({string.Join("+", members.Select(id => CountOn(Fixture.ById(id), items)))}), "
                        + $"{spawned - stored} outside, {items.Count - spawned} carried";
                    return stored == 70 && spawned == 80;
                })
                .Wait(1000)
                .Expect("still 70 after a wait", () =>
                {
                    int stored = members.Sum(id => StoredItems(Fixture.ById(id)).Count);
                    return Ok(stored == 70 && CountSpawned(items) == 80 ? null : $"after the wait: {stored} in the group");
                })
                .Start();
        }

        /// <summary>The vanilla "Link storage settings" gizmo of <paramref name="member"/>, for the current selection.</summary>
        private static Command_Action LinkGizmo(Thing member) =>
            member.GetGizmos().OfType<Command_Action>().FirstOrDefault(c => c.defaultLabel == "LinkStorageSettings".Translate().RawText)
            ?? throw new InvalidOperationException($"{member} has no Link storage settings gizmo");

        /// <summary>
        /// T11. Precondition: a clear site, a free hauler; 6 apparel in a Normal stockpile. Trigger: a dresser walled in on
        /// every side. Expected: no colonist can reach it and the store finder never picks it; over a fixed window no job
        /// targets it, the apparel stays in the stockpile, and no errors (vanilla logs one when a pawn loops on jobs).
        /// Failure mode: a job loop or errors on an unreachable target.
        /// </summary>
        private static void T11UnreachableDresser()
        {
            var f = new Fixture();
            var items = new List<string>();
            var jobs = new HashSet<int>();
            Zone_Stockpile zone = null;
            string dresserId = null;
            int watchUntil = 0;
            var scenario = new Scenario("T11 Unreachable dresser", HaulTimeout).OnFinish(f.CleanUp).OnFinish(() => zone?.Delete());
            scenario.Summary(() => problem)
                .Do("6 apparel in a Normal stockpile and a walled-in dresser", () =>
                {
                    problem = null;
                    RequireHaulers();
                    CellRect site = f.Site(9, 5);
                    List<IntVec3> zoneCells = Enumerable.Range(0, 6)
                        .Select(i => new IntVec3(site.minX + i % 3, 0, site.minZ + 1 + i / 3)).ToList();
                    zone = ApparelStockpile(zoneCells, StoragePriority.Normal);
                    foreach (IntVec3 c in zoneCells)
                        items.Add(Put(scenario, MakeApparel("Apparel_BasicShirt"), c));
                    Thing dresser = f.Spawn(Def("Dresser"), new IntVec3(site.minX + 6, 0, site.minZ + 2), Def("WoodLog"), Rot4.South);
                    dresserId = dresser.ThingID;
                    CellRect ring = dresser.OccupiedRect().ExpandedBy(1);
                    f.Evacuate(ring);
                    foreach (IntVec3 c in ring.EdgeCells)
                        f.Spawn(ThingDefOf.Wall, c, ThingDefOf.Steel);
                    Map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
                })
                .Expect("no colonist can reach it and the store finder skips it", () =>
                {
                    Thing dresser = Fixture.ById(dresserId);
                    if (StorageProblem(dresser, 20) is string why)
                        return Ok(why);
                    Pawn reaches = Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(p => p.CanReach(dresser, PathEndMode.ClosestTouch, Danger.Deadly));
                    if (reaches != null)
                        return Ok($"{reaches} can still reach the dresser");
                    foreach (Thing item in items.Select(Fixture.ById))
                        if (StoreUtility.TryFindBestBetterStorageFor(item, Worker, Map, StoragePriority.Normal, Faction.OfPlayer, out IntVec3 cell, out _))
                            return Ok($"the store finder sends {item} to {cell}");
                    watchUntil = Find.TickManager.TicksGame + 3000;
                    return Ok(null);
                })
                .Expect("3000 ticks with no job on the dresser", () =>
                {
                    CellRect rect = Fixture.ById(dresserId).OccupiedRect();
                    foreach (Pawn p in Map.mapPawns.FreeColonistsSpawned)
                    {
                        Job job = p.CurJob;
                        if (job != null && new[] { job.targetA, job.targetB, job.targetC }.Any(t => t.IsValid && rect.Contains(t.Cell)))
                            jobs.Add(job.loadID);
                    }
                    problem = $"{jobs.Count} jobs targeted the dresser";
                    return Find.TickManager.TicksGame >= watchUntil;
                })
                .Expect("no job targeted it and the apparel stayed in the stockpile", () =>
                {
                    int inZone = items.Count(id => Fixture.ById(id) is Thing t && zone.ContainsCell(t.Position));
                    return Ok(jobs.Count == 0 && inZone == 6 ? null : $"{jobs.Count} jobs targeted the dresser; {inZone}/6 in the stockpile");
                })
                .Start();
        }
    }
}
