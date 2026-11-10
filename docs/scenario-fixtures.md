# Scenario fixtures: rooms, benches, power

Convention over configuration for the state a scenario starts from. A fixture that is "a powered tailoring bench a
colonist can walk to" should be one line, built the same way every time, and fail loudly *at setup* when the map
can't host it — not 3000 ticks later as "bill never started". [`tools/game-side/Fixtures.cs`](../tools/game-side/Fixtures.cs)
is the code; this page is the why, the layouts and the recipes. Scenario scope rules still apply
([testing.md § Strict testing scope](testing.md)): build only the fixture the named scenario needs.

**Status.** `Fixtures.cs` compiles against `Krafs.Rimworld.Ref 1.6.4871` (warnings as errors) but has **not run in
game yet**: the first mod to adopt it proves it with one named scenario and records that run here. It generalizes
two in-game-proven fixtures: Quadrunner's `BuildRoom` (`Scenarios.Charging.cs`: conduit under the north wall,
battery touching it from outside, door south) and RimLogistics' `StorageFixture.WalledRoom` + vanometric cell beside
a cooler (`Scenarios.Storage.cs`, T07, run 2026-10-24 20:16). Every vanilla fact below was read in the 1.6
decompile (`tools/vanilla.sh grep`) or Core Defs (`tools/vanilla.sh def`); file names are given so you can re-check.

## Adopt

Copy `tools/game-side/Fixtures.cs` into `Source/<MOD_NAME>/` and rename its namespace. It depends only on
`Verse`/`RimWorld`; it works with the Quadrunner-style `Scenario` runner (`Do`/`Expect`/`OnFinish`, a thrown
exception becomes `FAIL at step n 'label'`).

```csharp
var f = new Fixture();
new Scenario("...", 3000).OnFinish(f.CleanUp)   // never s.Track() fixture things: see CleanUp below
```

## The conventions

| Rule | Convention | Why (vanilla fact) |
| --- | --- | --- |
| Footprint | `rect` includes the walls; `Inside` = `rect.ContractedBy(1)`; min 5×4 | a 3-wide bench plus its interaction cell |
| Site | `Site(w, h)`: nearest clear rect to the first free colonist, 2 free cells around, door approach reachable | no building, roof, water, fog or **zone** (haulers stock zones); plants/items destroyed |
| Pawns | everyone in the footprint is moved out **before** walls go up | `GenSpawn.SpawningWipes` wipes items and buildings, **never pawns** → walled-in colonist |
| Door | middle of the south wall, steel; `room.Outside` = 2 south of it | the one way in; a worker staged at `Outside` walks in on its own |
| Walls / roof | steel walls, `RoofConstructed` over the whole rect | `PsychologicallyOutdoors`/`TouchesMapEdge` checked after `TryRebuildDirtyRegionsAndRooms` |
| Span | shortest side ≤ 13 (interior ≤ 6 cells from a wall) | constructed roof > 6.9 cells from a holder collapses (`RoofCollapseUtility.RoofMaxSupportDistance`); bigger → `Rooms()` |
| Benches | north interior row, `Rot4.North`, slot *i* at the west end + *i*·(width+1) | interaction cell `(0,0,-1)` → one cell south, always inside, never against a wall |
| Power line | conduit under the **whole** north wall, laid before the wall | conduit isn't an edifice, so a wall spawned over it keeps it (`SpawningWipes`); consumers connect to the nearest transmitter within 6 cells (`PowerConnectionMaker.BestTransmitterForConnector`) — a north-row bench is 1 cell away |
| Source | 1×2 cell or battery **outside** the north wall, touching the conduit | transmitters join a net only by touching; a transmitter spawned on a conduit **wipes** it (`SpawningWipes`: `EverTransmitsPower` vs `isPowerConduit`) |
| Supply | vanometric rooms add a 1000 W cell whenever their benches would draw more | short nets switch a random consumer off (`PowerNet.PowerNetTick`) |
| Fuel | fueled benches are filled on spawn | `CurrentlyUsableForBills` needs `HasFuel` for a fueled bench |
| Ready | `Expect("powered", f.AllPowered)`, never a `Do` | see [Power comes on later](#power-comes-on-later) |
| Cleanup | unroof, then destroy newest-first | a wall despawning under roof queues a collapse (`RoofCollapseCellsFinder.Notify_RoofHolderDespawned`); Quadrunner's runner destroys `Track`ed things *before* `OnFinish` |

## Layouts

`W` wall · `D` door · `=` conduit under a wall · `V` vanometric cell or battery (1×2) · `B` bench · `i` its
interaction cell · `o` `room.Outside`. North is up.

**Single powered room** — `f.Room(7, 5)` + `f.Bench(room, "ElectricTailoringBench")`

```text
        V
        V
  = = = = = = =
  W B B B . . W
  W . i . . . W
  W . . . . . W
  W W W D W W W
        .
        o
```

**Adjacent rooms** — `f.Rooms(3, 6, 5)`: shared east/west walls, one door each, one conduit run so one net

```text
        V         V         V
        V         V         V
  = = = = = = = = = = = = = = = =
  W B B B . W B B B . W B B B . W
  W . i . . W . i . . W . i . . W
  W . . . . W . . . . W . . . . W
  W W W D W W W W D W W W W D W W
```

**Large multi-bench room** — `f.Room(17, 6)` + benches in slots 0–3: the four electric benches below draw
1030 W, so the room gets a second cell automatically.

**Battery + conduit** — `f.Room(7, 5, RoomPower.Battery, batteryPct: 0.2f)`: no generator, stored energy only
(600 Wd capacity; it charges at 50% efficiency, so start it at the level you need). `f.CutPower(room)` destroys a room's sources for the power-loss case;
`RoomPower.None` builds the room with no conduit at all for "bench never powered".

## Production benches (Core 1.6)

| defName | Size | Power | Without it | Notes |
| --- | --- | --- | --- | --- |
| `HandTailoringBench` | 3×1 | — | — | stuff: metallic/woody |
| `ElectricTailoringBench` | 3×1 | 120 W | **works at 50%** (`unpoweredWorkTableWorkSpeedFactor`) | stuff: metallic/woody |
| `FueledSmithy` | 3×1 | — | needs fuel (cap 50) | filled by `Bench()` |
| `ElectricSmithy` | 3×1 | 210 W | unusable | pushes heat |
| `TableMachining` | 3×1 | 350 W | unusable | |
| `ElectricStove` | 3×1 | 350 W | unusable | pushes heat |
| `VanometricPowerCell` | 1×2 | **+1000 W**, transmits | — | `CompPowerPlant`; not in `ThingDefOf` — `GetNamed` |
| `Battery` | 1×2 | stores 600 Wd (50% charge efficiency), transmits | — | `ThingDefOf.Battery`; `SetStoredEnergyPct` |

All six benches have `interactionCellOffset (0,0,-1)`. Re-check any of them with `tools/vanilla.sh def <defName>`.

## Recipes

Each throws at setup when the state isn't what the scenario claims; the runner turns that into a FAIL naming the
step.

```csharp
// A powered tailoring bench a colonist can reach, worker waiting outside the door.
var f = new Fixture();
FixtureRoom room = null; Building bench = null; Pawn worker = null;
new Scenario("Tailor in a powered room", 3000)
    .OnFinish(f.CleanUp)
    .Do("build the room", () =>
    {
        room = f.Room(7, 5);
        bench = f.Bench(room, "ElectricTailoringBench");
        worker = Find.CurrentMap.mapPawns.FreeColonistsSpawned.First();
        worker.Position = room.Outside;
        worker.Notify_Teleported();
    })
    .Expect("bench powered", f.AllPowered)                    // up to ~200 ticks, see below
    .Do("worker can reach it", () =>
    {
        if (Fixture.WhyUnreachable(worker, bench) is string why) throw new InvalidOperationException(why);
    })
    // ... add the bill, force it (gotchas.md: JobOnThing + TryTakeOrderedJob), assert the product
```

```csharp
// Failure mode: the bench loses power mid-bill (battery room, cut the source).
room = f.Room(7, 5, RoomPower.Battery);
bench = f.Bench(room, "ElectricSmithy");
// ... Expect("powered", f.AllPowered), start the bill, then:
f.CutPower(room);
// Expect: !bench.TryGetComp<CompPowerTrader>().PowerOn, the job ends, your mod degrades gracefully
```

When an `Expect` times out, put the reason in the FAIL line: `.Summary(() => f.Unpowered() ?? "all powered")`
(`Unpowered()` says *no power net*, *net short*, *switched off*, or *connected, not switched on yet*).

## Power comes on later

1. Spawning only **queues** the connection (`PowerNetManager.Notify_ConnectorWantsConnect` →
   `delayedActions`); it is applied in `UpdatePowerNetsAndConnections_First` on the map's next tick.
2. A connected `CompPowerTrader` starts with `PowerOn = false`. `PowerNet.PowerNetTick` switches waiting
   consumers on only on ticks where `TicksGame % max(30, 200/n) == 0`, about 5% of them (at least one) each time:
   one bench waits up to 200 ticks, *n* benches about `max(200, 30·n)`.

So `PowerOn` checked in the same `Do` that spawned the bench is always false, and a `Wait(1)` isn't enough. Gate on
`Expect(..., f.AllPowered)` with the scenario timeout well above that. `DebugSettings.unlimitedPower` doesn't help
here (it needs a net, and refills every battery: [gotchas.md](gotchas.md)).

## Why scenarios fail: symptom → cause

| Symptom | Cause | Convention that prevents it |
| --- | --- | --- |
| Bench never powers, `PowerNet == null` | no transmitter within 6 cells, or battery not touching a conduit | conduit under the north wall, source touching it |
| `PowerOn` false right after spawning | connection and switch-on are deferred | `Expect(f.AllPowered)` |
| Power flickers / one bench off | net demand > supply | auto-added cells; one 1000 W cell < four benches |
| Conduit "disappeared" | battery/cell spawned on its cell wiped it | sources outside the wall |
| Electric tailor works unpowered | 50% speed without power, `CurrentlyUsableForBills` true | assert `PowerOn`, not usability |
| Colonist inside a wall, or walled in | spawned walls never wipe pawns | `Evacuate()` before every wall/bench |
| Bill never starts, no error | interaction cell blocked, door missing/forbidden, or worker elsewhere | north-row benches, `WhyUnreachable()` |
| Colonists haul into or out of the test room | site overlapped a zone, or stock left unforbidden | `Site()` avoids zones; forbid fixture stock (RL T07) |
| Roof collapses, colonist hurt during cleanup | walls destroyed under roof | `CleanUp()` unroofs first |
| Colonist "stuck" doing other work | natural work and needs compete | [testing.md rules](testing.md): hold crafters drafted until the ordered job exists; force the job; `pawn.jobs.debugLog = true` |

## Looking things up instead of guessing

```bash
tools/vanilla.sh def ElectricSmithy                       # size, power, interaction cell, parent chain
tools/vanilla.sh grep 'BestTransmitterForConnector'       # the 6-cell connection search
tools/vanilla.sh grep 'void PowerNetTick'                 # when consumers switch on
tools/vanilla.sh grep 'static bool SpawningWipes'         # what a spawn destroys (pawns never)
tools/vanilla.sh grep 'RoofMaxSupportDistance'            # roof span
tools/vanilla.sh grep 'bool CurrentlyUsableForBills'      # power + fuel gates for bills
```
