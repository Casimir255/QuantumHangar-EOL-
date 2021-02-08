using Newtonsoft.Json;
using NLog;
using QuantumHangar.Serialization;
using QuantumHangar.Utils;
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
    public class PlayerHangar
    {
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(PlayerHangar));
        private Chat Chat;
        public readonly PlayerInfo SelectedPlayerFile;
        private int MaxHangarSlots = 5;
        private bool IsAdminCalling = false;
        private readonly ulong SteamID;
        private readonly long IdentityID;


        public string PlayersFolderPath { get; private set; }


        private static Settings Config { get { return Hangar.Config; } }


        public PlayerHangar(ulong SteamID, Chat Respond, bool IsAdminCalling = false)
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
            if(Index < 0)
            {
                Chat.Respond("Please input a positive non-zero number");
                return false;
            }


            if (Index > SelectedPlayerFile.Grids.Count && Index < MaxHangarSlots)
            {
                Chat.Respond("This hangar slot is empty! Select a grid that is in your hangar!");
                return false;
            }
            else if (Index > MaxHangarSlots)
            {
                Chat.Respond("Invalid number! You only have a max of " + MaxHangarSlots + " slots!");
                return false;
            }

            return true;
        }


        public static bool IsServerSaving(Chat Chat)
        {
            if (MySession.Static.IsSaveInProgress)
            {
                Chat.Respond("Server has a save in progress... Please wait!");
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
                    Chat.Respond("You have " + Timeformat + "  before you can perform this action!");
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
                    Chat.Respond("Grid is " + remainder + " blocks over the max slot block limit! " + Stamp.NumberofBlocks + "/" + Config.SingleMaxBlocks);
                    return false;
                }
            }

            if (Config.SingleMaxPCU != 0)
            {
                if (Stamp.GridPCU > Config.SingleMaxPCU)
                {
                    int remainder = Stamp.GridPCU - Config.SingleMaxPCU;
                    Chat.Respond("Grid is " + remainder + " PCU over the slot hangar PCU limit! " + Stamp.GridPCU + "/" + Config.SingleMaxPCU);
                    return false;
                }
            }

            if (Config.AllowStaticGrids)
            {
                if (Config.SingleMaxLargeGrids != 0 && Stamp.StaticGrids > Config.SingleMaxStaticGrids)
                {
                    int remainder = Stamp.StaticGrids - Config.SingleMaxStaticGrids;
                    Chat.Respond("You are " + remainder + " static grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (Stamp.StaticGrids > 0)
                {
                    Chat.Respond("Saving Static Grids is disabled!");
                    return false;
                }
            }

            if (Config.AllowLargeGrids)
            {
                if (Config.SingleMaxLargeGrids != 0 && Stamp.LargeGrids > Config.SingleMaxLargeGrids)
                {
                    int remainder = Stamp.LargeGrids - Config.SingleMaxLargeGrids;

                    Chat.Respond("You are " + remainder + " large grids over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (Stamp.LargeGrids > 0)
                {
                    Chat.Respond("Saving Large Grids is disabled!");
                    return false;
                }
            }

            if (Config.AllowSmallGrids)
            {
                if (Config.SingleMaxSmallGrids != 0 && Stamp.SmallGrids > Config.SingleMaxSmallGrids)
                {
                    int remainder = Stamp.SmallGrids - Config.SingleMaxLargeGrids;

                    Chat.Respond("You are " + remainder + " small grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (Stamp.SmallGrids > 0)
                {
                    Chat.Respond("Saving Small Grids is disabled!");
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

                Chat.Respond("Grid is " + remainder + " blocks over the total hangar block limit! " + TotalBlocks + "/" + Config.TotalMaxBlocks);
                return false;
            }

            if (Config.TotalMaxPCU != 0 && TotalPCU > Config.TotalMaxPCU)
            {

                int remainder = TotalPCU - Config.TotalMaxPCU;
                Chat.Respond("Grid is " + remainder + " PCU over the total hangar PCU limit! " + TotalPCU + "/" + Config.TotalMaxPCU);
                return false;
            }


            if (Config.TotalMaxStaticGrids != 0 && StaticGrids > Config.TotalMaxStaticGrids)
            {
                int remainder = StaticGrids - Config.TotalMaxStaticGrids;

                Chat.Respond("You are " + remainder + " static grid over the total hangar limit!");
                return false;
            }


            if (Config.TotalMaxLargeGrids != 0 && LargeGrids > Config.TotalMaxLargeGrids)
            {
                int remainder = LargeGrids - Config.TotalMaxLargeGrids;

                Chat.Respond("You are " + remainder + " large grid over the total hangar limit!");
                return false;
            }


            if (Config.TotalMaxSmallGrids != 0 && SmallGrids > Config.TotalMaxSmallGrids)
            {
                int remainder = LargeGrids - Config.TotalMaxSmallGrids;

                Chat.Respond("You are " + remainder + " small grid over the total hangar limit!");
                return false;
            }

            return true;
        }

        public bool CheckHanagarLimits()
        {
            if (SelectedPlayerFile.Grids.Count >= MaxHangarSlots)
            {
                Chat.Respond("You have reached your hangar limit!");
                return false;
            }

            return true;

        }



        public void SaveGridStamp(GridStamp Stamp, long IdentityID, bool Admin = false)
        {
            if (!Admin)
            {
                TimeStamp stamp = new TimeStamp();
                stamp.OldTime = DateTime.Now;
                SelectedPlayerFile.Timer = stamp;
            }

           

            SelectedPlayerFile.Grids.Add(Stamp);
            SelectedPlayerFile.SaveFile();
        }

        public void RemoveGridStamp(GridStamp Stamp)
        {
            TimeStamp stamp = new TimeStamp();
            stamp.OldTime = DateTime.Now;
            stamp.PlayerID = IdentityID;


            

            SelectedPlayerFile.Grids.Remove(Stamp);
            SelectedPlayerFile.Timer = stamp;

            try
            {
                File.Delete(Path.Combine(PlayersFolderPath, Stamp.GridName + ".sbc"));
                SelectedPlayerFile.SaveFile();
            }catch(Exception ex)
            {
                Log.Error(ex);
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
                if(IsAdminCalling)
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
        }

        public bool LoadGrid(int ID, out IEnumerable<MyObjectBuilder_CubeGrid> Grids, out GridStamp Stamp)
        {
            Grids = null;
            Stamp = null;
            if (ID - 1 >= SelectedPlayerFile.Grids.Count || ID < 1)
            {
                Chat.Respond("Invalid Index! Grid doent exsist in that slot!");
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
            IEnumerable<string> myFiles = Directory.EnumerateFiles(PlayersFolderPath, "*.*", SearchOption.TopDirectoryOnly).Where(s => Path.GetExtension(s).TrimStart('.').ToLowerInvariant() == "sbc" );

            //Scan for new grids

            List<GridStamp> NewGrids = new List<GridStamp>();
            int AddedGrids = 0;
            foreach(var file in myFiles)
            {
                if (SelectedPlayerFile.AnyGridsMatch(Path.GetFileNameWithoutExtension(file)))
                    continue;

                AddedGrids++;
                GridStamp Stamp = new GridStamp(file);
                NewGrids.Add(Stamp);
            }

            int RemovedGrids = 0;
            for(int i = SelectedPlayerFile.Grids.Count -1; i >= 0; i--)
            {
                if (!myFiles.Any(x => Path.GetFileNameWithoutExtension(x) == SelectedPlayerFile.Grids[i].GridName))
                {
                    RemovedGrids++;
                    SelectedPlayerFile.Grids.RemoveAt(i);
                }
            }

            SelectedPlayerFile.Grids.AddRange(NewGrids);
            SelectedPlayerFile.SaveFile();

            Chat.Respond("Removed " + RemovedGrids + " grids and added " + AddedGrids + " new grids to hangar");


        }




    }





    [JsonObject(MemberSerialization.OptIn)]
    public class PlayerInfo
    {
        //This is the players info file. Should contain methods for finding grids/checking timers 

        [JsonProperty]  public List<GridStamp> Grids = new List<GridStamp>();
        [JsonProperty]  public TimeStamp Timer;

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

        private Settings Config { get { return Hangar.Config;  } }


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
                //Chat.Respond("You have " + Timeformat + "  before you can perform this action!", Context);
                return false;
            }
            else
            {
                return true;
            }
        }

        public bool ReachedHangarLimit(int MaxAmount)
        {
            if(Grids.Count >= MaxAmount)
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
            return Grids[ID-1];
        }

        public void SaveFile()
        {
            FileSaver.Save(FilePath, this);
        }



        

    }



}
