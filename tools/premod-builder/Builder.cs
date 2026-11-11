using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PreModBuilder
{
    // Test-only mod, built and enabled by `autotest.py run --premod <spec>` (tools/autotest.py in the mod's repo).
    // It runs in a launch WITHOUT the mod under test, references nothing from it, and never ships. On the quicktest map it
    // builds what <dir>/spec.txt describes on a clear site near the first colonist, writes <dir>/manifest.txt (one
    // tab-separated line per spawned thing, so the mod's scenarios can compare "before" with "after"), saves the game as
    // <dir>/save-name, and writes <dir>/result ("PASS: ..." or "FAIL: ...").
    //
    // spec.txt, one directive per line ('#' starts a comment; x/z are offsets from the site's south-west corner):
    //   site  <width> <height>
    //   thing <tag> <defName> <stuff|-> <x> <z> <North|East|South|West> [quality=<QualityCategory>] [hp=<0..1>]
    //   items <tag> <defName> <stuff|-> <count> <x> <z> <width>   (one per cell, rows of <width> going north)
    // manifest.txt columns: tag, ThingID, defName, stuff, x, z, rotation, hit points, quality, art title.
    [StaticConstructorOnStartup]
    public static class Boot
    {
        static Boot()
        {
            if (!GenCommandLine.TryGetCommandLineArg("modcenter-premod", out string dir)) return;
            var go = new GameObject("PreModBuilder");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Builder>().Dir = dir;
        }
    }

    public class Builder : MonoBehaviour
    {
        public string Dir;
        private bool finished;
        private float nextBeat;
        private int waitFrames;

        private void Update()
        {
            if (finished || Dir == null) return;
            try
            {
                if (Time.realtimeSinceStartup >= nextBeat)
                {
                    nextBeat = Time.realtimeSinceStartup + 1f;
                    File.WriteAllText(Path.Combine(Dir, "heartbeat"), "building");
                }
                if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                    || LongEventHandler.ShouldWaitForEvent || !Current.Game.PlayerHasControl) return;
                foreach (Window window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList()) window.Close(false);
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                // A few frames for the map to settle (regions, reachability) before choosing the site.
                if (++waitFrames < 30) return;
                string saveName = File.ReadAllText(Path.Combine(Dir, "save-name")).Trim();
                int count = Build(File.ReadAllLines(Path.Combine(Dir, "spec.txt")));
                string path = GenFilePaths.FilePathForSavedGame(saveName);
                if (File.Exists(path)) File.Delete(path);
                GameDataSaveLoader.SaveGame(saveName);
                if (!File.Exists(path)) throw new IOException("the save was not written: " + path);
                Complete($"PASS: built {count} things, saved {saveName}");
            }
            catch (Exception e)
            {
                Log.Error("[PreModBuilder] " + e);
                Complete("FAIL: " + e.Message);
            }
        }

        private int Build(string[] spec)
        {
            Map map = Find.CurrentMap;
            CellRect site = default;
            bool haveSite = false;
            var manifest = new List<string>();
            foreach (string raw in spec)
            {
                string line = raw.Split('#')[0].Trim();
                if (line.Length == 0) continue;
                string[] p = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (p[0])
                {
                    case "site":
                        site = Site(map, Int(p[1]), Int(p[2]));
                        haveSite = true;
                        break;
                    case "thing":
                    {
                        if (!haveSite) throw new FormatException("'site' must come first");
                        Thing thing = Make(p[2], p[3]);
                        foreach (string option in p.Skip(7))
                        {
                            string[] kv = option.Split('=');
                            if (kv[0] == "quality")
                                (thing.TryGetComp<CompQuality>() ?? throw new FormatException($"{p[2]} has no quality"))
                                    .SetQuality((QualityCategory)Enum.Parse(typeof(QualityCategory), kv[1]), ArtGenerationContext.Colony);
                            else if (kv[0] == "hp")
                                thing.HitPoints = Math.Max(1, (int)(thing.MaxHitPoints * float.Parse(kv[1], CultureInfo.InvariantCulture)));
                            else throw new FormatException("unknown option " + option);
                        }
                        if (thing.def.CanHaveFaction) thing.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(thing, Cell(site, p[4], p[5]), map, Rot4.FromString(p[6]));
                        manifest.Add(Describe(p[1], thing));
                        break;
                    }
                    case "items":
                    {
                        if (!haveSite) throw new FormatException("'site' must come first");
                        int n = Int(p[4]), width = Int(p[7]);
                        IntVec3 corner = Cell(site, p[5], p[6]);
                        for (int i = 0; i < n; i++)
                        {
                            Thing item = Make(p[2], p[3]);
                            GenSpawn.Spawn(item, corner + new IntVec3(i % width, 0, i / width), map);
                            manifest.Add(Describe(p[1], item));
                        }
                        break;
                    }
                    default:
                        throw new FormatException("unknown directive: " + line);
                }
            }
            File.WriteAllLines(Path.Combine(Dir, "manifest.txt"), manifest);
            return manifest.Count;
        }

        private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        private static IntVec3 Cell(CellRect site, string x, string z) => new IntVec3(site.minX + Int(x), 0, site.minZ + Int(z));

        private static Thing Make(string defName, string stuff)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
            ThingDef stuffDef = stuff == "-" ? (def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null) : DefDatabase<ThingDef>.GetNamed(stuff);
            return ThingMaker.MakeThing(def, stuffDef);
        }

        private static string Describe(string tag, Thing t)
        {
            string quality = t.TryGetQuality(out QualityCategory q) ? q.ToString() : "-";
            string title = t.TryGetComp<CompArt>() is CompArt art && art.Active ? art.Title : "-";
            return string.Join("\t", tag, t.ThingID, t.def.defName, t.Stuff?.defName ?? "-", t.Position.x, t.Position.z,
                t.Rotation.ToStringHuman(), t.HitPoints, quality, title);
        }

        /// <summary>The nearest clear width×height rect to the first colonist, with 2 free cells around, reachable.</summary>
        private static CellRect Site(Map map, int width, int height)
        {
            IntVec3 from = map.mapPawns.FreeColonistsSpawned.First().Position;
            // Walkable, not Standable: trees and items pass, and are cleared below (as tools/game-side/Fixtures.cs Site).
            bool Clear(IntVec3 c) => c.InBounds(map) && c.Walkable(map) && !c.Fogged(map) && !c.Roofed(map)
                && !c.GetTerrain(map).IsWater && c.GetFirstBuilding(map) == null && map.zoneManager.ZoneAt(c) == null;
            CellRect At(IntVec3 c) => new CellRect(c.x - width / 2, c.z - height / 2, width, height);
            IntVec3? found = CellRect.CenteredOn(from, 50).ClipInsideMap(map).Cells
                .OrderBy(c => c.DistanceToSquared(from))
                .Select(c => (IntVec3?)c)
                .FirstOrDefault(c => !At(c.Value).ExpandedBy(2).Contains(from) && At(c.Value).ExpandedBy(2).Cells.All(Clear)
                    && map.reachability.CanReach(from, c.Value, PathEndMode.OnCell, TraverseParms.For(TraverseMode.PassDoors)));
            if (found == null) throw new InvalidOperationException($"no clear {width}x{height} site within 50 cells of {from}");
            CellRect rect = At(found.Value);
            foreach (IntVec3 c in rect.ExpandedBy(2).Cells)
                foreach (Thing t in c.GetThingList(map).Where(t => t.def.category == ThingCategory.Plant || t.def.category == ThingCategory.Item).ToList())
                    t.Destroy();
            return rect;
        }

        private void Complete(string text)
        {
            File.WriteAllText(Path.Combine(Dir, "result"), text + "\n");
            Log.Message("[PreModBuilder] " + text);
            finished = true;
        }
    }
}
