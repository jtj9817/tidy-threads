using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RemovalProbe
{
    // Test-only mod, built and enabled by autotest.py removal-check (tools/autotest.py in the mod's repo).
    // No reference to the mod's assembly and nothing saved. The mod identity it checks comes from <dir>/mod.txt
    // (assembly name, packageId, defName prefix — one per line; empty lines disable that check).
    [StaticConstructorOnStartup]
    public static class Boot
    {
        static Boot()
        {
            if (!GenCommandLine.TryGetCommandLineArg("modcenter-removal-probe", out string dir)) return;
            var go = new GameObject("RemovalProbe");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<Probe>().Dir = dir;
        }
    }

    public class Probe : MonoBehaviour
    {
        public string Dir;
        private int stage, startTick, captureFrames;
        private float nextBeat;
        private Game before;
        private bool finished;
        private string modAssembly, packageId, defPrefix;
        private const string Resave = "removal_probe_resaved";

        private void Update()
        {
            if (finished || Dir == null) return;
            try
            {
                if (modAssembly == null)
                {
                    string[] info = File.ReadAllLines(Path.Combine(Dir, "mod.txt"));
                    modAssembly = info.Length > 0 ? info[0] : "";
                    packageId = info.Length > 1 ? info[1] : "";
                    defPrefix = info.Length > 2 ? info[2] : "";
                }
                if (Time.realtimeSinceStartup >= nextBeat)
                {
                    nextBeat = Time.realtimeSinceStartup + 1f;
                    File.WriteAllText(Path.Combine(Dir, "heartbeat"), stage.ToString());
                }
                if (Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null
                    || LongEventHandler.ShouldWaitForEvent || !Current.Game.PlayerHasControl) return;
                foreach (Window window in Find.WindowStack.Windows.Where(w => w.forcePause).ToList()) window.Close(false);
                Find.TickManager.CurTimeSpeed = TimeSpeed.Ultrafast;
                if (stage == 0)
                {
                    CheckColony();
                    startTick = Find.TickManager.TicksGame;
                    stage = 1;
                    Log.Message("[RemovalProbe] colony loaded without the mod; observing 600 ticks");
                }
                else if (stage == 1 && Find.TickManager.TicksGame - startTick >= 600)
                {
                    CheckColony();
                    string path = GenFilePaths.FilePathForSavedGame(Resave);
                    if (File.Exists(path)) File.Delete(path);
                    GameDataSaveLoader.SaveGame(Resave);
                    if (!File.Exists(path)) throw new IOException("The no-mod resave was not written.");
                    before = Current.Game;
                    stage = 2;
                    GameDataSaveLoader.LoadGame(Resave);
                }
                else if (stage == 2 && Current.Game != before)
                {
                    CheckColony();
                    startTick = Find.TickManager.TicksGame;
                    stage = 3;
                }
                else if (stage == 3 && Find.TickManager.TicksGame - startTick >= 600)
                {
                    CheckColony();
                    Find.CameraDriver.SetRootPosAndSize(Find.CurrentMap.Center.ToVector3Shifted(), 16f);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "removed-colony.png"));
                    stage = 4;
                }
                else if (stage == 4 && ++captureFrames >= 10)
                {
                    Complete("PASS: loaded, ticked 600, saved, reloaded and ticked 600; expected vanilla contents retained; mod assembly absent");
                }
            }
            catch (Exception e)
            {
                Log.Error("[RemovalProbe] " + e);
                Complete("FAIL: " + e);
            }
        }

        private void CheckColony()
        {
            if ((modAssembly != "" && AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == modAssembly))
                || (packageId != "" && LoadedModManager.RunningMods.Any(m => m.PackageId == packageId)))
                throw new InvalidOperationException("The mod is still loaded; not a removal proof.");
            var ids = new HashSet<string>();
            var contents = new List<Thing>();
            foreach (Map map in Find.Maps)
            {
                ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForGroup(ThingRequestGroup.Everything), contents);
                foreach (Thing thing in contents)
                {
                    if (IsModContent(thing.def.defName)) throw new InvalidOperationException("Mod content remains: " + thing);
                    ids.Add(thing.ThingID);
                }
            }
            foreach (IThingHolder holder in Find.WorldObjects.AllWorldObjects.OfType<IThingHolder>())
            {
                ThingOwnerUtility.GetAllThingsRecursively(holder, contents);
                foreach (Thing thing in contents)
                {
                    if (IsModContent(thing.def.defName)) throw new InvalidOperationException("Mod world content remains: " + thing);
                    ids.Add(thing.ThingID);
                }
            }
            foreach (LogEntry entry in Find.BattleLog.Battles.SelectMany(b => b.Entries))
            {
                ids.Add(entry.GetUniqueLoadID());
                // Exercise the history's actual grammar/render text path after optional weapon/cover removal.
                entry.ToGameStringFromPOV(entry.GetConcerns().FirstOrDefault());
            }
            foreach (string id in File.ReadAllLines(Path.Combine(Dir, "expected.txt")))
                if (!ids.Contains(id)) throw new InvalidOperationException("Vanilla content was lost: " + id);
        }

        private bool IsModContent(string defName)
        {
            return defPrefix != "" && (defName.StartsWith(defPrefix, StringComparison.Ordinal)
                                       || defName.StartsWith("Corpse_" + defPrefix, StringComparison.Ordinal));
        }

        private void Complete(string text)
        {
            File.WriteAllText(Path.Combine(Dir, "result"), text + "\n");
            Log.Message("[RemovalProbe] " + text);
            finished = true;
        }
    }
}
