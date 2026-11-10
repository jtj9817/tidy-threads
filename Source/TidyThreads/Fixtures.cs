using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

// Scenario fixture kit for the autotest loop: rooms, benches and power that come up in a known state, by convention.
// To adopt: copy into Source/<YourMod>/ and rename the namespace; it depends on nothing else in the mod.
// The conventions, layouts and the vanilla facts behind each rule: docs/scenario-fixtures.md.
namespace TidyThreads
{
    /// <summary>How a fixture room is powered. A powered room has a conduit under its whole north wall and its source
    /// outside that wall, touching it.</summary>
    public enum RoomPower
    {
        /// <summary>No conduit, no source: the "bench without power" failure mode.</summary>
        None,
        /// <summary>Vanometric power cells (1000 W each, forever), added as the room's benches need them.</summary>
        Vanometric,
        /// <summary>One battery and no generator: stored energy only, for drain and power-loss failure modes.</summary>
        Battery,
    }

    /// <summary>A walled, roofed steel room: door in the middle of the south wall, benches along the north wall.</summary>
    public sealed class FixtureRoom
    {
        /// <summary>Walls included.</summary>
        public CellRect rect;
        public RoomPower power;
        public string doorId;
        public readonly List<string> benchIds = new List<string>();
        public readonly List<string> sourceIds = new List<string>();

        public CellRect Inside => rect.ContractedBy(1);
        public IntVec3 Door => new IntVec3(rect.CenterCell.x, 0, rect.minZ);
        /// <summary>Two cells in front of the door: where a worker stands to be "outside, able to walk in".</summary>
        public IntVec3 Outside => Door + IntVec3.South * 2;
        public Room Room => Inside.CenterCell.GetRoom(Find.CurrentMap);
        public IEnumerable<Building> Benches => benchIds.Select(Fixture.ById).OfType<Building>();
    }

    /// <summary>
    /// Builds and removes one scenario's rooms. Holds ThingIDs and cells, never references, so it survives a save/reload.
    /// Wire it as <c>new Scenario(...).OnFinish(f.CleanUp)</c> and never <c>Track</c> its things: walls must go after the roof.
    /// </summary>
    public sealed class Fixture
    {
        private const int MaxRoofedSpan = 13;
        private static ThingDef VanometricCell => DefDatabase<ThingDef>.GetNamed("VanometricPowerCell");

        public readonly List<FixtureRoom> rooms = new List<FixtureRoom>();
        private readonly List<string> thingIds = new List<string>();
        private readonly List<IntVec3> roofed = new List<IntVec3>();

        private static Map Map => Find.CurrentMap;

        public static Thing ById(string id) => Map?.listerThings.AllThings.FirstOrDefault(t => t.ThingID == id);

        /// <summary>A width×height room (walls included) on a fresh <see cref="Site"/> near the first free colonist.</summary>
        public FixtureRoom Room(int width = 7, int height = 5, RoomPower power = RoomPower.Vanometric, float batteryPct = 1f,
            IntVec3? near = null) =>
            Room(Site(width, height, near: near), power, batteryPct);

        /// <summary>
        /// <paramref name="count"/> rooms west to east, each sharing its east wall with the next, one door each; their
        /// conduits join into one power net along the shared north wall.
        /// </summary>
        public List<FixtureRoom> Rooms(int count, int width, int height, RoomPower power = RoomPower.Vanometric,
            float batteryPct = 1f, IntVec3? near = null)
        {
            CellRect site = Site(count * (width - 1) + 1, height, near: near);
            var list = new List<FixtureRoom>();
            for (int i = 0; i < count; i++)
                list.Add(Room(new CellRect(site.minX + i * (width - 1), site.minZ, width, height), power, batteryPct));
            return list;
        }

        /// <summary>
        /// A width×height rect (walls included) near <paramref name="near"/> (default: the first free colonist), clear for
        /// <paramref name="margin"/> cells around and reachable from there: no building, roof, water, fog or zone. Its
        /// plants and items are destroyed and pawns moved out. The margin holds the door approach and the power source.
        /// </summary>
        public CellRect Site(int width, int height, int margin = 2, IntVec3? near = null, float maxDist = 50f)
        {
            Map map = Map;
            IntVec3 origin = near ?? map.mapPawns.FreeColonistsSpawned.FirstOrDefault()?.Position ?? map.Center;
            // The map center can be solid rock, from which nothing is reachable.
            IntVec3 from = origin.Standable(map) ? origin : CellFinder.StandableCellNear(origin, map, maxDist);
            CellRect RectAt(IntVec3 c) => new CellRect(c.x - width / 2, c.z - height / 2, width, height);
            bool Clear(IntVec3 c) => c.InBounds(map) && c.Walkable(map) && !c.Fogged(map) && !c.Roofed(map)
                && !c.GetTerrain(map).IsWater && c.GetFirstBuilding(map) == null && map.zoneManager.ZoneAt(c) == null;
            // Every cell in range, nearest first: random sampling misses the few clear areas on a dense map (QR).
            IntVec3? found = CellRect.CenteredOn(origin, (int)maxDist).ClipInsideMap(map).Cells
                .Where(c => c.InHorDistOf(origin, maxDist))
                .OrderBy(c => c.DistanceToSquared(origin))
                .Select(c => (IntVec3?)c)
                .FirstOrDefault(c =>
                {
                    CellRect around = RectAt(c.Value).ExpandedBy(margin);
                    return !around.Contains(from) && around.Cells.All(Clear)
                        && map.reachability.CanReach(from, new IntVec3(RectAt(c.Value).CenterCell.x, 0, RectAt(c.Value).minZ - 2),
                            PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors));
                });
            if (found == null)
                throw new InvalidOperationException($"no clear {width}x{height} site (+{margin}) within {maxDist} cells of {origin}");
            CellRect rect = RectAt(found.Value);
            ClearItemsAndPlants(rect.ExpandedBy(margin));
            Evacuate(rect.ExpandedBy(1));
            return rect;
        }

        /// <summary>
        /// Walls, door, roof and power on <paramref name="rect"/> (walls included; pawns in it are moved out first, since a
        /// spawned wall wipes items and buildings but never pawns). Throws if the result is not an enclosed indoor room.
        /// </summary>
        public FixtureRoom Room(CellRect rect, RoomPower power = RoomPower.Vanometric, float batteryPct = 1f)
        {
            Map map = Map;
            if (rect.Width < 5 || rect.Height < 4)
                throw new ArgumentException($"room {rect} is too small: a bench needs 3 inside cells across and its interaction cell");
            // Constructed roof collapses beyond 6.9 cells of a holder (RoofCollapseUtility); nothing in a room is a holder.
            if (Math.Min(rect.Width, rect.Height) > MaxRoofedSpan)
                throw new ArgumentException($"room {rect}: the roof needs a wall within 6 cells; use Rooms() instead of one span over {MaxRoofedSpan}");
            var room = new FixtureRoom { rect = rect, power = power };
            Evacuate(rect);
            ClearItemsAndPlants(rect);
            if (power != RoomPower.None)
                // Laid first, so each wall goes up over it as a colonist would build it; transmitters join by touching.
                for (int x = rect.minX; x <= rect.maxX; x++)
                {
                    var c = new IntVec3(x, 0, rect.maxZ);
                    if (!c.GetThingList(map).Any(t => t.def == ThingDefOf.PowerConduit))
                        Spawn(ThingDefOf.PowerConduit, c);
                }
            foreach (IntVec3 c in rect.EdgeCells)
                if (c != room.Door && c.GetEdifice(map)?.def != ThingDefOf.Wall)   // a shared wall is already up
                    Spawn(ThingDefOf.Wall, c, ThingDefOf.Steel);
            room.doorId = Spawn(ThingDefOf.Door, room.Door, ThingDefOf.Steel).ThingID;
            foreach (IntVec3 c in rect.Cells.Where(c => !c.Roofed(map)))
            {
                map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                roofed.Add(c);
            }
            if (power == RoomPower.Vanometric)
                AddSource(room, VanometricCell);
            else if (power == RoomPower.Battery)
                AddSource(room, ThingDefOf.Battery).TryGetComp<CompPowerBattery>().SetStoredEnergyPct(batteryPct);
            map.regionAndRoomUpdater.TryRebuildDirtyRegionsAndRooms();
            Room r = room.Room;
            if (r == null || r.PsychologicallyOutdoors || r.TouchesMapEdge)
                throw new InvalidOperationException($"room {rect} is not an enclosed indoor room");
            rooms.Add(room);
            return room;
        }

        /// <summary>
        /// A bench by defName in the room's north row, facing south so its interaction cell is inside, slot 0 at the west
        /// end, one free cell between benches. Made from default stuff, refueled if fueled, and in a vanometric room the
        /// room gets another cell whenever its benches would draw more than the cells make.
        /// </summary>
        public Building Bench(FixtureRoom room, string defName, int slot = 0)
        {
            Map map = Map;
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            IntVec2 size = def.size;
            int minX = room.Inside.minX + slot * (size.x + 1);
            var center = new IntVec3(minX + (size.x - 1) / 2, 0, room.Inside.maxZ - (size.z - 1) + (size.z - 1) / 2);
            CellRect footprint = GenAdj.OccupiedRect(center, Rot4.North, size);
            if (!room.Inside.Contains(footprint.Min) || !room.Inside.Contains(footprint.Max))
                throw new ArgumentException($"{defName} slot {slot} ({footprint}) doesn't fit inside {room.Inside}");
            if (def.hasInteractionCell)
            {
                IntVec3 ic = ThingUtility.InteractionCellWhenAt(def, center, Rot4.North, map);
                if (!room.Inside.Contains(ic) || footprint.Contains(ic) || ic.GetEdifice(map) != null)
                    throw new ArgumentException($"{defName} slot {slot}: interaction cell {ic} is not a free cell inside {room.Inside}");
            }
            Evacuate(footprint);
            var bench = (Building)Spawn(def, center);
            room.benchIds.Add(bench.ThingID);
            CompRefuelable fuel = bench.TryGetComp<CompRefuelable>();
            if (fuel != null)
                fuel.Refuel(fuel.GetFuelCountToFullyRefuel());
            if (room.power == RoomPower.Vanometric)
                while (Demand(room) > room.sourceIds.Count * -VanometricCell.GetCompProperties<CompProperties_Power>().PowerConsumption)
                    AddSource(room, VanometricCell);
            return bench;
        }

        /// <summary>Destroys the room's power sources: its consumers switch off on the next power-net tick.</summary>
        public void CutPower(FixtureRoom room)
        {
            foreach (string id in room.sourceIds)
                ById(id)?.Destroy();
            room.sourceIds.Clear();
        }

        /// <summary>Spawns a player building (default stuff when made from stuff) and owns it for <see cref="CleanUp"/>.</summary>
        public Thing Spawn(ThingDef def, IntVec3 cell, ThingDef stuff = null, Rot4? rot = null)
        {
            Thing thing = ThingMaker.MakeThing(def, stuff ?? (def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null));
            if (def.CanHaveFaction)
                thing.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(thing, cell, Map, rot ?? Rot4.North);
            thingIds.Add(thing.ThingID);
            return thing;
        }

        /// <summary>Moves every pawn standing in <paramref name="area"/> to a free standable cell outside it.</summary>
        public void Evacuate(CellRect area)
        {
            Map map = Map;
            CellRect keepOut = area.ExpandedBy(1);
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.Where(p => area.Contains(p.Position)).ToList())
            {
                if (!CellFinder.TryFindRandomCellNear(area.CenterCell, map, Math.Max(area.Width, area.Height) + 8,
                        c => !keepOut.Contains(c) && c.Standable(map) && c.GetFirstPawn(map) == null, out IntVec3 to))
                    throw new InvalidOperationException($"nowhere to move {pawn} out of {area}");
                pawn.Position = to;
                pawn.Notify_Teleported();
            }
        }

        /// <summary>
        /// True once every consumer bench in a powered room is on. Use it in an <c>Expect</c>, never a <c>Do</c>: connections
        /// apply on the next tick and the power net then switches consumers on over up to max(200, 30·n) ticks.
        /// </summary>
        public bool AllPowered() => PoweredBenches().All(b => b.TryGetComp<CompPowerTrader>().PowerOn);

        /// <summary>Why the first unpowered consumer bench is off, or null if all are on: for a FAIL line.</summary>
        public string Unpowered() => PoweredBenches().Select(WhyNotPowered).FirstOrDefault(why => why != null);

        /// <summary>Why <paramref name="thing"/> has no power, or null if it is on (or uses none).</summary>
        public static string WhyNotPowered(Thing thing)
        {
            CompPowerTrader power = thing.TryGetComp<CompPowerTrader>();
            if (power == null || power.PowerOn)
                return null;
            if (!thing.Spawned)
                return $"{thing} is not spawned";
            if (!FlickUtility.WantsToBeOn(thing))
                return $"{thing} is switched off";
            PowerNet net = power.PowerNet;
            if (net == null)
                return $"{thing} at {thing.Position} has no power net: no transmitter within 6 cells (or it spawned this tick)";
            float watts = net.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
            if (watts < 0f && net.CurrentStoredEnergy() <= 0f)
                return $"{thing}: its net runs {watts:F0} W with no stored energy";
            return $"{thing}: connected ({watts:F0} W, {net.CurrentStoredEnergy():F0} Wd stored), not switched on yet";
        }

        /// <summary>Why <paramref name="worker"/> can't use <paramref name="bench"/>, or null if it can reach it.</summary>
        public static string WhyUnreachable(Pawn worker, Thing bench)
        {
            Map map = bench.Map;
            if (!worker.Spawned || worker.Downed)
                return $"{worker} is not up and spawned";
            if (bench.def.hasInteractionCell && !bench.InteractionCell.Standable(map))
                return $"{bench}'s interaction cell {bench.InteractionCell} is blocked by {bench.InteractionCell.GetFirstBuilding(map)}";
            if (worker.CanReach(bench, PathEndMode.InteractionCell, Danger.Deadly))
                return null;
            Room room = bench.GetRoom();
            List<Building_Door> doors = room?.ContainedAndAdjacentThings.OfType<Building_Door>().ToList() ?? new List<Building_Door>();
            return $"{worker} at {worker.Position}{(worker.Position.Standable(map) ? "" : " (inside a wall)")} can't reach {bench}: "
                + $"its room {room?.ID} has {doors.Count} doors, {doors.Count(d => d.IsForbidden(Faction.OfPlayer))} forbidden";
        }

        /// <summary>
        /// Unroofs first (a wall despawning under a roof queues a collapse of what it held), then destroys every
        /// thing the fixture spawned, newest first. Safe to call twice, and after a reload.
        /// </summary>
        public void CleanUp()
        {
            Map map = Map;
            foreach (IntVec3 c in roofed)
                map?.roofGrid.SetRoof(c, null);
            roofed.Clear();
            for (int i = thingIds.Count - 1; i >= 0; i--)
                if (ById(thingIds[i]) is Thing t && !t.Destroyed)
                    t.Destroy();
            thingIds.Clear();
            rooms.Clear();
        }

        private IEnumerable<Building> PoweredBenches() =>
            rooms.Where(r => r.power != RoomPower.None).SelectMany(r => r.Benches).Where(b => b.TryGetComp<CompPowerTrader>() != null);

        private static float Demand(FixtureRoom room) =>
            room.Benches.Sum(b => b.TryGetComp<CompPowerTrader>()?.Props.PowerConsumption ?? 0f);

        /// <summary>
        /// A 1×2 source outside the north wall, touching the conduit under it: never on the conduit itself, since a
        /// spawning transmitter wipes a conduit in its cell. Nearest free column to the wall's middle first.
        /// </summary>
        private Thing AddSource(FixtureRoom room, ThingDef def)
        {
            Map map = Map;
            int cx = room.rect.CenterCell.x;
            for (int i = 0; i < room.rect.Width * 2; i++)
            {
                int x = cx + (i % 2 == 0 ? i / 2 : -(i / 2 + 1));
                var cell = new IntVec3(x, 0, room.rect.maxZ + 1);
                if (x < room.rect.minX || x > room.rect.maxX)
                    continue;
                if (GenAdj.OccupiedRect(cell, Rot4.North, def.size).Cells.Any(c => !c.InBounds(map) || c.GetFirstBuilding(map) != null))
                    continue;
                Thing source = Spawn(def, cell);
                room.sourceIds.Add(source.ThingID);
                return source;
            }
            throw new InvalidOperationException($"no free cell outside the north wall of {room.rect} for another {def.defName}");
        }

        private static void ClearItemsAndPlants(CellRect area)
        {
            Map map = Map;
            foreach (IntVec3 c in area.ClipInsideMap(map).Cells)
                foreach (Thing t in c.GetThingList(map).Where(t => t.def.category == ThingCategory.Plant
                             || t.def.category == ThingCategory.Item).ToList())
                    t.Destroy();
        }
    }
}
