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
using VRage.Game.ModAPI;
using VRage.Network;

namespace QuantumHangar
{
    public static class AutoHangar
    {
        //1800000
        private static Timer _updateTimer = new Timer(1800000);
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static Settings Config => Hangar.Config;


        public static bool ScheduleAutoHangar = false;
        private static Stopwatch _watcher = new Stopwatch();

        public static void StartAutoHangar()
        {
            //Every 30min schedule autohangar
            _updateTimer.Elapsed += UpdateTimer_Elapsed;
            _updateTimer.Start();
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


        public static void RunAutoHangar(bool saveAll, bool hangarStatic = true, bool hangarLarge = true,
            bool hangarSmall = true, bool hangarLargest = true)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready || MySandboxGame.IsPaused)
                return;


            var exportPlayerIdentities = new List<long>();
            var playersOfflineDays = new Dictionary<long, int>();
            try
            {
                //Scan all the identities we need
                foreach (var identity in MySession.Static.Players.GetAllIdentities())
                {
                    //No need to check this identity. Its an NPC
                    if (string.IsNullOrEmpty(identity.DisplayName) ||
                        MySession.Static.Players.IdentityIsNpc(identity.IdentityId))
                        continue;


                    //Funcky SteamID check
                    var lastLogin = identity.LastLoginTime;
                    var steamId = MySession.Static.Players.TryGetSteamId(identity.IdentityId);
                    if (steamId == 0)
                        continue;
                    int lowestValue = Math.Min(Config.AutoHangarDayAmountStation, Math.Min(Config.AutoHangarDayAmountLargeGrid, Config.AutoHangarDayAmountSmallGrid));
                    var lowest = Config.AutoHangarGridsByType ? lowestValue : Config.AutoHangarDayAmount;

                    switch (saveAll)
                    {
                        //Need to see if we need to check this identity
                        case true:
                        case false when lastLogin.AddDays(lowest) < DateTime.Now &&
                                    !Config.AutoHangarPlayerBlacklist.Any(x => x.SteamId == steamId):
                            exportPlayerIdentities.Add(identity.IdentityId);
                            playersOfflineDays[identity.IdentityId] = (int)(DateTime.Now - lastLogin).TotalDays;
                            break;
                    }
                }

                Log.Warn($"AutoHangar Running! Total players to check {exportPlayerIdentities.Count()}");


                //Dictionary for a list of all the grids we need to remove
                var scannedGrids = new Dictionary<long, List<AutoHangarItem>>();

                foreach (var group in MyCubeGridGroups.Static.Physical.Groups.ToList())
                {
                    if (group.Nodes.Count == 0)
                        continue;

                    var grids = new List<MyCubeGrid>();
                    foreach (var grid in group.Nodes.Select(groupNodes => groupNodes.NodeData).Where(grid => grid != null && !grid.MarkedForClose && !grid.MarkedAsTrash))
                    {
                        var owner = GridUtilities.GetBiggestOwner(grid);
                        if (!exportPlayerIdentities.Contains(owner))
                        {
                            continue;
                        }

                        grids.Add(grid);
                    }

                    var ByPlayer = new Dictionary<long, List<MyCubeGrid>>();

                    foreach (var grid in grids)
                    {
                        var owner = GridUtilities.GetBiggestOwner(grid);

                        if (ByPlayer.TryGetValue(owner, out var items))
                        {
                            items.Add(grid);
                        }
                        else
                        {
                            ByPlayer.Add(owner, new List<MyCubeGrid>() { grid });
                        }
                    }

                    foreach (var gridList in ByPlayer)
                    {
                        if (!exportPlayerIdentities.Contains(gridList.Key)) continue;
                        var totalBlocks = 0;
                        //Dont add if this grid group is null/empty
                        if (gridList.Value.Count == 0)
                            continue;

                        totalBlocks += gridList.Value.Select(x => x.BlocksCount).Sum();
                        //Get biggest grid owner, and see if they are an identity that we need to export
                        gridList.Value.BiggestGrid(out var largestGrid);
                        //No need to autohangar if the largest owner is an NPC
                        if (MySession.Static.Players.IdentityIsNpc(gridList.Key))
                            continue;
                        var exportedGrids = new List<MyCubeGrid>();

                        var staticDays = Config.AutoHangarGridsByType ? Config.AutoHangarDayAmountStation : Config.AutoHangarDayAmount;
                        var largeDays = Config.AutoHangarGridsByType ? Config.AutoHangarDayAmountLargeGrid : Config.AutoHangarDayAmount;
                        var smallDays = Config.AutoHangarGridsByType ? Config.AutoHangarDayAmountSmallGrid : Config.AutoHangarDayAmount;

                        if (hangarStatic && playersOfflineDays[gridList.Key] >= staticDays)
                        {
                            exportedGrids.AddRange(gridList.Value.Where(x => x.GridSizeEnum == MyCubeSize.Large && x.IsStatic).ToList());
                        }
                        if (hangarLarge && playersOfflineDays[gridList.Key] >= largeDays)
                        {
                            exportedGrids.AddRange(gridList.Value.Where(x => x.GridSizeEnum == MyCubeSize.Large && !x.IsStatic).ToList());
                        }
                        if (hangarSmall && playersOfflineDays[gridList.Key] >= smallDays)
                        {
                            exportedGrids.AddRange(gridList.Value.Where(x => x.GridSizeEnum == MyCubeSize.Small).ToList());
                        }

                        if (!exportedGrids.Any())
                        {
                            continue;
                        }
                        //Add this new grid into our planned export queue

                        var hangar = new AutoHangarItem(totalBlocks, exportedGrids, largestGrid);
                        if (!scannedGrids.ContainsKey(gridList.Key))
                            scannedGrids.Add(gridList.Key, new List<AutoHangarItem>() { hangar });
                        else
                            scannedGrids[gridList.Key].Add(hangar);
                    }
                }

                Task.Run(() => SaveAutoHangarGrids(scannedGrids));
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                exportPlayerIdentities.Clear();
                _watcher.Stop();
            }
        }

        public static async Task<int> SaveAutoHangarGrids(Dictionary<long, List<AutoHangarItem>> scannedGrids)
        {
            var gridCounter = 0;


            try
            {
                _watcher.Reset();
                _watcher.Start();


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
                    var playersHangar = new PlayerHangar(id, null);

                    foreach (var item in allGrids)
                    {
                        //remove respawn grids
                        if (item.LargestGrid.IsRespawnGrid && Config.DeleteRespawnPods)
                        {
                            //Close all grids
                            item.Grids.Close("AutoHangar deleted respawn pod");
                            continue;
                        }


                        var result = new GridResult
                        {
                            Grids = item.Grids,
                            BiggestGrid = item.LargestGrid
                        };


                        var stamp = result.GenerateGridStamp();
                        playersHangar.SelectedPlayerFile.FormatGridName(stamp);


                        var val = await playersHangar.SaveGridsToFile(result, stamp.GridName);
                        if (val)
                        {
                            //Load player file and update!
                            //Fill out grid info and store in file
                            playersHangar.SaveGridStamp(stamp, false, true);
                            gridCounter++;
                            Log.Info(result.BiggestGrid.DisplayName + " was sent to Hangar due to inactivity!");
                        }
                        else
                        {
                            Log.Info(result.BiggestGrid.DisplayName +
                                     " FAILED to Hangar! Check read error above for more info!");
                        }
                    }

                    playersHangar.SavePlayerFile();
                }

                _watcher.Stop();
                Log.Warn($"Finished Hangaring: {gridCounter} grids! Action took: {_watcher.Elapsed}");

                return gridCounter;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }

            _watcher.Stop();
            return gridCounter;
        }


        public static void Dispose()
        {
            _updateTimer.Elapsed -= UpdateTimer_Elapsed;
            _updateTimer.Stop();
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