# TT-001: Stored apparel can vanish when a full dresser or wardrobe is removed

| Field | Value |
| --- | --- |
| **Type** | Bug |
| **Status** | Resolved (deconstruct and uninstall); destruction by fire, raids or roof collapse still open |
| **Priority** | High (silent item loss, no error logged) |
| **Component** | Storage: dresser and wardrobe (`Patches/TT_Dresser.xml`, `Patches/TT_Wardrobe.xml`) |
| **Affects** | 1.0.0 |
| **Found by** | Code review of the storage mechanism (2026-11-10) |
| **Test coverage** | T13, reworked to a crowded site (unfixed: 36 of 40 shirts lost; fixed: 0 lost) |

## Summary

When a dresser or wardrobe holding more than one item per cell leaves the map, vanilla moves the extra items to
nearby cells and ignores the result of each placement. If no usable cell is found, the item has already been taken
off the map and is never put back. It is lost with no error, and it is gone for good at the next save.

## Root cause

`Verse/Building.cs:274-298` (`Building.DeSpawn`, game `1.6.4871`):

```csharp
if (MaxItemsInCell < 2) return;
foreach (IntVec3 item3 in this.OccupiedRect())
{
    int itemCount = item3.GetItemCount(map);
    if (itemCount <= 1 || itemCount <= item3.GetMaxItemsAllowedInCell(map)) continue;
    for (int k = 0; k < itemCount - 1; k++)
    {
        Thing firstItem = item3.GetFirstItem(map);
        if (firstItem == null) break;
        firstItem.DeSpawn();
        GenPlace.TryPlaceThing(firstItem, item3, map, ThingPlaceMode.Near);   // result ignored
        if (item3.GetItemCount(map) <= item3.GetMaxItemsAllowedInCell(map)) break;
    }
}
```

- `GenPlace.TryPlaceThing(..., Near)` returns `false` **without logging** when `TryFindPlaceSpotNear` finds no spot
  (`Verse/GenPlace.cs:58-61`).
- The search covers a radius of 12.9 cells, 529 cells in all (`GenPlace.cs:19`). A cell is unusable when the item
  can't spawn there (a wall, rock, or out of bounds), when it already holds an item outside storage, or when it is
  storage that is full or doesn't accept the item (`GenPlace.PlaceSpotQualityAt`, `GenPlace.cs:216-247`).
  Unreachable cells still count as a last resort.

## Why this mod makes it worse

Vanilla shelves have the same flaw, but each of their cells holds 3 items, so a full shelf has 2 items per cell to move.
Our furniture holds far more:

| Furniture | Capacity | Items left in place (1 per cell) | Items to move |
| --- | --- | --- | --- |
| Shelf (vanilla, 1 cell) | 3 | 1 | 2 |
| Dresser | 20 | 2 | **18** |
| Wardrobe | 30 | 2 | **28** |

## Triggers

All of these go through `Building.DeSpawn`:

- [x] Deconstruct (`DestroyMode.Deconstruct`): fixed
- [x] Uninstall or reinstall: fixed (`MinifyUtility.MakeMinified` → ASF `ThingClass.Minify` → `DeSpawn`; ASF packs
      contents only when the `minifiedDef` has `MinifiedExtension packContents`, and vanilla `MinifiedThing` has none)
- [ ] Destroyed by fire, explosion, a raid or a roof collapse (`DestroyMode.KillFinalize`): not covered, open question

## Likely situations

- A packed storeroom of linked, full dressers and wardrobes being cleared out one piece at a time: nearby
  furniture is full and nearby floor cells already hold items.
- A mountain base, where most cells within 13 tiles are solid rock.
- A full wardrobe in a crowded room that burns down or is destroyed in a raid.

## Steps to reproduce

1. Build a small room (for example 3×3) enclosed by natural rock or walls, with every floor cell holding an item or
   furniture, and no free cells within about 13 tiles.
2. Put a dresser in it and fill it with 20 apparel.
3. Deconstruct or uninstall the dresser.
4. Count the apparel on the map.

**Expected:** all 20 items are still on the map.
**Actual:** fewer than 20. The missing ones are despawned with no owner, and nothing appears in the log.

## Checked and not affected

| Area | Why it is safe |
| --- | --- |
| Loading a save | Items are placed before buildings (`Map.FinalizeLoading`, `Map.cs:765-795`); the overflow check in `GenSpawn.cs:152` runs only when `ProgramState.Playing`. |
| Adding or removing VFE | VFE's `Table_Wardrobe` and ours share the size (2,1) and the stuff categories (Metallic, Woody, Stony): no refund or wipe on load. |
| Building over items | Pass-through furniture doesn't wipe items (`GenSpawn.SpawningWipes`, `GenSpawn.cs:383`). |
| Removing the mod | ASF's `SaveGameCompatibility` falls back to the def's class (`Building`); the items stay spawned on the floor. |
| Minified dresser sold or destroyed | No contents are packed into it (see Triggers). |
| `LegacyDresserConverter` | Creates storage settings only; never touches items. Vanilla saves `settings` for linked buildings too (`Building_Storage.cs:185`). |
| Filters and priority | Change where apparel is hauled; never destroy it. |
| Test harness in the shipped DLL | Gated by the `-modcenter-autotest` launch argument; no dev-mode entry point. |

## Resolution

**Decided (user, 2026-11-10):** before a dresser or wardrobe is deconstructed or reinstalled, all the apparel stored
in it is removed first. The vanilla overflow drop then has nothing to move, so it can't lose anything.

### Acceptance criteria

- [x] Deconstructing a dresser or wardrobe that holds apparel first empties it. No stored item is ever left
      unspawned with no owner. (T13)
- [x] Uninstalling (and so reinstalling) a dresser or wardrobe that holds apparel first empties it in the same way. (T13)
- [x] Every removed item ends up spawned on the map or in a hauler's hands. (T13) If no cell on the whole map can take
      an item, it is put back on the furniture and a `[TidyThreads] ERROR` names it; vanilla's drop then handles it as
      before. This needs a map with no free cell at all, so it isn't tested.
- [x] An empty dresser or wardrobe is deconstructed and uninstalled exactly as before. (T3)
- [x] No new red errors from this mod, ASF or Harmony. (T13, T14, T3)

### Notes for the implementer

- This needs C# beyond the pre-mod converter. That changes the recorded decision "C# has one job": record the
  change in `TODO.md § Decisions`, `AGENTS.md § Recorded decisions` and the living plan doc. Justify the new Harmony
  patch in the plan, as `docs/playbook.md § Code conventions` requires.
- Hook point to choose: the deconstruct and uninstall paths (`Designator_Deconstruct`/`JobDriver_Deconstruct`,
  `Designator_Uninstall`/`JobDriver_Uninstall`, `MinifyUtility.MakeMinified`). Look up the exact vanilla methods
  with `tools/vanilla.sh grep`; don't rely on memory.
- Destruction by fire, raids or roof collapse can't be emptied ahead of time. Ask the user whether those triggers
  are in scope or stay a known vanilla limitation.
- Correct the README line "Uninstalling or deconstructing … Nothing is deleted" to match the behaviour that ships.

### Implementation

`Source/TidyThreads/EmptyBeforeRemoval.cs`, two Harmony prefixes that both call `EmptyBeforeRemoval.Empty`:

| Patch | Covers |
| --- | --- |
| `Building_Storage.Destroy`, mode `Deconstruct` | `JobDriver_Deconstruct.FinishedRemoving`, god mode's `Designator_Deconstruct` |
| `MinifyUtility.MakeMinified` | `MinifyUtility.Uninstall` (the uninstall job), `Toils_Construct.UninstallIfMinifiable` (reinstall), `GenSpawn.Refund` |

For our furniture only (`ApparelStorageExtension`), and only while it is spawned, every item on its cells is taken off
and placed with `GenPlace.TryPlaceThing(Near)`, never on the furniture's own cells. If that fails, the same search runs
from grid points 18 cells apart over the whole map, nearest first, so every cell on the map is in reach. One
`[TidyThreads]` dev-mode line reports how many items moved and how many needed the fallback.

### Verification

T13 was reworked with the user's approval: every free cell within 13 cells of its dressers holds one forbidden wood
log, and a probe shirt confirms that vanilla's Near search finds no spot. Run log in `docs/test-runs.md` (2026-11-11):

| Build | Result |
| --- | --- |
| Without the fix | FAIL: 36 of 40 shirts neither spawned nor carried (18 per dresser) |
| With the fix | T13 PASS, 0 new errors; T14 (moved fixture) and T3 (empty uninstall/reinstall) PASS |

## References

- Vanilla: `.tools/decomp/Verse/Building.cs`, `Verse/GenPlace.cs`, `Verse/GenSpawn.cs`, `Verse/Map.cs`,
  `RimWorld/Building_Storage.cs`
- ASF: `Source/AdaptiveStorage/ThingClass.cs` (`OnDeSpawn`, `Minify`),
  `HarmonyPatches/EnablePackingOnMinify.cs`, `HarmonyPatches/SaveGameCompatibility.cs`
- Scenario: T13 Uninstall and deconstruct while full (`Source/TidyThreads/Scenarios.P4.cs`)
