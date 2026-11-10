using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TidyThreads
{
    /// <summary>Phase 1: the dresser becomes apparel storage (T1, T3, T4).</summary>
    public static partial class Scenarios
    {
        private static readonly string[] Stuffs = { "WoodLog", "Steel", "BlocksGranite" };

        /// <summary>
        /// T1. Precondition: a clear site. Trigger: dressers made in wood, steel and granite, then 20 shirts put in one.
        /// Expected: each is ASF storage, apparel only, Critical, limit 20, the research unchanged, and the full one
        /// holds 20 without drawing them. Failure mode: the patch didn't apply, or a wrong filter, priority or capacity.
        /// </summary>
        private static void T1BuildADresser()
        {
            var f = new Fixture();
            var dressers = new List<string>();
            var scenario = new Scenario("T1 Build a dresser", 2000).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Do("three dressers", () =>
                {
                    problem = null;
                    CellRect site = f.Site(9, 3);
                    for (int i = 0; i < Stuffs.Length; i++)
                        dressers.Add(f.Spawn(Def("Dresser"), new IntVec3(site.minX + 1 + 3 * i, 0, site.minZ + 1),
                            Def(Stuffs[i]), Rot4.South).ThingID);
                })
                .Expect("research is still Complex furniture",
                    () => Ok(Def("Dresser").researchPrerequisites?.Any(r => r.defName == "ComplexFurniture") == true
                        ? null : "dresser research changed"))
                .Expect("every dresser is Critical apparel storage for 20",
                    () => Ok(dressers.Select(id => StorageProblem(Fixture.ById(id), 20)).FirstOrDefault(p => p != null)))
                .Do("20 shirts into the wooden dresser", () =>
                {
                    Thing dresser = Fixture.ById(dressers[0]);
                    List<IntVec3> cells = dresser.OccupiedRect().Cells.ToList();
                    for (int i = 0; i < 20; i++)
                    {
                        Thing shirt = ThingMaker.MakeThing(Def("Apparel_BasicShirt"), Def("Cloth"));
                        GenSpawn.Spawn(scenario.Track(shirt), cells[i % cells.Count], Map);
                    }
                })
                .Expect("it holds 20, shown as 20 / 20, none drawn", () =>
                {
                    Thing dresser = Fixture.ById(dressers[0]);
                    int stored = StoredItems(dresser).Count;
                    if (stored != 20)
                        return Ok($"{stored} items on the dresser, expected 20");
                    if (!dresser.GetInspectString().Contains("20 / 20"))
                        return Ok($"inspect pane: '{dresser.GetInspectString().Replace('\n', ' ')}'");
                    bool? shown = ShowsContainedItems(dresser);
                    return Ok(shown == false ? null : $"ASF ShowContainedItems is {shown?.ToString() ?? "unreadable"}");
                })
                .Start();
        }

        /// <summary>
        /// T3. Precondition: a clear site. Trigger: a minified dresser is installed, and a built one is uninstalled and
        /// reinstalled, both through the vanilla install blueprint. Expected: both end as Critical apparel storage.
        /// Failure mode: settings lost or not reset through the minified thing.
        /// </summary>
        private static void T3MinifiedAndReinstalled()
        {
            var f = new Fixture();
            string fromMinified = null, reinstalled = null;
            var scenario = new Scenario("T3 Minified and reinstalled", 2000).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Do("install a minified dresser and reinstall a built one", () =>
                {
                    problem = null;
                    CellRect site = f.Site(8, 4);
                    Thing inner = ThingMaker.MakeThing(Def("Dresser"), Def("WoodLog"));
                    MinifiedThing minified = inner.MakeMinified();
                    GenSpawn.Spawn(scenario.Track(minified), new IntVec3(site.minX + 1, 0, site.minZ), Map);
                    fromMinified = Install(scenario, minified, new IntVec3(site.minX + 1, 0, site.minZ + 2));

                    Thing built = f.Spawn(Def("Dresser"), new IntVec3(site.minX + 5, 0, site.minZ), Def("Steel"), Rot4.South);
                    MinifiedThing uninstalled = built.Uninstall();
                    scenario.Track(uninstalled);
                    reinstalled = Install(scenario, uninstalled, new IntVec3(site.minX + 5, 0, site.minZ + 2));
                })
                .Expect("the installed dresser is Critical apparel storage",
                    () => Ok(StorageProblem(Fixture.ById(fromMinified), 20)))
                .Expect("the reinstalled dresser is Critical apparel storage",
                    () => Ok(StorageProblem(Fixture.ById(reinstalled), 20)))
                .Start();
        }

        /// <summary>Places an install blueprint for <paramref name="minified"/> and completes it at once; the installed ThingID.</summary>
        private static string Install(Scenario scenario, MinifiedThing minified, IntVec3 cell)
        {
            Thing inner = minified.InnerThing;
            Blueprint_Install blueprint = GenConstruct.PlaceBlueprintForInstall(minified, cell, Map, Rot4.South, Faction.OfPlayer);
            if (!blueprint.TryReplaceWithSolidThing(Worker, out Thing created, out _))
                throw new System.InvalidOperationException($"install of {inner} at {cell} was blocked");
            return scenario.Track(created).ThingID;
        }

        /// <summary>
        /// T4. Precondition: a clear site. Trigger: a dresser blueprint, its storage edited (Important, no parkas), then
        /// turned into a frame and finished. Expected: the blueprint has a Storage tab at Critical, and the edits carry
        /// to the finished dresser. Failure mode: no Blueprint_Storage, so no tab or lost edits.
        /// </summary>
        private static void T4BlueprintSettings()
        {
            var f = new Fixture();
            IntVec3 cell = IntVec3.Invalid;
            Blueprint blueprint = null;
            string built = null;
            var scenario = new Scenario("T4 Blueprint settings", 2000).OnFinish(f.CleanUp);
            scenario.Summary(() => problem)
                .Do("a dresser blueprint", () =>
                {
                    problem = null;
                    CellRect site = f.Site(4, 3);
                    cell = new IntVec3(site.minX + 1, 0, site.minZ + 1);
                    blueprint = scenario.Track(GenConstruct.PlaceBlueprintForBuild(Def("Dresser"), cell, Map, Rot4.South,
                        Faction.OfPlayer, Def("WoodLog")));
                })
                .Expect("the blueprint has a Storage tab at Critical", () =>
                {
                    if (!(blueprint is Blueprint_Storage storage))
                        return Ok($"blueprint is a {blueprint.GetType().Name}, not Blueprint_Storage");
                    if (!storage.GetInspectTabs().Any(t => t is ITab_Storage))
                        return Ok("the blueprint has no Storage tab");
                    StoragePriority priority = storage.GetStoreSettings().Priority;
                    return Ok(priority == StoragePriority.Critical ? null : $"blueprint priority {priority}");
                })
                .Do("edit it: Important, no parkas; then build it", () =>
                {
                    StorageSettings settings = ((Blueprint_Storage)blueprint).GetStoreSettings();
                    settings.Priority = StoragePriority.Important;
                    settings.filter.SetAllow(Def("Apparel_Parka"), false);
                    if (!blueprint.TryReplaceWithSolidThing(Worker, out Thing frame, out _))
                        throw new System.InvalidOperationException($"blueprint at {cell} was blocked");
                    ((Frame)frame).CompleteConstruction(Worker);
                    built = scenario.Track(cell.GetThingList(Map).First(t => t.def == Def("Dresser"))).ThingID;
                })
                .Expect("the finished dresser kept the edits", () =>
                {
                    Thing dresser = Fixture.ById(built);
                    if (StorageProblem(dresser, 20, StoragePriority.Important) is string why)
                        return Ok(why);
                    return Ok(((Building_Storage)dresser).GetStoreSettings().filter.Allows(Def("Apparel_Parka"))
                        ? "parkas are allowed again" : null);
                })
                .Start();
        }
    }
}
