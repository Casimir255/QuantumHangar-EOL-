using Newtonsoft.Json;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Groups;
using VRage.ObjectBuilders;
using VRageMath;

namespace QuantumHangar.Utilities
{
    class AutoHangar
    {
        private Settings Config;
        private CrossServer Servers;
        private GridTracker Tracker;


        public AutoHangar(Settings config, GridTracker gridTracker, GridMarket Market = null)
        {

            Config = config;
            
            Tracker = gridTracker;

            if(Market != null)
            {
                Servers = Market.MarketServers;
            }

        }


        //These will all be background workers
        public void RunAutoHangar()
        {



            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(AutoHangarWorker);
            worker.RunWorkerAsync();
        }

        public void RunAutoHangarUnderPlanet()
        {
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(UnderPlanetWorker);
            worker.RunWorkerAsync();
        }


        public void RunAutoSell()
        {
            if (Servers == null)
                throw new Exception("Grid Market is null!");

            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += new DoWorkEventHandler(AutoSellWorker);
            worker.RunWorkerAsync();
        }



        private void AutoHangarWorker(object sender, DoWorkEventArgs e)
        {

            //Significant performance increase
            if (MySession.Static.Ready)
            {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();

                List<MyCubeGrid> ExportedGrids = new List<MyCubeGrid>();
                List<MyIdentity> ExportPlayerIdentities = new List<MyIdentity>();

                Hangar.Debug("AutoHangar: Getting Players!");
                var PlayerIdentities = MySession.Static.Players.GetAllIdentities().OfType<MyIdentity>();


                foreach (MyIdentity player in PlayerIdentities)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    DateTime LastLogin;
                    LastLogin = player.LastLoginTime;

                    ulong SteamID = MySession.Static.Players.TryGetSteamId(player.IdentityId);
                    if (LastLogin.AddDays(Config.AutoHangarDayAmount) < DateTime.Now)
                    {
                        //AutoHangarBlacklist
                        if (!Config.AutoHangarPlayerBlacklist.Any(x => x.SteamID == SteamID))
                        {
                            ExportPlayerIdentities.Add(player);
                        }
                    }
                }

                Hangar.Debug("AutoHangar: Total players to check-" + ExportPlayerIdentities.Count());
                int GridCounter = 0;

                //This gets all the grids
                foreach (MyIdentity player in ExportPlayerIdentities)
                {
                    ulong id = 0;
                    try
                    {
                        id = MySession.Static.Players.TryGetSteamId(player.IdentityId);
                    }
                    catch
                    {
                        Hangar.Debug("Identitiy doesnt have a SteamID! Shipping!");
                        continue;
                    }


                    if (id == 0)
                    {
                        //Sanity check
                        continue;
                    }

                    GridMethods methods = new GridMethods(id, Config.FolderDirectory);
                    //string path = GridMethods.CreatePathForPlayer(Config.FolderDirectory, id);

                    if (!methods.LoadInfoFile(out PlayerInfo Data))
                        return;



                    ConcurrentBag<List<MyCubeGrid>> gridGroups = GridFinder.FindGridList(player.IdentityId, false);
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




                    foreach (List<MyCubeGrid> grids in gridGroups)
                    {
                        if (grids.Count == 0)
                            continue;


                        if (grids[0].IsRespawnGrid && Config.DeleteRespawnPods)
                        {
                            grids[0].Close();
                            continue;
                        }


                        Result result = new Result();
                        result.grids = grids;

                        var BiggestGrid = grids[0];
                        foreach (MyCubeGrid grid in grids)
                        {
                            if (grid.BlocksCount > BiggestGrid.BlocksCount)
                            {
                                BiggestGrid = grid;
                            }
                        }


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




                        result.biggestGrid = BiggestGrid;
                        result.GetGrids = true;

                        //Check for existing grid names
                        Utils.FormatGridName(Data, result);




                        if (methods.SaveGrids(result.grids, result.GridName))
                        {
                            //Load player file and update!
                            //Fill out grid info and store in file
                            HangarChecks.GetBPDetails(result, Config, out GridStamp Grid);

                            Grid.GridName = result.GridName;
                            Data.Grids.Add(Grid);
                            Tracker.HangarUpdate(id, true, Grid);


                            GridCounter++;
                            Hangar.Debug(result.biggestGrid.DisplayName + " was sent to Hangar due to inactivity!");
                        }
                        else
                            Hangar.Debug(result.biggestGrid.DisplayName + " FAILED to Hangar due to inactivity!");


                    }

                    //Save players file!
                    methods.SaveInfoFile(Data);
                }
                stopWatch.Stop();
                TimeSpan ts = stopWatch.Elapsed;

                Hangar.Debug("AutoHangar: Finished Hangaring -" + GridCounter + " grids! Action took: " + ts.ToString());
            }


        }



        //AutoSellsGrids (HotPile of shite)
        private void AutoSellWorker(object sender, DoWorkEventArgs e)
        {
            String[] subdirectoryEntries = Directory.GetDirectories(Config.FolderDirectory);
            foreach (string subdir in subdirectoryEntries)
            {
                string FolderName = new DirectoryInfo(subdir).Name;

                //Path.GetDirectoryName(subdir+"\\");

                if (FolderName == "PublicOffers")
                    continue;

                //Main.Debug(FolderName);
                ulong SteamID;
                try
                {
                    SteamID = Convert.ToUInt64(FolderName);
                }
                catch
                {
                    continue;
                    //Not a valid steam dir;
                }


                //Check playerlast logon
                //MyPlayer.PlayerId CurrentPlayer = MySession.Static.Players.GetAllPlayers().First(x => x.SteamId == SteamID);
                MyIdentity identity;
                DateTime LastLogin;
                try
                {

                    string playername = MySession.Static.Players.TryGetIdentityNameFromSteamId(SteamID);
                    identity = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName == playername);

                    //Main.Debug(identity.DisplayName);


                    //MyPlayer.PlayerId PlayerID = MySession.Static.Players.GetAllPlayers().First(x => x.SteamId == SteamID);
                    //CurrentPlayer = MySession.Static.Players.GetPlayerById(0);
                    LastLogin = identity.LastLoginTime;
                    if (LastLogin.AddDays(Config.SellAFKDayAmount) < DateTime.Now)
                    {
                        Hangar.Debug("Grids will be auto sold by auction!");
                    }
                    else
                    {
                        //Main.Debug(LastLogin.AddDays(MaxDayCount).ToString());
                        continue;
                    }

                }
                catch
                {
                    //Perhaps players was removed? Should we delete thy folder? Nah. WE SHALL SELL
                    continue;
                }


                PlayerInfo Data = new PlayerInfo();
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(subdir, "PlayerInfo.json")));

                    if (Data == null || Data.Grids == null || Data.Grids.Count == 0)
                    {
                        //Delete folder
                        Directory.Delete(subdir);
                        continue;
                    }


                }
                catch (Exception p)
                {

                    //Main.Debug("Unable File IO exception!", e, Main.ErrorType.Warn);
                    //File is prob null/missing
                    continue;
                }


                foreach (GridStamp grid in Data.Grids)
                {
                    //
                    if (grid.GridForSale == true)
                    {
                        //This grid could be for sale somewhere else. We need to remove it

                        try
                        {

                            int index = GridMarket.GridList.FindIndex(x => x.Name == grid.GridName);
                            CrossServerMessage message = new CrossServerMessage();
                            message.Type = CrossServer.MessageType.RemoveItem;
                            message.List.Add(GridMarket.GridList[index]);

                            //Send all servers that we removed an item
                            Servers.Update(message);

                            GridMarket.GridList.RemoveAt(index);
                            grid.GridForSale = false;
                        }
                        catch
                        {
                            //Could be a bugged grid?
                            grid.GridForSale = false;
                        }
                    }

                    string Description = "Sold by server due to inactivity at a discounted price! Originial owner: " + identity.DisplayName;


                    long Price = (long)grid.MarketValue;
                    if (grid.MarketValue == 0)
                    {
                        Price = grid.NumberofBlocks * grid.GridPCU;
                    }

                    grid.MarketValue = Price;

                    Price = Price / 2;
                    grid.GridForSale = true;

                    if (!SellOnMarket(subdir, grid, Data, identity, Price, Description))
                    {
                        Hangar.Debug("Unkown error on grid sell! Grid doesnt exist! (Dont manually delete files!)");
                        return;
                    }
                }

                FileSaver.Save(Path.Combine(subdir, "PlayerInfo.json"), Data);
                //File.WriteAllText(Path.Combine(subdir, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));


            }
        }
        private bool SellOnMarket(string IDPath, GridStamp Grid, PlayerInfo Data, MyIdentity Player, long NumPrice, string Description)
        {
            string path = Path.Combine(IDPath, Grid.GridName + ".sbc");
            if (!File.Exists(path))
            {
                //Context.Respond("Grid doesnt exist! Contact an admin!");
                return false;
            }



            Parallel.Invoke(() =>
            {

                MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_Definitions myObjectBuilder_Definitions);

                var shipBlueprint = myObjectBuilder_Definitions.ShipBlueprints;
                MyObjectBuilder_CubeGrid grid = shipBlueprint[0].CubeGrids[0];



                byte[] Definition = MyAPIGateway.Utilities.SerializeToBinary(grid);
                GridsForSale GridSell = new GridsForSale();
                GridSell.name = Grid.GridName;
                GridSell.GridDefinition = Definition;


                    //Seller faction
                    var fc = MyAPIGateway.Session.Factions.GetObjectBuilder();

                MyObjectBuilder_Faction factionBuilder;
                try
                {
                    factionBuilder = fc.Factions.First(f => f.Members.Any(m => m.PlayerId == Player.IdentityId));
                    if (factionBuilder != null || factionBuilder.Tag != "")
                    {
                        Grid.SellerFaction = factionBuilder.Tag;
                    }
                }
                catch
                {

                    try
                    {
                        Hangar.Debug("Player " + Player.DisplayName + " has a bugged faction model! Attempting to fix!");
                        factionBuilder = fc.Factions.First(f => f.Members.Any(m => m.PlayerId == Player.IdentityId));
                        MyObjectBuilder_FactionMember member = factionBuilder.Members.First(x => x.PlayerId == Player.IdentityId);

                        bool IsFounder;
                        bool IsLeader;

                        IsFounder = member.IsFounder;
                        IsLeader = member.IsLeader;

                        factionBuilder.Members.Remove(member);
                        factionBuilder.Members.Add(member);
                    }
                    catch (Exception a)
                    {
                        Hangar.Debug("Welp tbh fix failed! Please why no fix. :(", a, Hangar.ErrorType.Trace);
                    }

                        //Bugged player!
                    }



                MarketList List = new MarketList();
                List.Name = Grid.GridName;
                List.Description = Description;
                List.Seller = "Auction House";
                List.Price = NumPrice;
                List.Steamid = Convert.ToUInt64(IDPath);
                List.MarketValue = Grid.MarketValue;
                List.SellerFaction = Grid.SellerFaction;
                List.GridMass = Grid.GridMass;
                List.SmallGrids = Grid.SmallGrids;
                List.LargeGrids = Grid.LargeGrids;
                List.NumberofBlocks = Grid.NumberofBlocks;
                List.MaxPowerOutput = Grid.MaxPowerOutput;
                List.GridBuiltPercent = Grid.GridBuiltPercent;
                List.JumpDistance = Grid.JumpDistance;
                List.NumberOfGrids = Grid.NumberOfGrids;
                List.BlockTypeCount = Grid.BlockTypeCount;
                List.PCU = Grid.GridPCU;


                    //List item as server offer
                    List.ServerOffer = true;




                    //We need to send to all to add one item to the list!
                    CrossServerMessage SendMessage = new CrossServerMessage();
                SendMessage.Type = CrossServer.MessageType.AddItem;
                SendMessage.GridDefinition.Add(GridSell);
                SendMessage.List.Add(List);


                Servers.Update(SendMessage);

            });

            return true;
        }



        private void HangarReset(string HangarDir, bool FixMarket)
        {
            String[] subdirectoryEntries = Directory.GetDirectories(HangarDir);
            foreach (string subdir in subdirectoryEntries)
            {
                string FolderName = new DirectoryInfo(subdir).Name;


                //Main.Debug(FolderName);
                ulong SteamID;
                try
                {
                    SteamID = Convert.ToUInt64(FolderName);
                }
                catch
                {
                    continue;
                    //Not a valid steam dir;
                }


                PlayerInfo Data = new PlayerInfo();
                try
                {
                    Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(subdir, "PlayerInfo.json")));

                    if (Data == null || Data.Grids == null || Data.Grids.Count == 0)
                    {
                        continue;
                    }




                    var ext = new List<string> { "sbc" };
                    var myFiles = Directory.GetFiles(subdir, "*.sbc", SearchOption.AllDirectories);

                    List<GridStamp> NewGrids = new List<GridStamp>();
                    NewGrids = Data.Grids;
                    for (int i = 0; i < Data.Grids.Count(); i++)
                    {

                        if (!myFiles.Any(x => Path.GetFileNameWithoutExtension(x) == Data.Grids[i].GridName))
                        {

                            Hangar.Debug("Removing grid: " + NewGrids[i].GridName + "! It doesnt exist in the folder!!");
                            NewGrids.RemoveAt(i);

                        }
                    }






                    Data.Grids = NewGrids;
                    FileSaver.Save(Path.Combine(subdir, "PlayerInfo.json"), Data);


                }
                catch (Exception e)
                {

                    //Main.Debug("Unable File IO exception!", e, Main.ErrorType.Warn);
                    //File is prob null/missing
                    continue;
                }
            }



            if (FixMarket)
            {
                for (int i = 0; i < GridMarket.GridList.Count(); i++)
                {
                    //Removes any item with steamID of 0
                    if (GridMarket.GridList[i].Steamid == 0)
                    {
                        Hangar.Debug("Removing " + GridMarket.GridList[i].Name + " from the market. Has SteamID of 0");
                        GridMarket.GridList.RemoveAt(i);
                    }
                }
            }


        }
        private void UnderPlanetWorker(object sender, DoWorkEventArgs e)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            int GridCounter = 0;
            int TotalGridCounter = 0;

            Dictionary<long, List<Result>> ToSaveGrids = new Dictionary<long, List<Result>>();

            Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group =>
            {

                List<MyCubeGrid> gridList = new List<MyCubeGrid>();
                var BiggestGrid = group.Nodes.First().NodeData;
                Result result = new Result();
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                {

                    MyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    if (grid.BlocksCount > BiggestGrid.BlocksCount)
                    {
                        BiggestGrid = grid;
                    }
                    TotalGridCounter += 1;
                    gridList.Add(grid);
                }


                if (gridList.Count == 0)
                {
                    return;
                }

                result.grids = gridList;
                result.biggestGrid = BiggestGrid;





                Vector3D Position = BiggestGrid.PositionComp.GetPosition();
                if (!Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(Position)))
                {
                    MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(Position);

                    //Main.Debug("Planet Min Radius: " + planet.MinimumRadius);

                    double distance = Vector3D.Distance(Position, planet.PositionComp.GetPosition());

                    //Main.Debug("Your distance from center: " + distance);

                    if (distance < planet.MinimumRadius * .7)
                    {
                        //Will save grid!
                        Hangar.Debug("Will save grid");

                        if (ToSaveGrids.ContainsKey(BiggestGrid.BigOwners[0]))
                        {
                            //Dictionary already contains grid!
                            ToSaveGrids[BiggestGrid.BigOwners[0]].Add(result);
                        }
                        else
                        {
                            List<Result> AllGrids = new List<Result>();
                            AllGrids.Add(result);
                            ToSaveGrids.Add(BiggestGrid.BigOwners[0], AllGrids);
                        }


                        return;
                    }
                    else
                    {
                        return;
                    }

                }
                else
                {
                    return;
                }


            });


            //Attempt save!
            foreach (var item in ToSaveGrids)
            {
                ulong id = 0;
                try
                {
                    id = MySession.Static.Players.TryGetSteamId(item.Key);
                }
                catch
                {
                    Hangar.Debug("Identitiy doesnt have a SteamID! Shipping!");
                    continue;
                }


                if (id == 0)
                {
                    //Sanity check
                    continue;
                }

                GridMethods methods = new GridMethods(id, Config.FolderDirectory);
                //string path = GridMethods.CreatePathForPlayer(Config.FolderDirectory, id);


                if (!methods.LoadInfoFile(out PlayerInfo Data))
                    continue;


                foreach (Result R in item.Value)
                {
                    //Fix invalid characters
                    Utils.FormatGridName(Data, R);

                    if (methods.SaveGrids(R.grids, R.GridName))
                    {
                        //Load player file and update!
                        //Fill out grid info and store in file
                        HangarChecks.GetBPDetails(R, Config, out GridStamp Grid);

                        Grid.GridName = R.GridName;
                        Data.Grids.Add(Grid);
                        Tracker.HangarUpdate(id, true, Grid);



                        GridCounter += 1;
                        Hangar.Debug(R.biggestGrid.DisplayName + " was sent to Hangar due to inside planet!");
                    }
                    else
                        Hangar.Debug(R.biggestGrid.DisplayName + " FAILED to Hangar due to inside planet!");
                }

                //Save file
                methods.SaveInfoFile(Data);

            }


            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;

            Hangar.Debug("PlanetHangar: Found [" + GridCounter + "] grids out of [" + TotalGridCounter + "] total grids under planet! Action took: " + ts.ToString());
        }

        public void SaveAll()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            int GridCounter = 0;
            int TotalGridCounter = 0;

            Dictionary<long, List<Result>> ToSaveGrids = new Dictionary<long, List<Result>>();

            Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group =>
            {

                List<MyCubeGrid> gridList = new List<MyCubeGrid>();
                var BiggestGrid = group.Nodes.First().NodeData;
                Result result = new Result();
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                {

                    MyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    if (grid.BlocksCount > BiggestGrid.BlocksCount)
                    {
                        BiggestGrid = grid;
                    }
                    TotalGridCounter += 1;
                    gridList.Add(grid);
                }


                if (gridList.Count == 0)
                {
                    return;
                }

                result.grids = gridList;
                result.biggestGrid = BiggestGrid;



                if (ToSaveGrids.ContainsKey(BiggestGrid.BigOwners[0]))
                {
                    //Dictionary already contains grid!
                    ToSaveGrids[BiggestGrid.BigOwners[0]].Add(result);
                }
                else
                {
                    List<Result> AllGrids = new List<Result>();
                    AllGrids.Add(result);
                    ToSaveGrids.Add(BiggestGrid.BigOwners[0], AllGrids);
                }
            });


            //Attempt save!
            foreach (var item in ToSaveGrids)
            {
                ulong id = 0;
                try
                {
                    id = MySession.Static.Players.TryGetSteamId(item.Key);
                }
                catch
                {
                    Hangar.Debug("Identitiy doesnt have a SteamID! Shipping!");
                    continue;
                }


                if (id == 0)
                {
                    //Sanity check
                    continue;
                }

                GridMethods methods = new GridMethods(id, Config.FolderDirectory);
                //string path = GridMethods.CreatePathForPlayer(Config.FolderDirectory, id);


                if (!methods.LoadInfoFile(out PlayerInfo Data))
                    continue;


                foreach (Result R in item.Value)
                {
                    //Fix invalid characters
                    Utils.FormatGridName(Data, R);


                    if (methods.SaveGrids(R.grids, R.GridName))
                    {
                        //Load player file and update!
                        //Fill out grid info and store in file
                        HangarChecks.GetBPDetails(R, Config, out GridStamp Grid);

                        Grid.GridName = R.GridName;
                        Data.Grids.Add(Grid);
                        Tracker.HangarUpdate(id, true, Grid);



                        GridCounter += 1;
                        Hangar.Debug(R.biggestGrid.DisplayName + " was sent to Hangar due to inside planet!");
                    }
                    else
                        Hangar.Debug(R.biggestGrid.DisplayName + " FAILED to Hangar due to inside planet!");
                }

                //Save file
                methods.SaveInfoFile(Data);
            }


            stopwatch.Stop();
            TimeSpan ts = stopwatch.Elapsed;

            Hangar.Debug("SaveAll: Found [" + GridCounter + "] grids out of [" + TotalGridCounter + "] total grids under planet! Action took: " + ts.ToString());

        }

    }
}
