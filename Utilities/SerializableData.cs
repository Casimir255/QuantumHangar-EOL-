using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using QuantumHangar.Utils;
using Sandbox.Game.Entities.Character;
using VRage.Game;
using VRage.Game.ModAPI;
using Sandbox.Game.World;
using NLog;
using QuantumHangar.Serialization;
using QuantumHangar.HangarMarket;

namespace QuantumHangar
{
    public enum CostType
    {
        BlockCount,
        PerGrid,
        Fixed
    };


    public enum LoadType
    {
        ForceLoadMearPlayer,
        Optional,
        ForceLoadNearOriginalPosition

    }

    public enum MessageType
    {
        RequestAllItems,
        AddOne,
        RemoveOne,
        SendDefinition,
        PurchasedGrid,
    }


    public class FileSaver
    {
        //A regex invalidCharCollection
        private static Regex InvalidNameScanner = new Regex(string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()))));

        public static void Save(string dir, object data)
        {
            //All methods calling this should still actually be in another thread... So we dont need to call it again.
            FileSaveTask(dir, data);
        }

        private static void FileSaveTask(string dir, object data)
        {
            try
            {
                File.WriteAllText(dir, JsonConvert.SerializeObject(data, Formatting.Indented));
            }
            catch (Exception e)
            {
                //Hangar.Debug("Unable to save file @" + dir, e, Hangar.ErrorType.Trace);
            }
        }


        public static string CheckInvalidCharacters(string filename)
        {
            //This will get any invalid file names and remove those characters
            return InvalidNameScanner.Replace(filename, "");
        }
    }

    public class ZoneRestrictions
    {
        public string Name { get; set; }

        public bool AllowSaving { get; set; }
        public bool AllowLoading { get; set; }


        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public double Radius { get; set; }


    }


    public class HangarBlacklist
    {
        public string Name { get; set; }
        public ulong SteamID { get; set; }
    }


    public class TimeStamp
    {
        public DateTime OldTime;
    }

    public class GridStamp
    {
        public long GridID;
        public string GridName;
        public int GridPCU;
        public int ServerPort = 0;
        public bool GridForSale = false;
        public double MarketValue = 0;
        public Dictionary<long, int> ShipPCU = new Dictionary<long, int>();
        public bool ForceSpawnNearPlayer = false;
        public bool TransferOwnerShipOnLoad = false;



        public string SellerFaction = "N/A";
        public float GridMass = 0;
        public int StaticGrids = 0;
        public int SmallGrids = 0;
        public int LargeGrids = 0;
        public int NumberofBlocks = 0;
        public float MaxPowerOutput = 0;
        public float GridBuiltPercent = 0;
        public long JumpDistance = 0;
        public int NumberOfGrids = 0;
        public Vector3D GridSavePosition = new Vector3D(0, 0, 0);


        //Server blocklimits Block
        public Dictionary<string, int> BlockTypeCount = new Dictionary<string, int>();


        //Grid Stored Materials
        public Dictionary<string, double> StoredMaterials = new Dictionary<string, double>();


        [JsonIgnore]
        private Settings Config { get { return Hangar.Config; } }

        [JsonIgnore]
        public string OriginalGridPath { get; }

        public GridStamp(List<MyCubeGrid> Grids)
        {

            float DisassembleRatio = 0;
            double EstimatedValue = 0;

            BlockTypeCount.Add("Reactors", 0);
            BlockTypeCount.Add("Turrets", 0);
            BlockTypeCount.Add("StaticGuns", 0);
            BlockTypeCount.Add("Refineries", 0);
            BlockTypeCount.Add("Assemblers", 0);

            foreach (MyCubeGrid SingleGrid in Grids)
            {
                if (SingleGrid.GridSizeEnum == MyCubeSize.Large)
                {
                    if (SingleGrid.IsStatic)
                    {
                        StaticGrids += 1;
                        EstimatedValue += SingleGrid.BlocksCount * Config.StaticGridMarketMultiplier;
                    }
                    else
                    {
                        LargeGrids += 1;
                        EstimatedValue += SingleGrid.BlocksCount * Config.LargeGridMarketMultiplier;
                    }
                }
                else
                {
                    SmallGrids += 1;
                    EstimatedValue += SingleGrid.BlocksCount * Config.SmallGridMarketMultiplier;
                }


                foreach (MyCubeBlock SingleBlock in SingleGrid.GetFatBlocks())
                {
                    var Block = (IMyCubeBlock)SingleBlock;


                    if (SingleBlock.BuiltBy != 0)
                    {
                        UpdatePCUCounter(SingleBlock.BuiltBy, SingleBlock.BlockDefinition.PCU);
                    }

                    if (Block as IMyLargeTurretBase != null)
                    {
                        BlockTypeCount["Turrets"] += 1;
                    }
                    if (Block as IMySmallGatlingGun != null)
                    {
                        BlockTypeCount["Turrets"] += 1;
                    }

                    if (Block as IMyGunBaseUser != null)
                    {
                        BlockTypeCount["StaticGuns"] += 1;
                    }

                    if (Block as IMyRefinery != null)
                    {
                        BlockTypeCount["Refineries"] += 1;
                    }
                    if (Block as IMyAssembler != null)
                    {
                        BlockTypeCount["Assemblers"] += 1;
                    }


                    //Main.Debug("Block:" + Block.BlockDefinition + " ratio: " + Block.BlockDefinition.);
                    DisassembleRatio += SingleBlock.BlockDefinition.DeformationRatio;
                   NumberofBlocks += 1;
                }

                BlockTypeCount["Reactors"] += SingleGrid.NumberOfReactors;
                NumberOfGrids += 1;
                GridMass += SingleGrid.Mass;
                GridPCU += SingleGrid.BlocksPCU;
            }

            if (Grids[0].BigOwners.Count > 0)
                Grids[0].GridSystems.JumpSystem.GetMaxJumpDistance(Grids[0].BigOwners[0]);

            //Get Total Build Percent
            GridBuiltPercent = DisassembleRatio / NumberofBlocks;
            MarketValue = EstimatedValue;
        }

        public GridStamp() { }

        public GridStamp(string file) {
            OriginalGridPath = file;
            GridName = Path.GetFileNameWithoutExtension(file);
            ForceSpawnNearPlayer = true;
            GridSavePosition = Vector3D.Zero;
            TransferOwnerShipOnLoad = true;
        }



       


        public void UpdateBiggestGrid(MyCubeGrid BiggestGrid)
        {
            GridName = BiggestGrid.DisplayName;

            GridID = BiggestGrid.EntityId;
            GridSavePosition = BiggestGrid.PositionComp.GetPosition();
        }
        public bool CheckGridLimits(Chat Response, MyIdentity TargetIdentity)
        {
            //No need to check limits
            if (Config.OnLoadTransfer)
                return true;

            if (ShipPCU.Count == 0)
            {
                MyBlockLimits blockLimits = TargetIdentity.BlockLimits;

                MyBlockLimits a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                {
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Hangar.Debug("PCU Server limits is 0!");
                    return true;
                }

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                int CurrentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                int MaxPcu = blockLimits.PCU + CurrentPcu;

                int pcu = MaxPcu - CurrentPcu;

                //Find the difference
                if (MaxPcu - CurrentPcu <= GridPCU)
                {
                    int Need = GridPCU - (MaxPcu - CurrentPcu);
                    Response.Respond("PCU limit reached! You need an additional " + Need + " pcu to perform this action!");
                    return false;
                }

                return true;
            }


            foreach (KeyValuePair<long, int> Player in ShipPCU)
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

                int CurrentPcu = blockLimits.PCUBuilt;
                int MaxPcu = blockLimits.PCU + CurrentPcu;
                int pcu = MaxPcu - CurrentPcu;

                //Find the difference
                if (MaxPcu - CurrentPcu <= Player.Value)
                {
                    int Need = Player.Value - (MaxPcu - CurrentPcu);
                    Response.Respond("PCU limit reached! " + Identity.DisplayName + " needs an additional " + Need + " PCU to load this grid!");
                    return false;
                }

            }

            return true;
        }
        public void UpdatePCUCounter(long Player, int Amount)
        {
            if (ShipPCU.ContainsKey(Player))
            {
                ShipPCU[Player] += Amount;
            }
            else
            {
                ShipPCU.Add(Player, Amount);
            }
        }

        public string GetGridPath(string PlayersFolderPath)
        {
            return Path.Combine(PlayersFolderPath, GridName + ".sbc");
        }



        public bool TryGetGrids(string PlayersFolderPath, out IEnumerable<MyObjectBuilder_CubeGrid> Grids)
        {
            Grids = null;
            string GridPath = Path.Combine(PlayersFolderPath, GridName + ".sbc");

            if (!GridSerializer.LoadGrid(GridPath, out Grids))
                return false;

            return true;
        }

        public bool IsGridForSale()
        {
            return GridForSale;
        }

        public void Transfered()
        {
            ForceSpawnNearPlayer = true;
            GridSavePosition = Vector3D.Zero;
            TransferOwnerShipOnLoad = true;
            GridForSale = false;
        }

    }

    public class GridResult
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public List<MyCubeGrid> Grids = new List<MyCubeGrid>();
        public MyCubeGrid BiggestGrid;
        public string GridName;
        public long BiggestOwner;
        public ulong OwnerSteamID;

        private bool IsAdmin = false;
        public GridResult(bool Admin = false)
        {
            IsAdmin = Admin;
        }



        public static Settings Config { get { return Hangar.Config; } }

        public bool GetGrids(Chat Response, MyCharacter character, string GridNameOrEntity = null)
        {
            if (!GridUtilities.FindGridList(GridNameOrEntity, character, out Grids))
            {
                Response.Respond("No grids found. Check your viewing angle or make sure you spelled right!");
                return false;
            }


            Grids.BiggestGrid(out BiggestGrid);
            if (BiggestGrid == null)
            {
                Response.Respond("Grid incompatible!");
                return false;
            }


            if (!IsAdmin && !BiggestGrid.BigOwners.Contains(character.GetPlayerIdentityId()))
            {
                Response.Respond("You are not the owner of the biggest grid!");
                return false;
            }
                

            if (BiggestGrid.BigOwners.Count == 0)
                BiggestOwner = 0;
            else
                BiggestOwner = BiggestGrid.BigOwners[0];

            
            if (!GetOwner(BiggestOwner, out OwnerSteamID))
            {
                Response.Respond("Unable to get owners SteamID");
                return false;
            }


            GridName = BiggestGrid.DisplayName;
            return true;
        }

        public GridStamp GenerateGridStamp()
        {
            GridStamp Stamp = new GridStamp(Grids);
            Stamp.UpdateBiggestGrid(BiggestGrid);
            return Stamp;
        }


        public bool GetOwner(long BiggestOwner, out ulong SteamID)
        {

            SteamID = 0;
            if (MySession.Static.Players.IdentityIsNpc(BiggestOwner))
            {
                Log.Error($"{BiggestOwner} has been identitied as npc!");
                return false;
            }

            SteamID = MySession.Static.Players.TryGetSteamId(BiggestOwner);
            if (SteamID == 0)
            {
                Log.Error($"{BiggestOwner} doesnt have a steamID!");
                return false;
            }
                


            return true;
        }


    }
}
