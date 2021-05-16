using NLog;
using QuantumHangar.HangarChecks;
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
using System.Threading.Tasks;
using System.Timers;
using VRage.Game;

namespace QuantumHangar
{
    public static class AutoHangar
    {
        private static Timer UpdateTimer = new Timer(1800000);
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static Settings Config { get { return Hangar.Config; } }



        public static void StartAutoHangar()
        {
            UpdateTimer.Elapsed += UpdateTimer_Elapsed;
            UpdateTimer.Start();
        }

        private static void UpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if(Config.AutoHangarGrids)
                RunAutoHangar();
        }

        public static void RunAutoHangar()
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready || MySandboxGame.IsPaused)
                return;

           

            try
            {
                AutoHangarWorker();
            }catch(Exception ex)
            {
                Log.Error(ex);
            }
        }

        private static void AutoHangarWorker()
        {

            //Significant performance increase
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            List<MyCubeGrid> ExportedGrids = new List<MyCubeGrid>();
            List<long> ExportPlayerIdentities = new List<long>();

            Log.Warn("AutoHangar: Getting Players!");
            
            foreach (MyIdentity player in MySession.Static.Players.GetAllIdentities())
            {
                if (player == null)
                {
                    continue;
                }

                DateTime LastLogin;
                LastLogin = player.LastLoginTime;

                ulong SteamID = MySession.Static.Players.TryGetSteamId(player.IdentityId);

                if (SteamID == 0)
                    continue;

                if (LastLogin.AddDays(Config.AutoHangarDayAmount) < DateTime.Now)
                {
                    //AutoHangarBlacklist
                    if (!Config.AutoHangarPlayerBlacklist.Any(x => x.SteamID == SteamID))
                    {
                        ExportPlayerIdentities.Add(player.IdentityId);
                    }
                }
            }

            Log.Warn("AutoHangar: Total players to check:" + ExportPlayerIdentities.Count());
            int GridCounter = 0;

            //This gets all the grids
            foreach (long player in ExportPlayerIdentities)
            {
                ulong id = MySession.Static.Players.TryGetSteamId(player);



                //string path = GridMethods.CreatePathForPlayer(Config.FolderDirectory, id);
                
                

                ConcurrentBag<List<MyCubeGrid>> gridGroups = GridUtilities.FindGridList(player, Config.EnableSubGrids);
                //Log.Warn(player + ":" + gridGroups.Count);

                if (gridGroups.Count == 0)
                    continue;


                long LargestGridID = 0;

                if (Config.KeepPlayersLargestGrid)
                {
                    //First need to find their largets grid
                    int BlocksCount = 0;

                    foreach (List<MyCubeGrid> grids in gridGroups)
                    {
                        int GridBlockCounts = 0;
                        int LargestSingleGridCount = 0;
                        MyCubeGrid LargetsGrid = grids[0];
                        foreach (MyCubeGrid grid in grids)
                        {
                            if (grid.BlocksCount > LargestSingleGridCount)
                            {
                                LargestSingleGridCount = grid.BlocksCount;
                                LargetsGrid = grid;
                            }
                        }

                        GridBlockCounts = LargetsGrid.BlocksCount;

                        if (GridBlockCounts > BlocksCount)
                        {
                            BlocksCount = GridBlockCounts;
                            LargestGridID = LargetsGrid.EntityId;
                        }

                    }
                }


                if (gridGroups.Count == 0)
                    continue;

                PlayerHangar PlayersHangar = new PlayerHangar(id, null);
                foreach (List<MyCubeGrid> grids in gridGroups)
                {
                    if (grids.Count == 0)
                        continue;


                    if (grids[0].IsRespawnGrid && Config.DeleteRespawnPods)
                    {
                        grids[0].Close();
                        continue;
                    }


                    GridResult Result = new GridResult();
                    Result.Grids = grids;

                    var BiggestGrid = grids[0];
                    foreach (MyCubeGrid grid in grids)
                    {
                        if (grid.BlocksCount > BiggestGrid.BlocksCount)
                        {
                            BiggestGrid = grid;
                        }
                    }

                    Result.BiggestGrid = BiggestGrid;

                    if (Config.KeepPlayersLargestGrid)
                    {
                        if (BiggestGrid.EntityId == LargestGridID)
                        {
                            //Skip players largest grid
                            continue;
                        }
                    }


                    //Grid Size Checks
                    if (BiggestGrid.GridSizeEnum == MyCubeSize.Large)
                    {
                        if (BiggestGrid.IsStatic && !Config.AutoHangarStaticGrids)
                        {
                            continue;
                        }
                        else if (!BiggestGrid.IsStatic && !Config.AutoHangarLargeGrids)
                        {
                            continue;
                        }
                    }
                    else if (BiggestGrid.GridSizeEnum == MyCubeSize.Small && !Config.AutoHangarSmallGrids)
                    {
                        continue;
                    }


                    GridStamp Stamp = Result.GenerateGridStamp();
                    GridUtilities.FormatGridName(PlayersHangar, Stamp);



                    if (PlayersHangar.SaveGridsToFile(Result, Stamp.GridName))
                    {
                        //Load player file and update!
                        //Fill out grid info and store in file
                        PlayersHangar.SaveGridStamp(Stamp, false, true);
                        GridCounter++;
                        Log.Info(Result.BiggestGrid.DisplayName + " was sent to Hangar due to inactivity!");
                    }
                    else
                        Log.Info(Result.BiggestGrid.DisplayName + " FAILED to Hangar due to inactivity!");


                }

                //Save players file!
                PlayersHangar.SavePlayerFile();
            }
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;

            Log.Warn("Finished Hangaring: " + GridCounter + " grids! Action took: " + ts.ToString());
        }

        public static void Dispose()
        {
            UpdateTimer.Elapsed -= UpdateTimer_Elapsed;
            UpdateTimer.Stop();
        }
    }
}
