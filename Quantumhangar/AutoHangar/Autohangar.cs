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
            if (Config.AutoHangarGrids && Config.PluginEnabled)
                RunAutoHangar(false, Config.AutoHangarStaticGrids, Config.AutoHangarLargeGrids, Config.AutoHangarSmallGrids, Config.KeepPlayersLargestGrid);
        }


        public static void RunAutoHangar(bool SaveAll, bool hangarStatic = false, bool hangarLarge = false, bool hangarSmall = false, bool hangarLargest = false)
        {
            if (!Hangar.ServerRunning || !MySession.Static.Ready || MySandboxGame.IsPaused)
                return;

            //Significant performance increase
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

  
            List<long> ExportPlayerIdentities = new List<long>();

            Log.Warn("AutoHangar Starting!");

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

                Log.Warn("AutoHangar: Total players to check:" + ExportPlayerIdentities.Count());



                int GridCounter = 0;

                //This gets all the grids for each player
                foreach (long player in ExportPlayerIdentities)
                {

                    //List of all girs
                    ConcurrentBag<List<MyCubeGrid>> gridGroups = GridUtilities.FindGridList(player, Config.EnableSubGrids);


                    if (gridGroups.Count == 0)
                        continue;


                    long? LargestGridID = -1;

                    //Keep largest
                    if (!hangarLargest)
                    {
                        //If they only have one set of grids left, it must be their largest!
                        if (gridGroups.Count == 1)
                            continue;


                        //First need to find their largest grid in each collection
                        int BlocksCount = 0;


                        //Obtain biggest grid from all collected grids
                        foreach (List<MyCubeGrid> grids in gridGroups)
                        {
                            if (grids.Count == 0)
                                continue;

                            grids.BiggestGrid(out MyCubeGrid LargestGrid);

                            if (LargestGrid.BlocksCount > BlocksCount)
                            {
                                BlocksCount = LargestGrid.BlocksCount;
                                LargestGridID = LargestGrid.EntityId;
                            }
                        }
                    }





                    ulong id = MySession.Static.Players.TryGetSteamId(player);
                    PlayerHangar PlayersHangar = new PlayerHangar(id, null);


                    foreach (List<MyCubeGrid> grids in gridGroups)
                    {
                        if (grids.Count == 0)
                            continue;

                        //Obtain the biggest grid
                        grids.BiggestGrid(out MyCubeGrid LargestGrid);


                        //remove respawn grids
                        if (LargestGrid.IsRespawnGrid && Config.DeleteRespawnPods)
                        {
                            //Close all grids
                            grids.Close();
                            continue;
                        }

                        //Skip this grid set if its the largest grid and we have keep the largest grid ingame enabled
                        if (LargestGridID != -1 && LargestGridID.Value == LargestGrid.EntityId)
                            continue;



                        //Now see if we should hangar this shit
                        if (LargestGrid.GridSizeEnum == MyCubeSize.Large && LargestGrid.IsStatic && !hangarStatic)
                            continue;
                        else if (LargestGrid.GridSizeEnum == MyCubeSize.Large && !LargestGrid.IsStatic && !hangarLarge)
                            continue;
                        else if (LargestGrid.GridSizeEnum == MyCubeSize.Small && !hangarSmall)
                            continue;



                        GridResult Result = new GridResult();
                        Result.Grids = grids;
                        Result.BiggestGrid = LargestGrid;


                        GridStamp Stamp = Result.GenerateGridStamp();
                        PlayersHangar.SelectedPlayerFile.FormatGridName(Stamp);


                        if (PlayersHangar.SaveGridsToFile(Result, Stamp.GridName))
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

                    //Save players file!
                    PlayersHangar.SavePlayerFile();
                }

                TimeSpan ts = stopWatch.Elapsed;
                Log.Warn("Finished Hangaring: " + GridCounter + " grids! Action took: " + ts.ToString());
            }
            catch(Exception ex)
            {
                Log.Error(ex);
            }
            finally
            {
                ExportPlayerIdentities.Clear();
                stopWatch.Stop();
            }
        }


        public static void Dispose()
        {
            UpdateTimer.Elapsed -= UpdateTimer_Elapsed;
            UpdateTimer.Stop();
        }
    }
}
