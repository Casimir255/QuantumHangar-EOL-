using NLog;
using QuantumHangar.HangarChecks;
using QuantumHangar.Utils;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using VRage.Game;

namespace QuantumHangar
{
    public static class AutoHangar
    {
        //1800000
        private static Timer UpdateTimer = new Timer(1800000);
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static Settings Config => Hangar.Config;


        public static bool ScheduleAutoHangar = false;
        private static Stopwatch Watcher = new Stopwatch();

        public static void StartAutoHangar()
        {
            //Every 30min schedule autohangar
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;
            UpdateTimer.Start();
        }


        private static void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Config.AutoHangarGrids && Config.PluginEnabled)
                ScheduleAutoHangar = true;
        }


        public static void UpdateAutoHangar()
        {
            //Log.Info("Hit");
            if (!ScheduleAutoHangar)
                return;

            RunAutoHangar(false, Config.AutoHangarStaticGrids, Config.AutoHangarLargeGrids, Config.AutoHangarSmallGrids,
                Config.KeepPlayersLargestGrid);
            ScheduleAutoHangar = false;
        }


        public static void RunAutoHangar(bool SaveAll, bool hangarStatic = true, bool hangarLarge = true,
            bool hangarSmall = true, bool hangarLargest = true)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready || MySandboxGame.IsPaused)
                return;


            var ExportPlayerIdentities = new List<long>();

            try
            {
                //Scan all the identities we need
                foreach (var Identity in MySession.Static.Players.GetAllIdentities())
                {
                    //No need to check this identity. Its an NPC
                    if (string.IsNullOrEmpty(Identity.DisplayName) ||
                        MySession.Static.Players.IdentityIsNpc(Identity.IdentityId))
                        continue;


                    //Funcky SteamID check
                    var LastLogin = Identity.LastLoginTime;
                    var SteamID = MySession.Static.Players.TryGetSteamId(Identity.IdentityId);
                    if (SteamID == 0)
                        continue;

                    switch (SaveAll)
                    {
                        //Need to see if we need to check this identity
                        case true:
                        case false when LastLogin.AddDays(Config.AutoHangarDayAmount) < DateTime.Now &&
                                        !Config.AutoHangarPlayerBlacklist.Any(x => x.SteamID == SteamID):
                            ExportPlayerIdentities.Add(Identity.IdentityId);
                            break;
                    }
                }

                Log.Warn($"AutoHangar Running! Total players to check {ExportPlayerIdentities.Count()}");


                //Dictionary for a list of all the grids we need to remove
                var scannedGrids = new Dictionary<long, List<AutoHangarItem>>();

                foreach (var group in MyCubeGridGroups.Static.Physical.Groups.ToList())
                {
                    if (group.Nodes.Count == 0)
                        continue;

                    var totalBlocks = 0;
                    var grids = new List<MyCubeGrid>();
                    foreach (var grid in group.Nodes.Select(groupNodes => groupNodes.NodeData).Where(grid => grid != null && !grid.MarkedForClose && !grid.MarkedAsTrash))
                    {
                        totalBlocks += grid.BlocksCount;
                        grids.Add(grid);
                    }

                    //Dont add if this grid group is null/empty
                    if (grids.Count == 0)
                        continue;

                    //Get biggest grid owner, and see if they are an identity that we need to export
                    grids.BiggestGrid(out var LargestGrid);
                    var biggestOwner = LargestGrid.GetBiggestOwner();

                    //No need to autohangar if the largest owner is an NPC
                    if (MySession.Static.Players.IdentityIsNpc(biggestOwner))
                        continue;

                    switch (LargestGrid.GridSizeEnum)
                    {
                        //Now see if we should hangar this shit
                        case MyCubeSize.Large when LargestGrid.IsStatic && !hangarStatic:
                        case MyCubeSize.Large when !LargestGrid.IsStatic && !hangarLarge:
                        case MyCubeSize.Small when !hangarSmall:
                            continue;
                    }


                    //Add this new grid into our planned export queue
                    if (!ExportPlayerIdentities.Contains(biggestOwner)) continue;
                    var hangar = new AutoHangarItem(totalBlocks, grids, LargestGrid);
                    if (!scannedGrids.ContainsKey(biggestOwner))
                        scannedGrids.Add(biggestOwner, new List<AutoHangarItem>() { hangar });
                    else
                        scannedGrids[biggestOwner].Add(hangar);
                }


                Task.Run(() => SaveAutoHangarGrids(scannedGrids));
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                ExportPlayerIdentities.Clear();
                Watcher.Stop();
            }
        }

        public static async Task<int> SaveAutoHangarGrids(Dictionary<long, List<AutoHangarItem>> scannedGrids)
        {
            var GridCounter = 0;


            try
            {
                Watcher.Reset();
                Watcher.Start();


                //Now that we have our complete collection, lets loop through the grids
                foreach (var (k, allGrids) in scannedGrids)
                {
                    if (Config.KeepPlayersLargestGrid)
                    {
                        var largest = allGrids.Aggregate((i1, i2) => i1.BlocksCount > i2.BlocksCount ? i1 : i2);
                        allGrids.Remove(largest);
                    }

                    //No sense in running everything if this list is empty
                    if (allGrids.Count == 0)
                        continue;


                    //Grab Players Hangar
                    var id = MySession.Static.Players.TryGetSteamId(k);
                    var PlayersHangar = new PlayerHangar(id, null);

                    foreach (var item in allGrids)
                    {
                        //remove respawn grids
                        if (item.LargestGrid.IsRespawnGrid && Config.DeleteRespawnPods)
                        {
                            //Close all grids
                            item.Grids.Close("AutoHangar deleted respawn pod");
                            continue;
                        }


                        var Result = new GridResult
                        {
                            Grids = item.Grids,
                            BiggestGrid = item.LargestGrid
                        };


                        var Stamp = Result.GenerateGridStamp();
                        PlayersHangar.SelectedPlayerFile.FormatGridName(Stamp);


                        var val = await PlayersHangar.SaveGridsToFile(Result, Stamp.GridName);
                        if (val)
                        {
                            //Load player file and update!
                            //Fill out grid info and store in file
                            PlayersHangar.SaveGridStamp(Stamp, false, true);
                            GridCounter++;
                            Log.Info(Result.BiggestGrid.DisplayName + " was sent to Hangar due to inactivity!");
                        }
                        else
                        {
                            Log.Info(Result.BiggestGrid.DisplayName +
                                     " FAILED to Hangar! Check read error above for more info!");
                        }
                    }

                    PlayersHangar.SavePlayerFile();
                }

                Watcher.Stop();
                Log.Warn($"Finished Hangaring: {GridCounter} grids! Action took: {Watcher.Elapsed}");

                return GridCounter;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            Watcher.Stop();
            return GridCounter;
        }


        public static void Dispose()
        {
            UpdateTimer.Elapsed -= UpdateTimer_Elapsed;
            UpdateTimer.Stop();
        }
    }

    public class AutoHangarItem
    {
        public int BlocksCount = 0;
        public List<MyCubeGrid> Grids;
        public MyCubeGrid LargestGrid;

        public AutoHangarItem(int blocks, List<MyCubeGrid> grids, MyCubeGrid largestGrid)
        {
            BlocksCount = blocks;
            Grids = grids;
            LargestGrid = largestGrid;
        }
    }
}