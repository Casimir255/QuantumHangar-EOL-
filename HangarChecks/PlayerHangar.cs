using Newtonsoft.Json;
using NLog;
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
using VRage.Game;
using VRage.Game.ModAPI;

namespace QuantumHangar.HangarChecks
{
    //This is for all users. Doesnt matter who is invoking it. (Admin or different). Should contain main functions for the player hangar. (Either removing grids and saving, checking stamps etc)
    public class PlayerHangar : IDisposable
    {
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(PlayerHangar));
        private readonly Chat Chat;
        public readonly PlayerInfo SelectedPlayerFile;
        private int MaxHangarSlots = 5;
        private bool IsAdminCalling = false;
        private readonly ulong SteamID;
        private readonly long IdentityID;


        public string PlayersFolderPath { get; private set; }


        private static Settings Config { get { return Hangar.Config; } }


        public PlayerHangar(ulong SteamID, Chat Respond, bool IsAdminCalling = false)
        {
            try
            {

                this.SteamID = SteamID;
                this.IdentityID = MySession.Static.Players.TryGetIdentityId(SteamID);

                this.IsAdminCalling = IsAdminCalling;
                Chat = Respond;
                SelectedPlayerFile = new PlayerInfo();


                PlayersFolderPath = Path.Combine(Config.FolderDirectory, SteamID.ToString());
                SelectedPlayerFile.LoadFile(Config.FolderDirectory, SteamID);
                GetMaxHangarSlot(SteamID);

            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }


        private void GetMaxHangarSlot(ulong SteamID)
        {
            MyPromoteLevel UserLvl = MySession.Static.GetUserPromoteLevel(SteamID);

            MaxHangarSlots = Config.NormalHangarAmount;
            if (UserLvl == MyPromoteLevel.Scripter)
            {
                MaxHangarSlots = Config.ScripterHangarAmount;
            }
            else if (UserLvl == MyPromoteLevel.Moderator)
            {
                MaxHangarSlots = Config.ScripterHangarAmount * 2;
            }
            else if (UserLvl >= MyPromoteLevel.Admin)
            {
                MaxHangarSlots = Config.ScripterHangarAmount * 10;
            }
        }


        public bool ParseSelectedHangarInput(string GridNameOrNumber, out short SelectedIndex)
        {
            SelectedIndex = 0;
            if (Int16.TryParse(GridNameOrNumber, out SelectedIndex))
            {
                return IsInputValid(SelectedIndex);
            }
            else
            {
                if (SelectedPlayerFile.TryFindGridIndex(GridNameOrNumber, out SelectedIndex))
                    return IsInputValid(SelectedIndex); ;

                return false;
            }
        }
        private bool IsInputValid(int Index)
        {
            if (Index < 0)
            {
                Chat?.Respond("Please input a positive non-zero number");
                return false;
            }


            if (Index > SelectedPlayerFile.Grids.Count && Index < MaxHangarSlots)
            {
                Chat?.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                return false;
            }
           
            return true;
        }


        public static bool IsServerSaving(Chat Chat)
        {
            if (MySession.Static.IsSaveInProgress)
            {
                Chat?.Respond("Server has a save in progress... Please wait!");
                return true;
            }

            return false;
        }

        public bool CheckPlayerTimeStamp()
        {
            //Check timestamp before continuing!
            if (SelectedPlayerFile.Timer != null)
            {
                TimeStamp Old = SelectedPlayerFile.Timer;
                //There is a time limit!
                TimeSpan Subtracted = DateTime.Now.Subtract(Old.OldTime);
                TimeSpan WaitTimeSpawn = new TimeSpan(0, (int)Config.WaitTime, 0);
                TimeSpan Remainder = WaitTimeSpawn - Subtracted;
                //Log.Info("TimeSpan: " + Subtracted.TotalMinutes);
                if (Subtracted.TotalMinutes <= Config.WaitTime)
                {
                    //int RemainingTime = (int)Plugin.Config.WaitTime - Convert.ToInt32(Subtracted.TotalMinutes);
                    string Timeformat = string.Format("{0:mm}min & {0:ss}s", Remainder);
                    Chat?.Respond("You have " + Timeformat + "  before you can perform this action!");
                    return false;
                }
                else
                {
                    SelectedPlayerFile.Timer = null;
                    return true;
                }
            }

            return true;
        }

        public bool ExtensiveLimitChecker(GridStamp Stamp)
        {
            //Begin Single Slot Save!


            if (Config.SingleMaxBlocks != 0)
            {
                if (Stamp.NumberofBlocks > Config.SingleMaxBlocks)
                {
                    int remainder = Stamp.NumberofBlocks - Config.SingleMaxBlocks;
                    Chat?.Respond("Grid is " + remainder + " blocks over the max slot block limit! " + Stamp.NumberofBlocks + "/" + Config.SingleMaxBlocks);
                    return false;
                }
            }

            if (Config.SingleMaxPCU != 0)
            {
                if (Stamp.GridPCU > Config.SingleMaxPCU)
                {
                    int remainder = Stamp.GridPCU - Config.SingleMaxPCU;
                    Chat?.Respond("Grid is " + remainder + " PCU over the slot hangar PCU limit! " + Stamp.GridPCU + "/" + Config.SingleMaxPCU);
                    return false;
                }
            }

            if (Config.AllowStaticGrids)
            {
                if (Config.SingleMaxLargeGrids != 0 && Stamp.StaticGrids > Config.SingleMaxStaticGrids)
                {
                    int remainder = Stamp.StaticGrids - Config.SingleMaxStaticGrids;
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
                    int remainder = Stamp.LargeGrids - Config.SingleMaxLargeGrids;

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
                    int remainder = Stamp.SmallGrids - Config.SingleMaxLargeGrids;

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


            int TotalBlocks = 0;
            int TotalPCU = 0;
            int StaticGrids = 0;
            int LargeGrids = 0;
            int SmallGrids = 0;

            //Hangar total limit!
            foreach (GridStamp Grid in SelectedPlayerFile.Grids)
            {
                TotalBlocks += Grid.NumberofBlocks;
                TotalPCU += Grid.GridPCU;

                StaticGrids += Grid.StaticGrids;
                LargeGrids += Grid.LargeGrids;
                SmallGrids += Grid.SmallGrids;
            }

            if (Config.TotalMaxBlocks != 0 && TotalBlocks > Config.TotalMaxBlocks)
            {
                int remainder = TotalBlocks - Config.TotalMaxBlocks;

                Chat?.Respond("Grid is " + remainder + " blocks over the total hangar block limit! " + TotalBlocks + "/" + Config.TotalMaxBlocks);
                return false;
            }

            if (Config.TotalMaxPCU != 0 && TotalPCU > Config.TotalMaxPCU)
            {

                int remainder = TotalPCU - Config.TotalMaxPCU;
                Chat?.Respond("Grid is " + remainder + " PCU over the total hangar PCU limit! " + TotalPCU + "/" + Config.TotalMaxPCU);
                return false;
            }


            if (Config.TotalMaxStaticGrids != 0 && StaticGrids > Config.TotalMaxStaticGrids)
            {
                int remainder = StaticGrids - Config.TotalMaxStaticGrids;

                Chat?.Respond("You are " + remainder + " static grid over the total hangar limit!");
                return false;
            }


            if (Config.TotalMaxLargeGrids != 0 && LargeGrids > Config.TotalMaxLargeGrids)
            {
                int remainder = LargeGrids - Config.TotalMaxLargeGrids;

                Chat?.Respond("You are " + remainder + " large grid over the total hangar limit!");
                return false;
            }


            if (Config.TotalMaxSmallGrids != 0 && SmallGrids > Config.TotalMaxSmallGrids)
            {
                int remainder = LargeGrids - Config.TotalMaxSmallGrids;

                Chat?.Respond("You are " + remainder + " small grid over the total hangar limit!");
                return false;
            }

            return true;
        }

        public bool CheckHanagarLimits()
        {
            if (SelectedPlayerFile.Grids.Count >= MaxHangarSlots)
            {
                Chat?.Respond("You have reached your hangar limit!");
                return false;
            }

            return true;

        }



        public void SaveGridStamp(GridStamp Stamp, bool Admin = false, bool IgnoreSave = false)
        {
            if (!Admin)
            {
                TimeStamp stamp = new TimeStamp();
                stamp.OldTime = DateTime.Now;
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




        public bool RemoveGridStamp(GridStamp Stamp)
        {
            int Index = SelectedPlayerFile.Grids.FindIndex(x => x == Stamp);
            return RemoveStamp(Index);
        }


        public bool RemoveGridStamp(int ID)
        {
            return RemoveStamp(ID - 1);
        }

        private bool RemoveStamp(int ID)
        {
            if (!IsInputValid(ID))
                return false;


            if (!IsAdminCalling)
            {
                TimeStamp stamp = new TimeStamp();
                stamp.OldTime = DateTime.Now;
                stamp.PlayerID = IdentityID;
                SelectedPlayerFile.Timer = stamp;
            }

            try
            {
                File.Delete(Path.Combine(PlayersFolderPath, SelectedPlayerFile.Grids[ID].GridName + ".sbc"));
                SelectedPlayerFile.Grids.RemoveAt(ID);
                SelectedPlayerFile.SaveFile();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }


        public bool SaveGridsToFile(GridResult Grids, string FileName)
        {
            return GridSerializer.SaveGridsAndClose(Grids.Grids, PlayersFolderPath, FileName, IdentityID);
        }





        public void ListAllGrids()
        {

            if (SelectedPlayerFile.Grids.Count == 0)
            {
                if (IsAdminCalling)
                    Chat.Respond("There are no grids in this players hangar!");
                else
                    Chat.Respond("You have no grids in your hangar!");

                return;
            }



            var sb = new StringBuilder();

            if (IsAdminCalling)
                sb.AppendLine("Players has " + SelectedPlayerFile.Grids.Count() + "/" + MaxHangarSlots + " stored grids:");
            else
                sb.AppendLine("You have " + SelectedPlayerFile.Grids.Count() + "/" + MaxHangarSlots + " stored grids:");


            int count = 1;
            foreach (var grid in SelectedPlayerFile.Grids)
            {
                sb.AppendLine(" [" + count + "] - " + grid.GridName);
                count++;
            }




            Chat.Respond(sb.ToString());

            return;

        }

        public bool LoadGrid(int ID, out IEnumerable<MyObjectBuilder_CubeGrid> Grids, out GridStamp Stamp)
        {
            Grids = null;
            Stamp = null;
            if (ID - 1 >= SelectedPlayerFile.Grids.Count || ID < 1)
            {
                Chat?.Respond("Invalid Index! Grid doent exsist in that slot!");
                return false;
            }


            Stamp = SelectedPlayerFile.GetGrid(ID);
            string GridPath = Path.Combine(PlayersFolderPath, Stamp.GridName + ".sbc");

            Log.Warn("Attempting to load grid @" + GridPath);
            if (!GridSerializer.LoadGrid(GridPath, out Grids))
                return false;

            PluginDependencies.BackupGrid(Grids.ToList(), IdentityID);
            GridSerializer.TransferGridOwnership(Grids, IdentityID);

            return true;
        }


        public void UpdateHangar()
        {
            IEnumerable<string> myFiles = Directory.EnumerateFiles(PlayersFolderPath, "*.*", SearchOption.TopDirectoryOnly).Where(s => Path.GetExtension(s).TrimStart('.').ToLowerInvariant() == "sbc");

            //Scan for new grids

            List<GridStamp> NewGrids = new List<GridStamp>();
            int AddedGrids = 0;
            foreach (var file in myFiles)
            {
                if (SelectedPlayerFile.AnyGridsMatch(Path.GetFileNameWithoutExtension(file)))
                    continue;

                AddedGrids++;
                GridStamp Stamp = new GridStamp(file);
                NewGrids.Add(Stamp);
            }

            int RemovedGrids = 0;
            for (int i = SelectedPlayerFile.Grids.Count - 1; i >= 0; i--)
            {
                if (!myFiles.Any(x => Path.GetFileNameWithoutExtension(x) == SelectedPlayerFile.Grids[i].GridName))
                {
                    RemovedGrids++;
                    SelectedPlayerFile.Grids.RemoveAt(i);
                }
            }

            SelectedPlayerFile.Grids.AddRange(NewGrids);
            SelectedPlayerFile.SaveFile();

            Chat?.Respond("Removed " + RemovedGrids + " grids and added " + AddedGrids + " new grids to hangar");


        }

        public void Dispose()
        {

        }


        public bool CheckLimits(GridStamp Grid, IEnumerable<MyObjectBuilder_CubeGrid> Blueprint)
        {

            if (CheckGridLimits(Grid) == false || BlockLimitChecker(Blueprint) == false)
                return false;

            return true;

        }


        private bool CheckGridLimits( GridStamp Grid)
        {
            //Backwards compatibale
            if (Config.OnLoadTransfer)
                return true;

            MyIdentity NewPlayer = MySession.Static.Players.TryGetIdentity(IdentityID);
            if(NewPlayer == null)
            {
                Log.Fatal("Unable to get player identity! " + IdentityID);
                return false;
            }


            if (Grid.ShipPCU.Count == 0)
            {

                MyBlockLimits blockLimits = NewPlayer.BlockLimits;

                MyBlockLimits a = MySession.Static.GlobalBlockLimits;

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

                int CurrentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                int MaxPcu = blockLimits.PCU + CurrentPcu;

                int pcu = MaxPcu - CurrentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                //Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                //Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (MaxPcu - CurrentPcu <= Grid.GridPCU)
                {
                    int Need = Grid.GridPCU - (MaxPcu - CurrentPcu);
                    Chat.Respond("PCU limit reached! You need an additional " + Need + " pcu to perform this action!");
                    return false;
                }

                return true;
            }


            foreach (KeyValuePair<long, int> Player in Grid.ShipPCU)
            {

                MyIdentity Identity = MySession.Static.Players.TryGetIdentity(Player.Key);
                if (Identity == null)
                {
                    continue;
                }


                MyBlockLimits blockLimits = Identity.BlockLimits;
                MyBlockLimits a = MySession.Static.GlobalBlockLimits;

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

                int CurrentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                int MaxPcu = blockLimits.PCU + CurrentPcu;

                int pcu = MaxPcu - CurrentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                //Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                //Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (MaxPcu - CurrentPcu <= Player.Value)
                {
                    int Need = Player.Value - (MaxPcu - CurrentPcu);
                    Chat.Respond("PCU limit reached! " + Identity.DisplayName + " needs an additional " + Need + " PCU to load this grid!");
                    return false;
                }

            }

            return true;
        }


        private bool BlockLimitChecker(IEnumerable<MyObjectBuilder_CubeGrid> shipblueprints)
        {
            int BiggestGrid = 0;
            int blocksToBuild = 0;
            //failedBlockType = null;
            //Need dictionary for each player AND their blocks they own. (Players could own stuff on the same grid)
            Dictionary<long, Dictionary<string, int>> BlocksAndOwnerForLimits = new Dictionary<long, Dictionary<string, int>>();


            //Total PCU and Blocks
            int FinalBlocksCount = 0;
            int FinalBlocksPCU = 0;


            Dictionary<string, int> BlockPairNames = new Dictionary<string, int>();
            Dictionary<string, int> BlockSubTypeNames = new Dictionary<string, int>();


            //Go ahead and check if the block limits is enabled server side! If it isnt... continue!
            if (!Config.EnableBlackListBlocks)
            {
                return true;
            }


            else
            {
                //If we are using built in server block limits..
                if (Config.SBlockLimits)
                {
                    //& the server blocklimits is not enabled... Return true
                    if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.NONE)
                    {
                        return true;
                    }


                    //Cycle each grid in the ship blueprints

                        foreach (var CubeGrid in shipblueprints)
                        {

                            //Main.Debug("CubeBlocks count: " + CubeGrid.GetType());
                            if (BiggestGrid < CubeGrid.CubeBlocks.Count())
                            {
                                BiggestGrid = CubeGrid.CubeBlocks.Count();
                            }
                            blocksToBuild = blocksToBuild + CubeGrid.CubeBlocks.Count();

                            foreach (MyObjectBuilder_CubeBlock block in CubeGrid.CubeBlocks)
                            {

                                MyDefinitionId defId = new MyDefinitionId(block.TypeId, block.SubtypeId);

                                if (MyDefinitionManager.Static.TryGetCubeBlockDefinition(defId, out MyCubeBlockDefinition myCubeBlockDefinition))
                                {
                                    //Check for BlockPair or SubType?
                                    string BlockName = "";
                                    if (Config.SBlockLimits)
                                    {
                                        //Server Block Limits
                                        BlockName = myCubeBlockDefinition.BlockPairName;

                                    }
                                    else
                                    {
                                        //Custom Block SubType Limits
                                        BlockName = myCubeBlockDefinition.Id.SubtypeName;
                                    }

                                    long blockowner2 = 0L;
                                    blockowner2 = block.BuiltBy;

                                    //If the player dictionary already has a Key, we need to retrieve it
                                    if (BlocksAndOwnerForLimits.ContainsKey(blockowner2))
                                    {
                                        //if the dictionary already contains the same block type
                                        Dictionary<string, int> dictforuser = BlocksAndOwnerForLimits[blockowner2];
                                        if (dictforuser.ContainsKey(BlockName))
                                        {
                                            dictforuser[BlockName]++;
                                        }
                                        else
                                        {
                                            dictforuser.Add(BlockName, 1);
                                        }
                                        BlocksAndOwnerForLimits[blockowner2] = dictforuser;
                                    }
                                    else
                                    {
                                        BlocksAndOwnerForLimits.Add(blockowner2, new Dictionary<string, int>
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


                            }

                            FinalBlocksCount += CubeGrid.CubeBlocks.Count;
                        
                    }




                    if (MySession.Static.MaxGridSize != 0 && BiggestGrid > MySession.Static.MaxGridSize)
                    {
                        Chat.Respond("Biggest grid is over Max grid size! ");
                        return false;
                    }

                    //Need too loop player identities in dictionary. Do this via seperate function
                    if (PlayerIdentityLoop(BlocksAndOwnerForLimits, FinalBlocksCount) == true)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
                else
                {
                    //BlockLimiter
                    if (!PluginDependencies.BlockLimiterInstalled)
                    {
                        //BlockLimiter is null!
                        Chat.Respond("Blocklimiter Plugin not installed or Loaded!");
                        Log.Warn("BLimiter plugin not installed or loaded! May require a server restart!");
                        return false;
                    }


                    List<MyObjectBuilder_CubeGrid> grids = new List<MyObjectBuilder_CubeGrid>();

                        foreach (var CubeGrid in shipblueprints)
                        {
                            grids.Add(CubeGrid);
                        }
                    


                    bool ValueReturn = PluginDependencies.CheckGridLimits(grids, IdentityID);

                    //Convert to value return type
                    if (!ValueReturn)
                    {
                        //Main.Debug("Cannont load grid in due to BlockLimiter Configs!");
                        return true;
                    }
                    else
                    {
                        Chat.Respond("Grid would be over Server-Blocklimiter limits!");
                        return false;
                    }
                }
            }
        }

        private bool PlayerIdentityLoop(Dictionary<long, Dictionary<string, int>> BlocksAndOwnerForLimits, int blocksToBuild)
        {
            foreach (KeyValuePair<long, Dictionary<string, int>> Player in BlocksAndOwnerForLimits)
            {

                Dictionary<string, int> PlayerBuiltBlocks = Player.Value;
                MyIdentity myIdentity = MySession.Static.Players.TryGetIdentity(Player.Key);
                if (myIdentity != null)
                {
                    MyBlockLimits blockLimits = myIdentity.BlockLimits;
                    if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.PER_FACTION && MySession.Static.Factions.GetPlayerFaction(myIdentity.IdentityId) == null)
                    {
                        Chat.Respond("ServerLimits are set PerFaction. You are not in a faction! Contact an Admin!");
                        return false;
                    }

                    if (blockLimits != null)
                    {


                        if (MySession.Static.MaxBlocksPerPlayer != 0 && blockLimits.BlocksBuilt + blocksToBuild > blockLimits.MaxBlocks)
                        {
                            Chat.Respond("Cannot load grid! You would be over your Max Blocks!");
                            return false;
                        }

                        //Double check to see if the list is null
                        if (PlayerBuiltBlocks != null)
                        {
                            foreach (KeyValuePair<string, short> ServerBlockLimits in MySession.Static.BlockTypeLimits)
                            {
                                if (PlayerBuiltBlocks.ContainsKey(ServerBlockLimits.Key))
                                {
                                    int TotalNumberOfBlocks = PlayerBuiltBlocks[ServerBlockLimits.Key];

                                    if (blockLimits.BlockTypeBuilt.TryGetValue(ServerBlockLimits.Key, out MyBlockLimits.MyTypeLimitData LimitData))
                                    {
                                        //Grab their existing block count for the block limit
                                        TotalNumberOfBlocks += LimitData.BlocksBuilt;
                                    }

                                    //Compare to see if they would be over!
                                    short ServerLimit = MySession.Static.GetBlockTypeLimit(ServerBlockLimits.Key);
                                    if (TotalNumberOfBlocks > ServerLimit)
                                    {
                                        Chat.Respond("Player " + myIdentity.DisplayName + " would be over their " + ServerBlockLimits.Key + " limits! " + TotalNumberOfBlocks + "/" + ServerLimit);
                                        //Player would be over their block type limits
                                        return false;
                                    }
                                }
                            }
                        }

                    }


                }
            }

            return true;
        }

    }





    [JsonObject(MemberSerialization.OptIn)]
    public class PlayerInfo
    {
        //This is the players info file. Should contain methods for finding grids/checking timers 

        [JsonProperty] public List<GridStamp> Grids = new List<GridStamp>();
        [JsonProperty] public TimeStamp Timer;

        //This can prob be removed
        [JsonProperty] public List<GridStamp> GridBackups = new List<GridStamp>();

        public string FilePath { get; set; }
        public ulong SteamID { get; set; }

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public int TotalBlocks { get; private set; } = 0;
        public int TotalPCU { get; private set; } = 0;
        public int StaticGrids { get; private set; } = 0;
        public int LargeGrids { get; private set; } = 0;
        public int SmallGrids { get; private set; } = 0;

        private Settings Config { get { return Hangar.Config; } }


        public bool LoadFile(string FolderPath, ulong SteamID)
        {

            this.SteamID = SteamID;
            string PlayerFolderPath = Path.Combine(FolderPath, SteamID.ToString());
            FilePath = Path.Combine(PlayerFolderPath, "PlayerInfo.json");

            if (!File.Exists(FilePath))
                return true;


            try
            {
                PlayerInfo ScannedFile = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(FilePath));
                this.Grids = ScannedFile.Grids;
                this.Timer = ScannedFile.Timer;
            }
            catch (Exception e)
            {
                Log.Warn(e, "For some reason the file is broken");
                return false;
            }

            PerfromDataScan();
            return true;
        }

        private void PerfromDataScan()
        {
            //Accumulate Grid Data
            foreach (GridStamp Grid in Grids)
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

            TimeSpan Subtracted = DateTime.Now.Subtract(Timer.OldTime);
            TimeSpan WaitTimeSpawn = new TimeSpan(0, (int)Config.WaitTime, 0);
            TimeSpan Remainder = WaitTimeSpawn - Subtracted;

            if (Subtracted.TotalMinutes <= Config.WaitTime)
            {
                //int RemainingTime = (int)Plugin.Config.WaitTime - Convert.ToInt32(Subtracted.TotalMinutes);
                Response = string.Format("{0:mm}min & {0:ss}s", Remainder);
                //Chat?.Respond("You have " + Timeformat + "  before you can perform this action!", Context);
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool ReachedHangarLimit(int MaxAmount)
        {
            if (Grids.Count >= MaxAmount)
            {
                return true;
            }

            return false;
        }

        public bool AnyGridsMatch(string GridName)
        {
            return Grids.Any(x => x.GridName.Equals(GridName, StringComparison.CurrentCultureIgnoreCase));
        }

        public bool TryFindGridIndex(string GridName, out short result)
        {
            short? FoundIndex = (short?)Grids.FindIndex(x => x.GridName.Equals(GridName, StringComparison.CurrentCultureIgnoreCase));
            result = FoundIndex ?? -1;
            return FoundIndex.HasValue;
        }

        public GridStamp GetGrid(int ID)
        {
            return Grids[ID - 1];
        }

        public void SaveFile()
        {
            FileSaver.Save(FilePath, this);
        }





    }



}
