using Newtonsoft.Json;
using NLog;
using QuantumHangar.Serialization;
using QuantumHangar.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using Torch.Mod.Messages;
using Torch.Mod;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using static QuantumHangar.Utils.CharacterUtilities;

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
        PurchasedGrid
    }


    public class FileSaver
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        //A regex invalidCharCollection
        private static Regex _invalidNameScanner = new Regex(string.Format("[{0}]",
            Regex.Escape(new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars()))));

        public static async Task SaveAsync(string dir, object data)
        {
            //All methods calling this should still actually be in another thread... So we dont need to call it again.
            await WriteAsync(dir, data);
        }


        public static async Task WriteAsync(string dir, object data)
        {
            try
            {
                using (var sw = new StreamWriter(dir))
                {
                    await sw.WriteAsync(JsonConvert.SerializeObject(data, Formatting.Indented));
                }

                Log.Info("Done saving!");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
            }
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
            return _invalidNameScanner.Replace(filename, "");
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
        public ulong SteamId { get; set; }
    }


    public class TimeStamp
    {
        public DateTime OldTime;
    }

    public class GridStamp
    {
        public long GridId;
        public string GridName;
        public int GridPcu;
        public int ServerPort = 0;
        public bool GridForSale = false;
        public double MarketValue = 0;
        public Dictionary<long, int> ShipPcu = new Dictionary<long, int>();
        public bool ForceSpawnNearPlayer = false;
        public bool TransferOwnerShipOnLoad = false;


        public string SellerFaction = "N/A";
        public float GridMass = 0;
        public int StaticGrids = 0;
        public int SmallGrids = 0;
        public int LargeGrids = 0;
        public int NumberOfBlocks = 0;
        public float MaxPowerOutput = 0;
        public float GridBuiltPercent = 0;
        public long JumpDistance = 0;
        public int NumberOfGrids = 0;
        public Vector3D GridSavePosition = new Vector3D(0, 0, 0);


        public MyOrientedBoundingBoxD BoundingBox = new MyOrientedBoundingBoxD();
        public BoundingBoxD Box = new BoundingBoxD();
        public Vector3D MatrixTranslation = new Vector3D();


        //Server blocklimits Block
        public Dictionary<string, int> BlockTypeCount = new Dictionary<string, int>();


        //Grid Stored Materials
        public Dictionary<string, double> StoredMaterials = new Dictionary<string, double>();


        [JsonIgnore] private static Settings Config => Hangar.Config;

        [JsonIgnore] public string OriginalGridPath { get; }

        public GridStamp(List<MyCubeGrid> grids)
        {
            float disassembleRatio = 0;
            double estimatedValue = 0;

            BlockTypeCount.Add("Reactors", 0);
            BlockTypeCount.Add("Turrets", 0);
            BlockTypeCount.Add("StaticGuns", 0);
            BlockTypeCount.Add("Refineries", 0);
            BlockTypeCount.Add("Assemblers", 0);

            foreach (var singleGrid in grids)
            {
                if (singleGrid.GridSizeEnum == MyCubeSize.Large)
                {
                    if (singleGrid.IsStatic)
                    {
                        StaticGrids += 1;
                        estimatedValue += singleGrid.BlocksCount * Config.StaticGridMarketMultiplier;
                    }
                    else
                    {
                        LargeGrids += 1;
                        estimatedValue += singleGrid.BlocksCount * Config.LargeGridMarketMultiplier;
                    }
                }
                else
                {
                    SmallGrids += 1;
                    estimatedValue += singleGrid.BlocksCount * Config.SmallGridMarketMultiplier;
                }


                foreach (var singleBlock in singleGrid.GetFatBlocks())
                {
                    var block = (IMyCubeBlock)singleBlock;


                    if (singleBlock.BuiltBy != 0)
                        UpdatePcuCounter(singleBlock.BuiltBy, singleBlock.BlockDefinition.PCU);

                    switch (block)
                    {
                        case IMyLargeTurretBase _:
                        case IMySmallGatlingGun _:
                            BlockTypeCount["Turrets"] += 1;
                            break;
                    }

                    switch (block)
                    {
                        case IMyGunBaseUser _:
                            BlockTypeCount["StaticGuns"] += 1;
                            break;
                        case IMyRefinery _:
                            BlockTypeCount["Refineries"] += 1;
                            break;
                        case IMyAssembler _:
                            BlockTypeCount["Assemblers"] += 1;
                            break;
                    }


                    //Main.Debug("Block:" + Block.BlockDefinition + " ratio: " + Block.BlockDefinition.);
                    disassembleRatio += singleBlock.BlockDefinition.DeformationRatio;
                    NumberOfBlocks += 1;
                }

                BlockTypeCount["Reactors"] += singleGrid.NumberOfReactors;
                NumberOfGrids += 1;
                GridMass += singleGrid.Mass;
                GridPcu += singleGrid.BlocksPCU;
            }

            if (grids[0].BigOwners.Count > 0)
                grids[0].GridSystems.JumpSystem.GetMaxJumpDistance(grids[0].BigOwners[0]);

            //Get Total Build Percent
            GridBuiltPercent = disassembleRatio / NumberOfBlocks;
            MarketValue = estimatedValue;
        }

        public GridStamp()
        {
        }

        public GridStamp(string file)
        {
            OriginalGridPath = file;
            GridName = Path.GetFileNameWithoutExtension(file);
            ForceSpawnNearPlayer = true;
            GridSavePosition = Vector3D.Zero;
            TransferOwnerShipOnLoad = true;
        }


        public void UpdateBiggestGrid(MyCubeGrid biggestGrid)
        {
            GridName = biggestGrid.DisplayName;

            GridId = biggestGrid.EntityId;
            GridSavePosition = biggestGrid.PositionComp.GetPosition();
        }

        public bool CheckGridLimits(Chat response, MyIdentity targetIdentity)
        {
            //No need to check limits
            if (Config.OnLoadTransfer)
                return true;

            if (ShipPcu.Count == 0)
            {
                var blockLimits = targetIdentity.BlockLimits;

                var a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Hangar.Debug("PCU Server limits is 0!");
                    return true;

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                var currentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                var maxPcu = blockLimits.PCU + currentPcu;

                var pcu = maxPcu - currentPcu;

                //Find the difference
                if (maxPcu - currentPcu > GridPcu) return true;
                var need = GridPcu - (maxPcu - currentPcu);
                response.Respond("PCU limit reached! You need an additional " + need +
                                 " pcu to perform this action!");
                return false;

            }


            foreach (var player in ShipPcu)
            {
                var identity = MySession.Static.Players.TryGetIdentity(player.Key);
                if (identity == null) continue;


                var blockLimits = identity.BlockLimits;
                var a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Hangar.Debug("PCU Server limits is 0!");
                    continue;

                var currentPcu = blockLimits.PCUBuilt;
                var maxPcu = blockLimits.PCU + currentPcu;
                var pcu = maxPcu - currentPcu;

                //Find the difference
                if (maxPcu - currentPcu > player.Value) continue;
                var need = player.Value - (maxPcu - currentPcu);
                response.Respond("PCU limit reached! " + identity.DisplayName + " needs an additional " + need +
                                 " PCU to load this grid!");
                return false;
            }

            return true;
        }

        public void UpdatePcuCounter(long player, int amount)
        {
            if (ShipPcu.ContainsKey(player))
                ShipPcu[player] += amount;
            else
                ShipPcu.Add(player, amount);
        }

        public string GetGridPath(string playersFolderPath)
        {
            return Path.Combine(playersFolderPath, GridName + ".sbc");
        }


        public bool TryGetGrids(string playersFolderPath, out IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            grids = null;
            var gridPath = Path.Combine(playersFolderPath, GridName + ".sbc");

            return GridSerializer.LoadGrid(gridPath, out grids);
        }

        public bool IsGridForSale()
        {
            return GridForSale;
        }

        public void Transferred()
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
        public ulong OwnerSteamId;

        private readonly bool _isAdmin;


        /* Following are types that dont have an ownership tied with them */
        public static List<MyObjectBuilderType> NoOwnerTypes = new List<MyObjectBuilderType>()
        {
            MyObjectBuilderType.Parse("MyObjectBuilder_CubeBlock"),
            MyObjectBuilderType.Parse("MyObjectBuilder_PistonTop"),
            MyObjectBuilderType.Parse("MyObjectBuilder_MotorSuspension"),
            MyObjectBuilderType.Parse("MyObjectBuilder_Conveyor"),
            MyObjectBuilderType.Parse("MyObjectBuilder_ReflectorLight"),
            MyObjectBuilderType.Parse("MyObjectBuilder_Thrust"),
            MyObjectBuilderType.Parse("MyObjectBuilder_ConveyorConnector"),
            MyObjectBuilderType.Parse("MyObjectBuilder_InteriorLight"),
            MyObjectBuilderType.Parse("MyObjectBuilder_Passage"),
            MyObjectBuilderType.Parse("MyObjectBuilder_ExhaustBlock")
        };


        public GridResult(bool admin = false)
        {
            _isAdmin = admin;
        }


        public static Settings Config => Hangar.Config;


        public bool GetGrids(Chat response, MyCharacter character, string gridNameOrEntity = null)
        {
            if (!GridUtilities.FindGridList(gridNameOrEntity, character, out Grids))
            {
                response.Respond("No grids found. Check your viewing angle or make sure you spelled right!");
                return false;
            }


            Grids.BiggestGrid(out BiggestGrid);
            if (BiggestGrid == null)
            {
                response.Respond("Grid incompatible!");
                return false;
            }


            if (!_isAdmin)
            {
                var fatBlocks = BiggestGrid.GetFatBlocks().ToList();
                var ownerId = character.GetPlayerIdentityId();

                var totalFatBlocks = 0;
                var ownedFatBlocks = 0;


                foreach (var fat in fatBlocks.Where(fat => fat.IsFunctional && fat.IDModule != null))
                {
                    //WTF happened here?
                    //if (fat.OwnerId == 0)
                    //   Log.Error($"WTF: {fat.BlockDefinition.Id} - {fat.GetType()} - {fat.OwnerId}");


                    totalFatBlocks++;

                    if (fat.OwnerId == ownerId)
                        ownedFatBlocks++;
                }


                var percent = Math.Round((double)ownedFatBlocks / totalFatBlocks * 100, 3);
                var totalBlocksLeftNeeded = totalFatBlocks / 2 + 1 - ownedFatBlocks;

                if (percent <= 50)
                {
                    response.Respond(
                        $"You own {percent}% of the biggest grid! Need {totalBlocksLeftNeeded} more blocks to be the majority owner!");
                    return false;
                }
            }
            else
            {
                //Compute biggest owner
            }

            BiggestOwner = BiggestGrid.BigOwners.Count == 0 ? 0 : BiggestGrid.BigOwners[0];

            if (!GetOwner(BiggestOwner, out OwnerSteamId))
            {
                response.Respond("Unable to get owners SteamID! Are you an NPC?");
                return false;
            }


            GridName = BiggestGrid.DisplayName;
            return true;
        }

        public GridStamp GenerateGridStamp()
        {
            var stamp = new GridStamp(Grids);
            stamp.UpdateBiggestGrid(BiggestGrid);
            GetBoundingBox(Grids, BiggestGrid, OwnerSteamId, out MyOrientedBoundingBoxD obb, out MatrixD matrix, out BoundingBox myAaabb);
            
            //Save box and matrix. Saves time later on
            stamp.BoundingBox = obb;
            stamp.MatrixTranslation = matrix.Translation;
            stamp.Box = myAaabb;

            return stamp;
        }

        private static void GetBoundingBox(List<MyCubeGrid> Grids, MyCubeGrid BiggestGrid, ulong steamid, out MyOrientedBoundingBoxD OBB, out MatrixD Matrix, out BoundingBox myAabb)
        {
            Matrix = BiggestGrid.WorldMatrix;

            myAabb = BiggestGrid.PositionComp.LocalAABB;
            MatrixD orientatedMatrix = BiggestGrid.PositionComp.WorldMatrixNormalizedInv;

            foreach (var grid in Grids)
            {
                if (grid == BiggestGrid)
                    continue;

                Quaternion localQuat = Quaternion.CreateFromRotationMatrix(grid.WorldMatrix);
                MyOrientedBoundingBox localOBB = new MyOrientedBoundingBox(grid.PositionComp.WorldAABB.Center, grid.PositionComp.LocalAABB.HalfExtents, localQuat);
                localOBB.Transform(orientatedMatrix);
                myAabb.Include(localOBB.GetAABB());
            }


            DrawDebug sphere = new DrawDebug("Hangar2");
            Color color = new Color(255, 0, 0, 10);
            sphere.addOBB(myAabb, Matrix.Translation, Matrix.Forward, Matrix.Up, color, MySimpleObjectRasterizer.Wireframe, 1f, 0.005f);

            OBB = new MyOrientedBoundingBoxD(myAabb, Matrix);
        }

        public bool GetOwner(long biggestOwner, out ulong steamId)
        {
            steamId = 0;
            if (MySession.Static.Players.IdentityIsNpc(biggestOwner))
            {
                Log.Error($"{biggestOwner} has been identitied as npc!");
                return false;
            }

            steamId = MySession.Static.Players.TryGetSteamId(biggestOwner);
            if (steamId != 0) return true;
            Log.Error($"{biggestOwner} doesnt have a steamID!");
            return false;


        }
    }
}