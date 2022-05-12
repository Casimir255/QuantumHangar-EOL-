using NLog;
using QuantumHangar.HangarChecks;
using QuantumHangar.Utilities;
using QuantumHangar.Utils;
using Sandbox;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using VRage.Game;
using VRage.Groups;
using VRage.Utils;

namespace QuantumHangar
{
    public static class AutoHangar
    {
        //1800000
        private static System.Timers.Timer UpdateTimer = new System.Timers.Timer(1800000);
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static Settings Config { get { return Hangar.Config; } }


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

            RunAutoHangar(false, Config.AutoHangarStaticGrids, Config.AutoHangarLargeGrids, Config.AutoHangarSmallGrids, Config.KeepPlayersLargestGrid);
            ScheduleAutoHangar = false;
        }


        public static async void RunAutoHangar(bool SaveAll, bool hangarStatic = true, bool hangarLarge = true, bool hangarSmall = true, bool hangarLargest = true)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready || MySandboxGame.IsPaused)
                return;



            Watcher.Reset();
            Watcher.Start();

            List<long> ExportPlayerIdentities = new List<long>();



            try
            {
                //Scan all the identities we need
                foreach (MyIdentity Identity in MySession.Static.Players.GetAllIdentities())
                {
                    //No need to check this identity. Its an NPC
                    if (string.IsNullOrEmpty(Identity.DisplayName) || MySession.Static.Players.IdentityIsNpc(Identity.IdentityId))
                        continue;


                    //Funcky SteamID check
                    DateTime LastLogin = Identity.LastLoginTime;
                    ulong SteamID = MySession.Static.Players.TryGetSteamId(Identity.IdentityId);
                    if (SteamID == 0)
                        continue;



                    //Need to see if we need to check this identity
                    if (SaveAll)
                    {
                        ExportPlayerIdentities.Add(Identity.IdentityId);
                    }
                    else if (!SaveAll && LastLogin.AddDays(Config.AutoHangarDayAmount) < DateTime.Now && !Config.AutoHangarPlayerBlacklist.Any(x => x.SteamID == SteamID))
                    {
                        ExportPlayerIdentities.Add(Identity.IdentityId);
                    }

                }

                Log.Warn($"AutoHangar Running! Total players to check {ExportPlayerIdentities.Count()}");
               

                //Dictionary for a list of all the grids we need to remove
                Dictionary<long, List<AutoHangarItem>> scannedGrids = new Dictionary<long, List<AutoHangarItem>>();

                foreach (var group in MyCubeGridGroups.Static.Physical.Groups.ToList())
                {
                    if (group.Nodes.Count == 0)
                        continue;

                    int totalBlocks = 0;
                    List<MyCubeGrid> grids = new List<MyCubeGrid>();
                    foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                    {

                        MyCubeGrid grid = groupNodes.NodeData;

                        if (grid == null || grid.MarkedForClose || grid.MarkedAsTrash)
                            continue;

                        totalBlocks += grid.BlocksCount;
                        grids.Add(grid);
                    }

                    //Dont add if this grid group is null/empty
                    if (grids.Count == 0)
                        continue;

                    //Get biggest grid owner, and see if they are an identity that we need to export
                    grids.BiggestGrid(out MyCubeGrid LargestGrid);
                    long biggestOwner = LargestGrid.GetBiggestOwner();


                    //remove respawn grids
                    if (LargestGrid.IsRespawnGrid && Config.DeleteRespawnPods)
                    {
                        //Close all grids
                        grids.Close("Autohangar deleted respawn pod");
                        continue;
                    }

                    //No need to autohangar if the largest owner is an NPC
                    if (MySession.Static.Players.IdentityIsNpc(biggestOwner))
                        continue;

                    //Now see if we should hangar this shit
                    if (LargestGrid.GridSizeEnum == MyCubeSize.Large && LargestGrid.IsStatic && !hangarStatic)
                        continue;
                    else if (LargestGrid.GridSizeEnum == MyCubeSize.Large && !LargestGrid.IsStatic && !hangarLarge)
                        continue;
                    else if (LargestGrid.GridSizeEnum == MyCubeSize.Small && !hangarSmall)
                        continue;



                    //Add this new grid into our planned export queue
                    if (ExportPlayerIdentities.Contains(biggestOwner))
                    {
                        AutoHangarItem hangar = new AutoHangarItem(totalBlocks, grids, LargestGrid);
                        if (!scannedGrids.ContainsKey(biggestOwner))
                        {
                            scannedGrids.Add(biggestOwner, new List<AutoHangarItem>() { hangar });
                        }
                        else
                        {
                            scannedGrids[biggestOwner].Add(hangar);
                        }
                    }
                }


                int GridCounter = await SaveAutohangarGrids(scannedGrids);
             
                Watcher.Stop();
                Log.Warn($"Finished Hangaring: {GridCounter} grids! Action took: {Watcher.Elapsed}");

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

        public static async Task<int> SaveAutohangarGrids(Dictionary<long, List<AutoHangarItem>> scannedGrids)
        {
            int GridCounter = 0;


            try
            {
                
                //Now that we have our complete collection, lets loop through the grids
                foreach (KeyValuePair<long, List<AutoHangarItem>> kvp in scannedGrids)
                {
                    List<AutoHangarItem> allGrids = kvp.Value;

                    if (Config.KeepPlayersLargestGrid)
                    {
                        AutoHangarItem largest = allGrids.Aggregate((i1, i2) => i1.blocksCount > i2.blocksCount ? i1 : i2);
                        allGrids.Remove(largest);
                    }

                    //No sense in running everything if this list is empty
                    if (allGrids.Count == 0)
                        continue;


                    //Grab Players Hangar
                    ulong id = MySession.Static.Players.TryGetSteamId(kvp.Key);
                    PlayerHangar PlayersHangar = new PlayerHangar(id, null);

                    foreach (AutoHangarItem item in allGrids)
                    {
                        GridResult Result = new GridResult();
                        Result.Grids = item.grids;
                        Result.BiggestGrid = item.largestGrid;

                        GridStamp Stamp = Result.GenerateGridStamp();
                        PlayersHangar.SelectedPlayerFile.FormatGridName(Stamp);


                        bool val = await PlayersHangar.SaveGridsToFile(Result, Stamp.GridName);
                        if (val)
                        {
                            //Load player file and update!
                            //Fill out grid info and store in file
                            PlayersHangar.SaveGridStamp(Stamp, false, true);
                            GridCounter++;
                            Log.Info(Result.BiggestGrid.DisplayName + " was sent to Hangar due to inactivity!");
                        }
                        else
                            Log.Info(Result.BiggestGrid.DisplayName + " FAILED to Hangar! Check read error above for more info!");


                    }

                    PlayersHangar.SavePlayerFile();
                }

                return GridCounter;

            }
            catch(Exception ex)
            {
                Log.Error(ex);
            }

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

        public int blocksCount = 0;
        public List<MyCubeGrid> grids;
        public MyCubeGrid largestGrid;

        public AutoHangarItem(int blocks, List<MyCubeGrid> grids, MyCubeGrid largestGrid)
        {
            this.blocksCount = blocks;
            this.grids = grids;
            this.largestGrid = largestGrid;
        }

    }
}
