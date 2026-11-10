using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace TidyThreads
{
    /// <summary>Phase 4: dressers from saves made before the mod (T7, T13, T14), and the room role (T12).</summary>
    public static partial class Scenarios
    {
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
