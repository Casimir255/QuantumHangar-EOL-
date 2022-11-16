using Newtonsoft.Json;
using NLog;
using QuantumHangar.HangarMarket;
using QuantumHangar.Serialization;
using QuantumHangar.Utils;
using Sandbox.Definitions;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;

namespace QuantumHangar.HangarChecks
{
    //This is for all users. Doesn't matter who is invoking it. (Admin or different). Should contain main functions for the player hangar. (Either removing grids and saving, checking stamps etc)
    public class PlayerHangar : IDisposable
    {
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(PlayerHangar));
        private readonly Chat Chat;
        public readonly PlayerInfo SelectedPlayerFile;

        private bool IsAdminCalling;
        private readonly ulong SteamID;
        private readonly MyIdentity Identity;


        public string PlayersFolderPath { get; }


        private static Settings Config => Hangar.Config;


        public PlayerHangar(ulong SteamID, Chat Respond, bool IsAdminCalling = false)
        {
            try
            {

                MySession.Static.Players.TryGetIdentityFromSteamID(SteamID, out Identity);

                this.SteamID = SteamID;

                this.IsAdminCalling = IsAdminCalling;
                Chat = Respond;
                SelectedPlayerFile = new PlayerInfo();


                PlayersFolderPath = Path.Combine(Hangar.MainPlayerDirectory, SteamID.ToString());
                SelectedPlayerFile.LoadFile(Hangar.MainPlayerDirectory, SteamID);


            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public static bool TransferGrid(ulong From, ulong To, string GridName)
        {

            try
            {
                Log.Error("Starting Grid Transfer!");

                var FromInfo = new PlayerInfo();
                FromInfo.LoadFile(Hangar.MainPlayerDirectory, From);

                if (!FromInfo.GetGrid(GridName, out var Stamp, out var error))
                {

                    Log.Error("Failed to get grid! " + error);
                    return false;
                }


                var GridPath = Stamp.GetGridPath(FromInfo.PlayerFolderPath);
                var FileName = Path.GetFileName(GridPath);

                FromInfo.Grids.Remove(Stamp);
                FromInfo.SaveFile();

                var ToInfo = new PlayerInfo();
                ToInfo.LoadFile(Hangar.MainPlayerDirectory, To);
                ToInfo.FormatGridName(Stamp);


                //Call gridstamp transferred as it will force load near player, and transfer on load
                Stamp.Transfered();

                ToInfo.Grids.Add(Stamp);

                //Make sure to create directory
                Directory.CreateDirectory(ToInfo.PlayerFolderPath);
                File.Move(GridPath, Path.Combine(ToInfo.PlayerFolderPath, Stamp.GridName + ".sbc"));

                ToInfo.SaveFile();
                Log.Error("Moved Grid!");
            }
            catch (Exception Ex)
            {
                Log.Error(Ex);
                return false;
            }
            return true;
        }

        public static bool TransferGrid(PlayerInfo To, GridStamp Stamp)
        {
            try
            {
                var GridName = Stamp.GridName;
                To.FormatGridName(Stamp);

                //Make sure to create the directory!
                Directory.CreateDirectory(To.PlayerFolderPath);

                File.Copy(Stamp.OriginalGridPath, Path.Combine(To.PlayerFolderPath, Stamp.GridName + ".sbc"));
                Stamp.Transfered();

                To.ServerOfferPurchased(GridName);

                To.Grids.Add(Stamp);
                To.SaveFile();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
            return true;
        }

        /* Private Methods */
        private bool RemoveStamp(int ID)
        {
            //Log.Warn($"HitA: {ID}");

            //Input valid for ID - X
            if (!SelectedPlayerFile.IsInputValid(ID, out string Error))
            {
                Chat.Respond(Error);
                return false;
            }

            //Log.Warn("HitB");
            if (!IsAdminCalling)
            {
                var stamp = new TimeStamp
                {
                    OldTime = DateTime.Now
                };
                SelectedPlayerFile.Timer = stamp;
            }

            //Log.Warn("HitC");
            try
            {

                var a = Path.Combine(PlayersFolderPath, SelectedPlayerFile.Grids[ID - 1].GridName + ".sbc");
                Log.Info(a);

                File.Delete(a);
                SelectedPlayerFile.Grids.RemoveAt(ID - 1);
                SelectedPlayerFile.SaveFile();



                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
        private bool CheckGridLimits(GridStamp Grid)
        {
            //Backwards compatibale
            if (Config.OnLoadTransfer)
                return true;
            
            if (Grid.ShipPCU.Count == 0)
            {

                var blockLimits = Identity.BlockLimits;

                var a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                {
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Log.Debug("PCU Server limits is 0!");
                    return true;
                }

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                var CurrentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                var MaxPcu = blockLimits.PCU + CurrentPcu;

                var pcu = MaxPcu - CurrentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                //Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                //Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (MaxPcu - CurrentPcu > Grid.GridPCU) return true;
                var Need = Grid.GridPCU - (MaxPcu - CurrentPcu);
                Chat.Respond("PCU limit reached! You need an additional " + Need + " pcu to perform this action!");
                return false;

            }


            foreach (var Player in Grid.ShipPCU)
            {

                var Identity = MySession.Static.Players.TryGetIdentity(Player.Key);
                if (Identity == null)
                {
                    continue;
                }
                
                var blockLimits = Identity.BlockLimits;
                var a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                {
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Hangar.Debug("PCU Server limits is 0!");
                    continue;
                }

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                var CurrentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                var MaxPcu = blockLimits.PCU + CurrentPcu;

                var pcu = MaxPcu - CurrentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                //Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                //Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (MaxPcu - CurrentPcu > Player.Value) continue;
                var Need = Player.Value - (MaxPcu - CurrentPcu);
                Chat.Respond("PCU limit reached! " + Identity.DisplayName + " needs an additional " + Need + " PCU to load this grid!");
                return false;

            }
            return true;
        }
        private bool BlockLimitChecker(IEnumerable<MyObjectBuilder_CubeGrid> shipBlueprints)
        {
            var BiggestGrid = 0;
            var blocksToBuild = 0;
            //failedBlockType = null;
            //Need dictionary for each player AND their blocks they own. (Players could own stuff on the same grid)
            var BlocksAndOwnerForLimits = new Dictionary<long, Dictionary<string, int>>();


            //Total PCU and Blocks
            var FinalBlocksCount = 0;
            var FinalBlocksPCU = 0;


            //Go ahead and check if the block limits is enabled server side! If it isn't... return true!
            if (!Config.EnableBlackListBlocks)
            {
                return true;
            }

            //If we are using built in server block limits..
            if (Config.SBlockLimits)
            {
                //& the server blocklimits is not enabled... Return true
                if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.NONE)
                {
                    return true;
                }


                //Cycle each grid in the ship blueprints
                foreach (var CubeGrid in shipBlueprints)
                {

                    //Main.Debug("CubeBlocks count: " + CubeGrid.GetType());
                    if (BiggestGrid < CubeGrid.CubeBlocks.Count())
                    {
                        BiggestGrid = CubeGrid.CubeBlocks.Count();
                    }
                    blocksToBuild += CubeGrid.CubeBlocks.Count();

                    foreach (var block in CubeGrid.CubeBlocks)
                    {

                        var defId = new MyDefinitionId(block.TypeId, block.SubtypeId);

                        if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(defId,
                                out var myCubeBlockDefinition)) continue;
                        //Check for BlockPair or SubType?
                        var BlockName = "";
                        //Server Block Limits
                        BlockName = Config.SBlockLimits ? myCubeBlockDefinition.BlockPairName : /*Custom Block SubType Limits */ myCubeBlockDefinition.Id.SubtypeName;

                        var blockOwner2 = 0L;
                        blockOwner2 = block.BuiltBy;

                        //If the player dictionary already has a Key, we need to retrieve it
                        if (BlocksAndOwnerForLimits.ContainsKey(blockOwner2))
                        {
                            //if the dictionary already contains the same block type
                            var dictForUser = BlocksAndOwnerForLimits[blockOwner2];
                            if (dictForUser.ContainsKey(BlockName))
                            {
                                dictForUser[BlockName]++;
                            }
                            else
                            {
                                dictForUser.Add(BlockName, 1);
                            }
                            BlocksAndOwnerForLimits[blockOwner2] = dictForUser;
                        }
                        else
                        {
                            BlocksAndOwnerForLimits.Add(blockOwner2, new Dictionary<string, int>
                            {
                                {
                                    BlockName,
                                    1
                                }
                            });
                        }

                        FinalBlocksPCU += myCubeBlockDefinition.PCU;
                        //if()
                    }
                    FinalBlocksCount += CubeGrid.CubeBlocks.Count;
                }

                if (MySession.Static.MaxGridSize == 0 || BiggestGrid <= MySession.Static.MaxGridSize)
                    return PlayerIdentityLoop(BlocksAndOwnerForLimits, FinalBlocksCount);
                Chat.Respond("Biggest grid is over Max grid size! ");
                return false;

                //Need too loop player identities in dictionary. Do this via seperate function
            }
            //BlockLimiter
            if (!PluginDependencies.BlockLimiterInstalled)
            {
                //BlockLimiter is null!
                Chat.Respond("Blocklimiter Plugin not installed or Loaded!");
                Log.Warn("BLimiter plugin not installed or loaded! May require a server restart!");
                return false;
            }
            var grids = shipBlueprints.ToList();
            var ValueReturn = PluginDependencies.CheckGridLimits(grids, Identity.IdentityId);

            //Convert to value return type
            if (!ValueReturn)
            {
                //Main.Debug("Cannont load grid in due to BlockLimiter Configs!");
                return true;
            }
            Chat.Respond("Grid would be over Server-Blocklimiter limits!");
            return false;
        }

        private bool PlayerIdentityLoop(Dictionary<long, Dictionary<string, int>> BlocksAndOwnerForLimits, int blocksToBuild)
        {
            foreach (var (k, PlayerBuiltBlocks) in BlocksAndOwnerForLimits)
            {
                var myIdentity = MySession.Static.Players.TryGetIdentity(k);
                if (myIdentity == null) continue;
                var blockLimits = myIdentity.BlockLimits;
                if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.PER_FACTION && MySession.Static.Factions.GetPlayerFaction(myIdentity.IdentityId) == null)
                {
                    Chat.Respond("ServerLimits are set PerFaction. You are not in a faction! Contact an Admin!");
                    return false;
                }

                if (blockLimits == null) continue;
                if (MySession.Static.MaxBlocksPerPlayer != 0 && blockLimits.BlocksBuilt + blocksToBuild > blockLimits.MaxBlocks)
                {
                    Chat.Respond("Cannot load grid! You would be over your Max Blocks!");
                    return false;
                }

                //Double check to see if the list is null
                if (PlayerBuiltBlocks == null) continue;
                foreach (var ServerBlockLimits in MySession.Static.BlockTypeLimits)
                {
                    if (!PlayerBuiltBlocks.ContainsKey(ServerBlockLimits.Key)) continue;
                    var TotalNumberOfBlocks = PlayerBuiltBlocks[ServerBlockLimits.Key];

                    if (blockLimits.BlockTypeBuilt.TryGetValue(ServerBlockLimits.Key, out MyBlockLimits.MyTypeLimitData LimitData))
                    {
                        //Grab their existing block count for the block limit
                        TotalNumberOfBlocks += LimitData.BlocksBuilt;
                    }

                    //Compare to see if they would be over!
                    var ServerLimit = MySession.Static.GetBlockTypeLimit(ServerBlockLimits.Key);
                    if (TotalNumberOfBlocks <= ServerLimit) continue;
                    Chat.Respond("Player " + myIdentity.DisplayName + " would be over their " + ServerBlockLimits.Key + " limits! " + TotalNumberOfBlocks + "/" + ServerLimit);
                    //Player would be over their block type limits
                    return false;
                }
            }

            return true;
        }

        public bool IsGridForSale(GridStamp Grid, bool Admin)
        {
            return Admin && Grid.GridForSale;
        }

        public static bool IsServerSaving(Chat Chat)
        {
            if (!MySession.Static.IsSaveInProgress) return false;
            Chat?.Respond("Server has a save in progress... Please wait!");
            return true;
        }
        public bool CheckPlayerTimeStamp()
        {
            //Check timestamp before continuing!
            if (SelectedPlayerFile.Timer == null) return true;
            var Old = SelectedPlayerFile.Timer;
            var LastUse = Old.OldTime;


            //When the player can use the command
            var CanUseTime = LastUse + TimeSpan.FromMinutes(Config.WaitTime);
            //Log.Info("TimeSpan: " + Subtracted.TotalMinutes);
            if (CanUseTime > DateTime.Now)
            {

                var TimeLeft = DateTime.Now - CanUseTime;
                //int RemainingTime = (int)Plugin.Config.WaitTime - Convert.ToInt32(Subtracted.TotalMinutes);
                Chat?.Respond($"You have {TimeLeft:hh\\:mm\\:ss} until you can perform this action!");
                return false;
            }
            SelectedPlayerFile.Timer = null;
            return true;
        }
        public bool ExtensiveLimitChecker(GridStamp Stamp)
        {
            //Begin Single Slot Save!
            if (Config.SingleMaxBlocks != 0)
            {
                if (Stamp.NumberofBlocks > Config.SingleMaxBlocks)
                {
                    var remainder = Stamp.NumberofBlocks - Config.SingleMaxBlocks;
                    Chat?.Respond("Grid is " + remainder + " blocks over the max slot block limit! " + Stamp.NumberofBlocks + "/" + Config.SingleMaxBlocks);
                    return false;
                }
            }

            if (Config.SingleMaxPcu != 0)
            {
                if (Stamp.GridPCU > Config.SingleMaxPcu)
                {
                    var remainder = Stamp.GridPCU - Config.SingleMaxPcu;
                    Chat?.Respond("Grid is " + remainder + " PCU over the slot hangar PCU limit! " + Stamp.GridPCU + "/" + Config.SingleMaxPcu);
                    return false;
                }
            }

            if (Config.AllowStaticGrids)
            {
                if (Config.SingleMaxLargeGrids != 0 && Stamp.StaticGrids > Config.SingleMaxStaticGrids)
                {
                    var remainder = Stamp.StaticGrids - Config.SingleMaxStaticGrids;
                    Chat?.Respond("You are " + remainder + " static grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (Stamp.StaticGrids > 0)
                {
                    Chat?.Respond("Saving Static Grids is disabled!");
                    return false;
                }
            }

            if (Config.AllowLargeGrids)
            {
                if (Config.SingleMaxLargeGrids != 0 && Stamp.LargeGrids > Config.SingleMaxLargeGrids)
                {
                    var remainder = Stamp.LargeGrids - Config.SingleMaxLargeGrids;
                    Chat?.Respond("You are " + remainder + " large grids over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (Stamp.LargeGrids > 0)
                {
                    Chat?.Respond("Saving Large Grids is disabled!");
                    return false;
                }
            }

            if (Config.AllowSmallGrids)
            {
                if (Config.SingleMaxSmallGrids != 0 && Stamp.SmallGrids > Config.SingleMaxSmallGrids)
                {
                    var remainder = Stamp.SmallGrids - Config.SingleMaxLargeGrids;
                    Chat?.Respond("You are " + remainder + " small grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (Stamp.SmallGrids > 0)
                {
                    Chat?.Respond("Saving Small Grids is disabled!");
                    return false;
                }
            }


            var TotalBlocks = 0;
            var TotalPCU = 0;
            var StaticGrids = 0;
            var LargeGrids = 0;
            var SmallGrids = 0;

            //Hangar total limit!
            foreach (var Grid in SelectedPlayerFile.Grids)
            {
                TotalBlocks += Grid.NumberofBlocks;
                TotalPCU += Grid.GridPCU;

                StaticGrids += Grid.StaticGrids;
                LargeGrids += Grid.LargeGrids;
                SmallGrids += Grid.SmallGrids;
            }

            if (Config.PlayerMaxBlocks != 0 && TotalBlocks > Config.PlayerMaxBlocks)
            {
                var remainder = TotalBlocks - Config.PlayerMaxBlocks;
                Chat?.Respond("Grid is " + remainder + " blocks over your hangar block limit! " + TotalBlocks + "/" + Config.PlayerMaxBlocks);
                return false;
            }

            if (Config.PlayerMaxPcu != 0 && TotalPCU > Config.PlayerMaxPcu)
            {
                var remainder = TotalPCU - Config.PlayerMaxPcu;
                Chat?.Respond("Grid is " + remainder + " PCU over your hangar PCU limit! " + TotalPCU + "/" + Config.PlayerMaxPcu);
                return false;
            }

            if (Config.PlayerMaxStaticGrids != 0 && StaticGrids > Config.PlayerMaxStaticGrids)
            {
                var remainder = StaticGrids - Config.PlayerMaxStaticGrids;
                Chat?.Respond("You are " + remainder + " static grid over your hangar limit!");
                return false;
            }

            if (Config.TotalMaxLargeGrids != 0 && LargeGrids > Config.TotalMaxLargeGrids)
            {
                var remainder = LargeGrids - Config.TotalMaxLargeGrids;
                Chat?.Respond("You are " + remainder + " large grid over your hangar limit!");
                return false;
            }

            if (Config.PlayerMaxSmallGrids == 0 || SmallGrids <= Config.PlayerMaxSmallGrids) return true;
            {
                var remainder = LargeGrids - Config.PlayerMaxSmallGrids;
                Chat?.Respond("You are " + remainder + " small grid over your hangar limit!");
                return false;
            }

        }
        public bool CheckHangarLimits()
        {
            if (SelectedPlayerFile.Grids.Count < SelectedPlayerFile.MaxHangarSlots) return true;
            Chat?.Respond("You have reached your hangar limit!");
            return false;
        }

        public bool SellSelectedGrid(GridStamp Stamp, long Price, string Description)
        {
            Stamp.GridForSale = true;

            var NewListing = new MarketListing(Stamp);
            NewListing.SetUserInputs(Description, Price);
            NewListing.Seller = Identity.DisplayName;
            NewListing.SetPlayerData(SteamID, Identity.IdentityId);
            HangarMarketController.SaveNewMarketFile(NewListing);
            SavePlayerFile();

            HangarMarketController.NewGridOfferListed(NewListing);
            return true;
        }

        public void SaveGridStamp(GridStamp Stamp, bool Admin = false, bool IgnoreSave = false)
        {
            if (!Admin)
            {
                var stamp = new TimeStamp
                {
                    OldTime = DateTime.Now
                };
                SelectedPlayerFile.Timer = stamp;
            }

            SelectedPlayerFile.Grids.Add(Stamp);

            if (!IgnoreSave)
                SelectedPlayerFile.SaveFile();
        }

        public void SavePlayerFile()
        {
            SelectedPlayerFile.SaveFile();
        }

        public bool RemoveGridStamp(int ID)
        {
            return RemoveStamp(ID);
        }

        public async Task<bool> SaveGridsToFile(GridResult Grids, string FileName)
        {
            return await GridSerializer.SaveGridsAndClose(Grids.Grids, PlayersFolderPath, FileName, Identity.IdentityId);
        }

        public void ListAllGrids()
        {

            if (SelectedPlayerFile.Grids.Count == 0)
            {
                Chat.Respond(IsAdminCalling
                    ? "There are no grids in this players hangar!"
                    : "You have no grids in your hangar!");

                return;
            }

            var sb = new StringBuilder();

            if (IsAdminCalling)
                sb.AppendLine("Players has " + SelectedPlayerFile.Grids.Count() + "/" + SelectedPlayerFile.MaxHangarSlots + " stored grids:");
            else
                sb.AppendLine("You have " + SelectedPlayerFile.Grids.Count() + "/" + SelectedPlayerFile.MaxHangarSlots + " stored grids:");

            var count = 1;
            foreach (var grid in SelectedPlayerFile.Grids)
            {
                if (grid.GridForSale)
                {
                    sb.AppendLine(" [$" + count + "$] - " + grid.GridName);
                }
                else
                {
                    sb.AppendLine(" [" + count + "] - " + grid.GridName);
                }

                count++;
            }
            Chat.Respond(sb.ToString());
        }

        public void DetailedReport(int ID)
        {
            var Response = new StringBuilder();
            string Prefix;
            if (ID == 0)
            {
                Prefix = $"HangarSlots: { SelectedPlayerFile.Grids.Count()}/{ SelectedPlayerFile.MaxHangarSlots}";
                Response.AppendLine("- - Global Limits - -");
                Response.AppendLine($"TotalBlocks: {SelectedPlayerFile.TotalBlocks}/{ Config.PlayerMaxBlocks}");
                Response.AppendLine($"TotalPCU: {SelectedPlayerFile.TotalPCU}/{ Config.PlayerMaxPcu}");
                Response.AppendLine($"StaticGrids: {SelectedPlayerFile.StaticGrids}/{ Config.PlayerMaxStaticGrids}");
                Response.AppendLine($"LargeGrids: {SelectedPlayerFile.LargeGrids}/{ Config.TotalMaxLargeGrids}");
                Response.AppendLine($"SmallGrids: {SelectedPlayerFile.SmallGrids}/{ Config.PlayerMaxSmallGrids}");
                Response.AppendLine();
                Response.AppendLine("- - Individual Hangar Slots - -");
                for (var i = 0; i < SelectedPlayerFile.Grids.Count; i++)
                {
                    var Stamp = SelectedPlayerFile.Grids[i];
                    Response.AppendLine($" * * Slot {i + 1} : {Stamp.GridName} * *");
                    Response.AppendLine($"PCU: {Stamp.GridPCU}/{Config.SingleMaxPcu}");
                    Response.AppendLine($"Blocks: {Stamp.NumberofBlocks}/{Config.SingleMaxBlocks}");
                    Response.AppendLine($"StaticGrids: {Stamp.StaticGrids}/{Config.SingleMaxStaticGrids}");
                    Response.AppendLine($"LargeGrids: {Stamp.LargeGrids}/{Config.SingleMaxLargeGrids}");
                    Response.AppendLine($"SmallGrids: {Stamp.SmallGrids}/{Config.SingleMaxSmallGrids}");
                    Response.AppendLine($"TotalGridCount: {Stamp.NumberOfGrids}");
                    Response.AppendLine($"Mass: {Stamp.GridMass}kg");
                    Response.AppendLine($"Built%: {Stamp.GridBuiltPercent * 100}%");
                    Response.AppendLine($" * * * * * * * * * * * * * * * * ");
                    Response.AppendLine();
                }
            }
            else
            {

                if (!SelectedPlayerFile.GetGrid(ID, out var Stamp, out var Error))
                {
                    Chat.Respond(Error);
                    return;
                }

                Prefix = $"Slot {ID} : {Stamp.GridName}";
                Response.AppendLine($"PCU: {Stamp.GridPCU}/{Config.SingleMaxPcu}");
                Response.AppendLine($"Blocks: {Stamp.NumberofBlocks}/{Config.SingleMaxBlocks}");
                Response.AppendLine($"StaticGrids: {Stamp.StaticGrids}/{Config.SingleMaxStaticGrids}");
                Response.AppendLine($"LargeGrids: {Stamp.LargeGrids}/{Config.SingleMaxLargeGrids}");
                Response.AppendLine($"SmallGrids: {Stamp.SmallGrids}/{Config.SingleMaxSmallGrids}");
                Response.AppendLine($"TotalGridCount: {Stamp.NumberOfGrids}");
                Response.AppendLine($"Mass: {Stamp.GridMass}kg");
                Response.AppendLine($"Built%: {Stamp.GridBuiltPercent * 100}%");
                Response.AppendLine();
            }

            ModCommunication.SendMessageTo(new DialogMessage("Hangar Info", Prefix, Response.ToString()), SteamID);
        }

        public bool TryGetGridStamp(int ID, out GridStamp Stamp)
        {
            if (SelectedPlayerFile.GetGrid(ID, out Stamp, out var Error)) return true;
            Chat.Respond(Error);
            return false;

        }
        public bool LoadGrid(GridStamp Stamp, out IEnumerable<MyObjectBuilder_CubeGrid> Grids)
        {
            if (!Stamp.TryGetGrids(PlayersFolderPath, out Grids))
                return false;


            PluginDependencies.BackupGrid(Grids.ToList(), Identity.IdentityId);
            GridSerializer.TransferGridOwnership(Grids, Identity.IdentityId, Stamp.TransferOwnerShipOnLoad);

            return true;
        }

        public void UpdateHangar()
        {
            if (!Directory.Exists(PlayersFolderPath))
            {
                Chat?.Respond("This players hangar doesn't exist! Skipping sync!");
                return;
            }

            var myFiles = Directory.EnumerateFiles(PlayersFolderPath, "*.*", SearchOption.TopDirectoryOnly).Where(s => Path.GetExtension(s).TrimStart('.').ToLowerInvariant() == "sbc");

            if (!myFiles.Any())
                return;
            //Scan for new grids


            var NewGrids = new List<GridStamp>();
            var AddedGrids = 0;
            foreach (var file in myFiles)
            {

                var name = Path.GetFileNameWithoutExtension(file);
                Log.Info(name);
                if (SelectedPlayerFile.AnyGridsMatch(Path.GetFileNameWithoutExtension(name)))
                    continue;

                AddedGrids++;
                var Stamp = new GridStamp(file);
                NewGrids.Add(Stamp);
            }

            var RemovedGrids = 0;
            for (var i = SelectedPlayerFile.Grids.Count - 1; i >= 0; i--)
            {
                if (myFiles.Any(x => Path.GetFileNameWithoutExtension(x) == SelectedPlayerFile.Grids[i].GridName))
                    continue;
                RemovedGrids++;
                SelectedPlayerFile.Grids.RemoveAt(i);
            }

            SelectedPlayerFile.Grids.AddRange(NewGrids);
            SelectedPlayerFile.SaveFile();

            Chat?.Respond($"Removed {RemovedGrids} grids and added {AddedGrids} new grids to hangar for player {SteamID}");


        }

        public void Dispose()
        {

        }

        public bool CheckLimits(GridStamp Grid, IEnumerable<MyObjectBuilder_CubeGrid> Blueprint)
        {
            return CheckGridLimits(Grid) && BlockLimitChecker(Blueprint);
        }


        public bool ParseInput(string selection, out int ID)
        {

            if (int.TryParse(selection, out ID))
                return true;
            for (var i = 0; i < SelectedPlayerFile.Grids.Count; i++)
            {
                var s = SelectedPlayerFile.Grids[i];
                if (s.GridName != selection) continue;
                ID = i+1;
                return true;
            }
            return false;

        }
    }





    [JsonObject(MemberSerialization.OptIn)]
    public class PlayerInfo
    {
        //This is the players info file. Should contain methods for finding grids/checking timers 

        [JsonProperty] public List<GridStamp> Grids = new List<GridStamp>();
        [JsonProperty] public TimeStamp Timer;
        [JsonProperty] private int? _maxHangarSlots;
        [JsonProperty] private Dictionary<string, int> _serverOfferPurchases = new Dictionary<string, int>();


        public int MaxHangarSlots { get; set; }
        public string FilePath { get; set; }
        public ulong SteamID { get; set; }

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public int TotalBlocks { get; private set; }
        public int TotalPCU { get; private set; }
        public int StaticGrids { get; private set; }
        public int LargeGrids { get; private set; }
        public int SmallGrids { get; private set; }

        private Settings Config => Hangar.Config;

        public string PlayerFolderPath;


        public bool LoadFile(string FolderPath, ulong SteamID)
        {

            this.SteamID = SteamID;
            PlayerFolderPath = Path.Combine(FolderPath, SteamID.ToString());
            FilePath = Path.Combine(PlayerFolderPath, "PlayerInfo.json");

            GetMaxHangarSlot();

            if (!File.Exists(FilePath))
                return true;


            try
            {
                var ScannedFile = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(FilePath));
                Grids = ScannedFile.Grids;
                Timer = ScannedFile.Timer;
                _serverOfferPurchases = ScannedFile._serverOfferPurchases;

                if (ScannedFile._maxHangarSlots.HasValue)
                    MaxHangarSlots = ScannedFile._maxHangarSlots.Value;
                PerformDataScan();

            }
            catch (Exception e)
            {
                Log.Warn(e, "For some reason the file is broken");
                return false;
            }


            return true;
        }

        private void PerformDataScan()
        {
            //Accumulate Grid Data
            foreach (var Grid in Grids)
            {
                TotalBlocks += Grid.NumberofBlocks;
                TotalPCU += Grid.GridPCU;

                StaticGrids += Grid.StaticGrids;
                LargeGrids += Grid.LargeGrids;
                SmallGrids += Grid.SmallGrids;
            }
        }


        public bool CheckTimeStamp(out string Response)
        {
            Response = null;
            if (Timer == null)
                return true;

            var Subtracted = DateTime.Now.Subtract(Timer.OldTime);
            var WaitTimeSpawn = new TimeSpan(0, (int)Config.WaitTime, 0);
            var Remainder = WaitTimeSpawn - Subtracted;
            if (!(Subtracted.TotalMinutes <= Config.WaitTime)) return true;
            Response = string.Format("{0:mm}min & {0:ss}s", Remainder);
            return false;
        }

        public bool ReachedHangarLimit(int MaxAmount)
        {
            return Grids.Count >= MaxAmount;
        }

        public bool AnyGridsMatch(string GridName)
        {
            return Grids.Any(x => x.GridName.Equals(GridName, StringComparison.Ordinal));
        }

        public bool TryFindGridIndex(string GridName, int result)
        {
            var FoundIndex = (short?)Grids.FindIndex(x => x.GridName.Equals(GridName, StringComparison.Ordinal));
            return FoundIndex != -1;
        }

        public bool GetGrid<T>(T GridNameOrNumber, out GridStamp Stamp, out string Message)
        {
            Message = string.Empty;
            Stamp = null;

            switch (GridNameOrNumber)
            {
                case int _:
                {
                    var Target = Convert.ToInt32(GridNameOrNumber);
                    if (!IsInputValid(Target, out Message))
                        return false;


                    Stamp = Grids[Target - 1];
                    return true;
                }
                case string _:
                {
                    //IsInputValid(SelectedIndex); ;

                    if (!int.TryParse(Convert.ToString(GridNameOrNumber), out var SelectedIndex))
                    {
                        Stamp = Grids.FirstOrDefault(x => x.GridName == Convert.ToString(GridNameOrNumber));
                        if (Stamp != null)
                            return true;
                    }



                    if (!IsInputValid(SelectedIndex, out Message))
                        return false;


                    Stamp = Grids[SelectedIndex - 1];
                    return true;
                }
                default:
                    return false;
            }
        }

        private void GetMaxHangarSlot()
        {

            if (_maxHangarSlots.HasValue)
            {
                MaxHangarSlots = _maxHangarSlots.Value;
            }
            else
            {
                var UserLvl = MySession.Static.GetUserPromoteLevel(SteamID);
                MaxHangarSlots = UserLvl switch
                {
                    MyPromoteLevel.Scripter => Config.ScripterHangarAmount,
                    MyPromoteLevel.Moderator => Config.ScripterHangarAmount * 2,
                    MyPromoteLevel.Admin => Config.ScripterHangarAmount * 10,
                    _ => Config.NormalHangarAmount
                };
            }
        }

        public bool IsInputValid(int Index, out string Message)
        {
            //Is input valid for index 1 - X
            Message = string.Empty;

            if (Index <= 0)
            {
                Message = "Please input a positive non-zero number";
                return false;
            }

            if (Index <= Grids.Count) return true;
            Message = "This hangar slot is empty! Select a grid that is in your hangar!";
            return false;

        }

        public void FormatGridName(GridStamp result)
        {
            try
            {
                result.GridName = FileSaver.CheckInvalidCharacters(result.GridName);
                // Log.Warn("Running GridName Checks: {" + GridName + "} :" + Test);

                if (!AnyGridsMatch(result.GridName)) return;
                //There is already a grid with that name!
                var NameCheckDone = false;
                var a = 1;
                while (!NameCheckDone)
                {
                    if (AnyGridsMatch(result.GridName + "[" + a + "]"))
                    {
                        a++;
                    }
                    else
                    {
                        //Hangar.Debug("Name check done! " + a);
                        NameCheckDone = true;
                    }
                }
                //Main.Debug("Saving grid name: " + GridName);
                result.GridName = result.GridName + "[" + a + "]";
            }
            catch (Exception e)
            {
                Log.Warn(e);
            }
        }

        public void ServerOfferPurchased(string name)
        {

            var Val = name.Trim();
            //Log.Info("SetServerOfferCount: " + Val);

            if (_serverOfferPurchases.ContainsKey(Val))
            {
                _serverOfferPurchases[Val] = _serverOfferPurchases[Val] + 1;
            }
            else
            {
                _serverOfferPurchases.Add(Val, 1);
            }
        }

        public int GetServerOfferPurchaseCount(string name)
        {
            var Val = name.Trim();
            //Log.Info("GetServerOfferCount: " + Val);
            return _serverOfferPurchases.ContainsKey(Val) ? _serverOfferPurchases[Val] : 0;
        }
        
        public async void SaveFile()
        {
            Directory.CreateDirectory(PlayerFolderPath);
             await FileSaver.SaveAsync(FilePath, this);
        }
        
    }
}
