using NLog.Fluent;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;
using Sandbox.ModAPI;
using System.IO;
using NLog;
using VRage.Groups;
using System.Collections.Concurrent;
using VRage.ModAPI;
using Newtonsoft.Json;
using Sandbox.Definitions;
using System.Threading;
using MyBankingSystem = Sandbox.Game.GameSystems.BankingAndCurrency.MyBankingSystem;
using MyAccountInfo = Sandbox.Game.GameSystems.BankingAndCurrency.MyAccountInfo;
using VRage;
using Sandbox.Game.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.GameSystems;
using Sandbox.Engine.Multiplayer;
using VRage.Network;
using System.ComponentModel;
using System.Collections;
using System.Diagnostics;
using Sandbox.Game.Entities.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
using QuantumHangar.Utilities;
using QuantumHangar;
using System.Text.RegularExpressions;
using System.Globalization;
using Sandbox.Game.Screens.Helpers;

namespace QuantumHangar
{

    public class GridMethods
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        private static bool KeepProjectionsOnSave = false;
        private static bool KeepOriginalOwner = true;

        public string FolderPath;
        private ulong SteamID;
        private HangarChecks HangarChecker;
        private CommandContext Context;
        private Settings Config;
        private Chat chat;


        public GridMethods(ulong UserSteamID, string FolderDirectory, HangarChecks checks = null)
        {
            FolderPath = Path.Combine(FolderDirectory, UserSteamID.ToString());
            Directory.CreateDirectory(FolderPath);
            SteamID = UserSteamID;


            if (checks != null)
            {
                HangarChecker = checks;
                Context = checks.Context;
                Config = checks.Plugin.Config;
                chat = new Chat(Context, checks._Admin);
            }

        }

        private bool SaveGridToFile(string path, string filename, List<MyObjectBuilder_CubeGrid> objectBuilders)
        {

            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), filename);
            definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();

            /* Reset ownership as it will be different on the new server anyway */
            foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids)
            {
                foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks)
                {
                    if (!KeepOriginalOwner)
                    {
                        cubeBlock.Owner = 0L;
                        cubeBlock.BuiltBy = 0L;
                    }

                    /* Remove Projections if not needed */
                    if (!KeepProjectionsOnSave)
                        if (cubeBlock is MyObjectBuilder_ProjectorBase projector)
                            projector.ProjectedGrids = null;

                    /* Remove Pilot and Components (like Characters) from cockpits */
                    if (cubeBlock is MyObjectBuilder_Cockpit cockpit)
                    {
                        cockpit.Pilot = null;

                        if (cockpit.ComponentContainer != null)
                        {
                            var components = cockpit.ComponentContainer.Components;

                            if (components != null)
                            {

                                for (int i = components.Count - 1; i >= 0; i--)
                                {

                                    var component = components[i];

                                    if (component.TypeId == "MyHierarchyComponentBase")
                                    {
                                        components.RemoveAt(i);
                                        continue;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };


            foreach (MyObjectBuilder_CubeGrid grid in definition.CubeGrids)
            {
                if (MyAPIGateway.Entities.TryGetEntityById(grid.EntityId, out IMyEntity entity))
                {
                    entity.Close();
                }
            }


            Log.Warn(path);

            return MyObjectBuilderSerializer.SerializeXML(path, false, builderDefinition);
        }


        public bool LoadGrid(string GridName, MyCharacter Player, long TargetPlayerID, bool keepOriginalLocation, Chat chat, Hangar Plugin, Vector3D GridSaveLocation, bool force = false)
        {
            string path = Path.Combine(FolderPath, GridName + ".sbc");

            if (!File.Exists(path))
            {
                chat.Respond("Grid doesnt exist! Admin should check logs for more information.");
                Log.Fatal("Grid doesnt exsist @" + path);
                return false;
            }

            try
            {
                if (MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_Definitions myObjectBuilder_Definitions))
                {
                    var shipBlueprints = myObjectBuilder_Definitions.ShipBlueprints;


                    if (shipBlueprints == null)
                    {

                        Hangar.Debug("No ShipBlueprints in File '" + path + "'");
                        chat.Respond("There arent any Grids in your file to import!");
                        return false;
                    }

                    if (!HangarChecker.BlockLimitChecker(shipBlueprints))
                    {
                        Hangar.Debug("Block Limiter Checker Failed");
                        return false;
                    }

                    if (Config.OnLoadTransfer)
                    {

                        Log.Warn("Target player: " + TargetPlayerID);

                        //Will transfer pcu to new player
                        foreach (MyObjectBuilder_ShipBlueprintDefinition definition in shipBlueprints)
                        {

                            foreach (MyObjectBuilder_CubeGrid CubeGridDef in definition.CubeGrids)
                            {
                                foreach (MyObjectBuilder_CubeBlock block in CubeGridDef.CubeBlocks)
                                {

                                    block.Owner = TargetPlayerID;
                                    block.BuiltBy = TargetPlayerID;

                                }
                            }
                        }
                    }

                    //If the configs have keep originial position on, we dont want to align this to gravity.
                    if (keepOriginalLocation)
                    {
                        foreach (var shipBlueprint in shipBlueprints)
                        {
                            if (!LoadShipBlueprint(shipBlueprint, GridSaveLocation, true, chat, Plugin))
                            {
                                Hangar.Debug("Error Loading ShipBlueprints from File '" + path + "'");
                                return false;
                            }
                        }

                        File.Delete(path);
                        return true;
                    }
                    else
                    {

                        foreach (var shipBlueprint in shipBlueprints)
                        {
                            var grids = shipBlueprint.CubeGrids;
                            ParallelSpawner Spawner = new ParallelSpawner(grids, chat, true);
                            if (!Spawner.Start(false, Player.PositionComp.GetPosition()))
                            {
                                Hangar.Debug("Error Loading ShipBlueprints from File '" + path + "'");
                                return false;
                            }
                        }

                        File.Delete(path);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                chat.Respond("This ship failed to load. Contact staff & Check logs!");
                Log.Error(ex, "Failed to deserialize grid: " + path + " from file! Is this a shipblueprint?");
            }

            return false;
        }

        private bool LoadShipBlueprint(MyObjectBuilder_ShipBlueprintDefinition shipBlueprint, Vector3D TargetLocation, bool keepOriginalLocation, Chat chat, Hangar Plugin, bool force = false)
        {
            var grids = shipBlueprint.CubeGrids;

            if (grids == null || grids.Length == 0)
            {
                chat.Respond("No grids in blueprint!");
                return false;
            }

            try
            {
                MyIdentity IDentity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(SteamID));

                if (Plugin.GridBackup != null)
                {
                    Plugin.GridBackup.GetType().GetMethod("BackupGridsManuallyWithBuilders", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new Type[2] { typeof(List<MyObjectBuilder_CubeGrid>), typeof(long) }, null).Invoke(Plugin.GridBackup, new object[] { grids.ToList(), IDentity.IdentityId });
                    Log.Warn("Successfully BackedUp grid!");
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e);
            }

            //For loading in the same location
            



            ParallelSpawner Spawner = new ParallelSpawner(grids, chat);
            return Spawner.Start(keepOriginalLocation, TargetLocation);
        }




        public static List<MyCubeGrid> FindGridList(string gridNameOrEntityId, MyCharacter character, Settings Config)
        {

            List<MyCubeGrid> grids = new List<MyCubeGrid>();

            if (gridNameOrEntityId == null && character == null)
                return new List<MyCubeGrid>();



            if (Config.EnableSubGrids)
            {
                //If we include subgrids in the grid grab

                long EntitiyID = character.Entity.EntityId;


                ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups;

                if (gridNameOrEntityId == null)
                    groups = GridFinder.FindLookAtGridGroup(character);
                else
                    groups = GridFinder.FindGridGroup(gridNameOrEntityId);

                if (groups.Count() > 1)
                    return null;


                foreach (var group in groups)
                {
                    foreach (var node in group.Nodes)
                    {
                        MyCubeGrid Grid = node.NodeData;

                        if (Grid.Physics == null)
                            continue;

                        grids.Add(Grid);
                    }
                }



                for (int i = 0; i < grids.Count(); i++)
                {
                    MyCubeGrid grid = grids[i];

                    if (Config.AutoDisconnectGearConnectors && !grid.BigOwners.Contains(character.GetPlayerIdentityId()))
                    {
                        //This disabels enemy grids that have clamped on
                        Action ResetBlocks = new Action(delegate
                        {
                            foreach (MyLandingGear gear in grid.GetFatBlocks<MyLandingGear>())
                            {
                                gear.AutoLock = false;
                                gear.RequestLock(false);
                            }
                        });

                        Task Bool = GameEvents.InvokeActionAsync(ResetBlocks);
                        if (!Bool.Wait(1000))
                            return null;

                    }
                    else if (Config.AutoDisconnectGearConnectors && grid.BigOwners.Contains(character.GetPlayerIdentityId()))
                    {
                        //This will check to see 
                        Action ResetBlocks = new Action(delegate
                        {
                            foreach (MyLandingGear gear in grid.GetFatBlocks<MyLandingGear>())
                            {
                                IMyEntity Entity = gear.GetAttachedEntity();
                                if (Entity == null || Entity.EntityId == 0)
                                {
                                    continue;
                                }


                                //Should prevent entity attachments with voxels
                                if (!(Entity is MyCubeGrid))
                                {
                                    //If grid is attacted to voxel or something
                                    gear.AutoLock = false;
                                    gear.RequestLock(false);

                                    continue;
                                }

                                MyCubeGrid attactedGrid = (MyCubeGrid)Entity;

                                //If the attaced grid is enemy
                                if (!attactedGrid.BigOwners.Contains(character.GetPlayerIdentityId()))
                                {
                                    gear.AutoLock = false;
                                    gear.RequestLock(false);

                                }
                            }
                        });

                        Task Bool = GameEvents.InvokeActionAsync(ResetBlocks);
                        if (!Bool.Wait(1000))
                            return null;
                    }
                }


                for (int i = 0; i < grids.Count(); i++)
                {
                    if (!grids[i].BigOwners.Contains(character.GetPlayerIdentityId()))
                    {
                        grids.RemoveAt(i);
                    }
                }
            }
            else
            {

                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups;

                if (gridNameOrEntityId == null)
                    groups = GridFinder.FindLookAtGridGroupMechanical(character);
                else
                    groups = GridFinder.FindGridGroupMechanical(gridNameOrEntityId);


                if (groups.Count > 1)
                    return null;



                Action ResetBlocks = new Action(delegate
                {
                    foreach (var group in groups)
                    {
                        foreach (var node in group.Nodes)
                        {

                            MyCubeGrid grid = node.NodeData;


                            foreach (MyLandingGear gear in grid.GetFatBlocks<MyLandingGear>())
                            {
                                gear.AutoLock = false;
                                gear.RequestLock(false);
                            }


                            if (grid.Physics == null)
                                continue;

                            grids.Add(grid);
                        }
                    }
                });

                Task Bool = GameEvents.InvokeActionAsync(ResetBlocks);
                if (!Bool.Wait(1000))
                    return null;


            }

            return grids;
        }

        public bool SaveGrids(List<MyCubeGrid> grids, string GridName, Hangar Plugin)
        {




            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in grids)
            {
                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }


            try
            {
                MyIdentity IDentity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(SteamID));

                if (Plugin.GridBackup != null)
                {
                    Plugin.GridBackup.GetType().GetMethod("BackupGridsManuallyWithBuilders", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, null, new Type[2] { typeof(List<MyObjectBuilder_CubeGrid>), typeof(long) }, null).Invoke(Plugin.GridBackup, new object[] { objectBuilders, IDentity.IdentityId });
                    Log.Warn("Successfully BackedUp grid!");
                }
            }
            catch (Exception e)
            {
                Log.Fatal(e);
            }


            try
            {
                //Need To check grid name

                string GridSavePath = Path.Combine(FolderPath, GridName + ".sbc");

                //Log.Info("SavedDir: " + pathForPlayer);
                bool saved = SaveGridToFile(GridSavePath, GridName, objectBuilders);

                if (saved)
                {
                    DisposeGrids(grids);
                }


                return saved;
            }
            catch (Exception e)
            {
                Hangar.Debug("Saving Grid Failed!", e, Hangar.ErrorType.Fatal);
                return false;
            }
        }

        public void DisposeGrids(List<MyCubeGrid> Grids)
        {

            foreach (MyCubeGrid Grid in Grids)
            {
                foreach (MyCockpit Block in Grid.GetBlocks().OfType<MyCockpit>())
                {
                    if (Block.Pilot != null)
                    {
                        Block.RemovePilot();
                    }
                }

                Grid.Close();
            }
        }
        public void SaveInfoFile(PlayerInfo Data)
        {
            FileSaver.Save(Path.Combine(FolderPath, "PlayerInfo.json"), Data);
        }

        public bool LoadInfoFile(out PlayerInfo Data)
        {
            PlayerInfo Info = new PlayerInfo();

            string FilePath = Path.Combine(FolderPath, "PlayerInfo.json");


            if (!File.Exists(FilePath))
            {
                Data = Info;
                return true;
            }


            try
            {
                Info = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Log.Warn(e, "For some reason the file is broken");
                Data = Info;
                return false;
            }


            Data = Info;
            return true;
        }

        public int SyncWithDisk()
        {
            PlayerInfo Data;
            LoadInfoFile(out Data);
            string[] files = Directory.GetFiles(FolderPath, "*.sbc");
            List<GridStamp> gridStamps = new List<GridStamp>(Data.Grids);
            int newGrids = 0;
            foreach (var f in files)
            {
                if (!f.EndsWith(".sbc"))
                {
                    continue;
                }

                string gridName = Path.GetFileNameWithoutExtension(f);
                bool found = false;
                foreach (var stamp in gridStamps)
                {
                    if (stamp.GridName == gridName)
                    {
                        found = true;
                        gridStamps.Remove(stamp);
                        break;
                    }
                }
                if (found)
                    continue;
                var newStamp = new GridStamp();
                newStamp.GridName = gridName;
                newStamp.ForceSpawnNearPlayer = true;
                Data.Grids.Add(newStamp);
                newGrids++;
            }

            // delete stamps for grids no longer present
            foreach (var stamp in gridStamps)
            {
                Data.Grids.Remove(stamp);
            }

            SaveInfoFile(Data);

            Log.Info($"Deleted {gridStamps.Count} that weren't on disk for {SteamID}");
            Log.Info($"Found {newGrids} that weren't registered for {SteamID}");
            return newGrids;
        }
    }



    public class GridFinder
    {
        //Thanks LordTylus, I was too lazy to create my own little utils

        public static ConcurrentBag<List<MyCubeGrid>> FindGridList(long playerId, bool includeConnectedGrids)
        {

            ConcurrentBag<List<MyCubeGrid>> grids = new ConcurrentBag<List<MyCubeGrid>>();

            if (includeConnectedGrids)
            {

                Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group =>
                {

                    List<MyCubeGrid> gridList = new List<MyCubeGrid>();

                    foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                    {

                        MyCubeGrid grid = groupNodes.NodeData;

                        if (grid.Physics == null)
                            continue;

                        gridList.Add(grid);
                    }

                    if (IsPlayerIdCorrect(playerId, gridList))
                        grids.Add(gridList);
                });

            }
            else
            {

                Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group =>
                {

                    List<MyCubeGrid> gridList = new List<MyCubeGrid>();

                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes)
                    {

                        MyCubeGrid grid = groupNodes.NodeData;

                        if (grid.Physics == null)
                            continue;

                        gridList.Add(grid);
                    }

                    if (IsPlayerIdCorrect(playerId, gridList))
                        grids.Add(gridList);
                });
            }

            return grids;
        }

        private static bool IsPlayerIdCorrect(long playerId, List<MyCubeGrid> gridList)
        {

            MyCubeGrid biggestGrid = null;

            foreach (var grid in gridList)
                if (biggestGrid == null || biggestGrid.BlocksCount < grid.BlocksCount)
                    biggestGrid = grid;

            /* No biggest grid should not be possible, unless the gridgroup only had projections -.- just skip it. */
            if (biggestGrid == null)
                return false;

            bool hasOwners = biggestGrid.BigOwners.Count != 0;

            if (!hasOwners)
            {

                if (playerId != 0L)
                    return false;

                return true;
            }

            return playerId == biggestGrid.BigOwners[0];
        }

        public static List<MyCubeGrid> FindGridList(string gridNameOrEntityId, MyCharacter character, bool includeConnectedGrids)
        {


            List<MyCubeGrid> grids = new List<MyCubeGrid>();

            if (gridNameOrEntityId == null && character == null)
                return new List<MyCubeGrid>();

            if (includeConnectedGrids)
            {

                ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups;

                if (gridNameOrEntityId == null)
                    groups = FindLookAtGridGroup(character);
                else
                    groups = FindGridGroup(gridNameOrEntityId);

                if (groups.Count > 1)
                    return null;

                foreach (var group in groups)
                {
                    foreach (var node in group.Nodes)
                    {

                        MyCubeGrid grid = node.NodeData;


                        if (grid.Physics == null)
                            continue;

                        grids.Add(grid);
                    }
                }

            }
            else
            {

                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups;

                if (gridNameOrEntityId == null)
                    groups = FindLookAtGridGroupMechanical(character);
                else
                    groups = FindGridGroupMechanical(gridNameOrEntityId);

                if (groups.Count > 1)
                    return null;

                foreach (var group in groups)
                {
                    foreach (var node in group.Nodes)
                    {

                        MyCubeGrid grid = node.NodeData;

                        if (grid.Physics == null)
                            continue;

                        grids.Add(grid);
                    }
                }
            }

            return grids;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindGridGroup(string gridName)
        {

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();
            Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group =>
            {

                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                {

                    MyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.DisplayName.Equals(gridName) && grid.EntityId + "" != gridName)
                        continue;

                    groups.Add(group);
                }
            });

            return groups;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindLookAtGridGroup(IMyCharacter controlledEntity)
        {

            const float range = 5000;
            Matrix worldMatrix;
            Vector3D startPosition;
            Vector3D endPosition;

            worldMatrix = controlledEntity.GetHeadMatrix(true, true, false); // dead center of player cross hairs, or the direction the player is looking with ALT.
            startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Physical.Groups)
            {

                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                {

                    IMyCubeGrid cubeGrid = groupNodes.NodeData;

                    if (cubeGrid != null)
                    {

                        if (cubeGrid.Physics == null)
                            continue;

                        // check if the ray comes anywhere near the Grid before continuing.    
                        if (ray.Intersects(cubeGrid.WorldAABB).HasValue)
                        {

                            Vector3I? hit = cubeGrid.RayCastBlocks(startPosition, endPosition);

                            if (hit.HasValue)
                            {

                                double distance = (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length();


                                if (list.TryGetValue(group, out double oldDistance))
                                {

                                    if (distance < oldDistance)
                                    {
                                        list.Remove(group);
                                        list.Add(group, distance);
                                    }

                                }
                                else
                                {

                                    list.Add(group, distance);
                                }
                            }
                        }
                    }
                }
            }

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();

            if (list.Count == 0)
                return bag;

            // find the closest Entity.
            var item = list.OrderBy(f => f.Value).First();
            bag.Add(item.Key);

            return bag;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindGridGroupMechanical(string gridName)
        {

            ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();
            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group =>
            {

                foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes)
                {

                    MyCubeGrid grid = groupNodes.NodeData;

                    if (grid.Physics == null)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.DisplayName.Equals(gridName) && grid.EntityId + "" != gridName)
                        continue;

                    groups.Add(group);
                }
            });

            return groups;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindLookAtGridGroupMechanical(IMyCharacter controlledEntity)
        {
            try
            {
                const float range = 5000;
                Matrix worldMatrix;
                Vector3D startPosition;
                Vector3D endPosition;

                worldMatrix = controlledEntity.GetHeadMatrix(true, true, false); // dead center of player cross hairs, or the direction the player is looking with ALT.
                startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
                endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

                var list = new Dictionary<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group, double>();
                var ray = new RayD(startPosition, worldMatrix.Forward);

                foreach (var group in MyCubeGridGroups.Static.Mechanical.Groups)
                {

                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes)
                    {

                        IMyCubeGrid cubeGrid = groupNodes.NodeData;

                        if (cubeGrid != null)
                        {

                            if (cubeGrid.Physics == null)
                                continue;

                            // check if the ray comes anywhere near the Grid before continuing.    
                            if (ray.Intersects(cubeGrid.WorldAABB).HasValue)
                            {

                                Vector3I? hit = cubeGrid.RayCastBlocks(startPosition, endPosition);

                                if (hit.HasValue)
                                {

                                    double distance = (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length();


                                    if (list.TryGetValue(group, out double oldDistance))
                                    {

                                        if (distance < oldDistance)
                                        {
                                            list.Remove(group);
                                            list.Add(group, distance);
                                        }

                                    }
                                    else
                                    {

                                        list.Add(group, distance);
                                    }
                                }
                            }
                        }
                    }
                }

                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();

                if (list.Count == 0)
                    return bag;

                // find the closest Entity.
                var item = list.OrderBy(f => f.Value).First();
                bag.Add(item.Key);

                return bag;
            }
            catch (Exception e)
            {
                //Hangar.Debug("Matrix Error!", e, Hangar.ErrorType.Trace);
                return null;
            }

        }

    }


    public class Utils
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static bool TryUpdatePlayerBalance(PlayerAccount Account)
        {
            try
            {

                long IdentityID = MySession.Static.Players.TryGetIdentityId(Account.SteamID);

                if (IdentityID == 0)
                {
                    return false;
                }


                if (Account.AccountAdjustment)
                {
                    MyBankingSystem.ChangeBalance(IdentityID, Account.AccountBalance);
                    return true;
                }


                long OriginalBalance = MyBankingSystem.GetBalance(IdentityID);
                long BalanceAdjuster = Account.AccountBalance - OriginalBalance;

                if (BalanceAdjuster == 0)
                {
                    return true;
                }

                MyBankingSystem.ChangeBalance(IdentityID, BalanceAdjuster);
                Hangar.Debug("Player " + IdentityID + " account has been updated! ");

                return true;
            }
            catch
            {


                return false;
            }






        }
        public static bool TryGetPlayerBalance(ulong steamID, out long balance)
        {
            try
            {
                //PlayerId Player = new MyPlayer.PlayerId(steamID);
                Hangar.Debug("SteamID: " + steamID);
                long IdentityID = MySession.Static.Players.TryGetIdentityId(steamID);
                Hangar.Debug("IdentityID: " + IdentityID);
                balance = MyBankingSystem.GetBalance(IdentityID);
                return true;
            }
            catch (Exception e)
            {
                Hangar.Debug("Unkown keen player error!", e, Hangar.ErrorType.Fatal);
                balance = 0;
                return false;
            }


        }

        public static bool AdminTryGetPlayerSteamID(string NameOrSteamID, Chat chat, out ulong PSteamID)
        {
            ulong? SteamID;
            if (UInt64.TryParse(NameOrSteamID, out ulong PlayerSteamID))
            {
                MyIdentity Identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(PlayerSteamID, 0));

                if (Identity == null)
                {
                    chat.Respond(NameOrSteamID + " doesnt exsist as an Identity! Dafuq?");
                    PSteamID = 0;
                    return false;
                }

                PSteamID = PlayerSteamID;
                return true;
            }
            else
            {
                try
                {
                    MyIdentity MPlayer;
                    MPlayer = MySession.Static.Players.GetAllIdentities().FirstOrDefault(x => x.DisplayName.Equals(NameOrSteamID));
                    SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);
                }
                catch (Exception e)
                {
                    //Hangar.Debug("Player "+ NameOrID + " dosnt exist on the server!", e, Hangar.ErrorType.Warn);
                    chat.Respond("Player " + NameOrSteamID + " dosnt exist on the server!");
                    PSteamID = 0;
                    return false;
                }
            }

            if (!SteamID.HasValue)
            {
                chat.Respond("Invalid player format! Check logs for more details!");
                Hangar.Debug("Player " + NameOrSteamID + " dosnt exist on the server!");
                PSteamID = 0;
                return false;
            }

            PSteamID = SteamID.Value;
            return true;
        }

        public static readonly string m_ScanPattern = "GPS:([^:]{0,32}):([\\d\\.-]*):([\\d\\.-]*):([\\d\\.-]*):";
        public static Vector3D GetGps(string text)
        {
            int num = 0;
            foreach (Match item in Regex.Matches(text, m_ScanPattern))
            {
                string value = item.Groups[1].Value;
                double value2;
                double value3;
                double value4;
                try
                {
                    value2 = double.Parse(item.Groups[2].Value, CultureInfo.InvariantCulture);
                    value2 = Math.Round(value2, 2);
                    value3 = double.Parse(item.Groups[3].Value, CultureInfo.InvariantCulture);
                    value3 = Math.Round(value3, 2);
                    value4 = double.Parse(item.Groups[4].Value, CultureInfo.InvariantCulture);
                    value4 = Math.Round(value4, 2);
                }
                catch (SystemException)
                {
                    continue;
                }

                return new Vector3D(value2, value3, value4);

            }

            return Vector3D.Zero;
        }

        public static void SendGps(Vector3D Position, string name, long EntityID, double Miniutes = 5)
        {
            MyGps myGps = new MyGps();
            myGps.ShowOnHud = true;
            myGps.Coords = Position;
            myGps.Name = name;
            myGps.Description = "Hangar location for loading grid at or around this position";
            myGps.AlwaysVisible = true;

            MyGps gps = myGps;
            gps.DiscardAt = TimeSpan.FromMinutes(MySession.Static.ElapsedPlayTime.TotalMinutes + Miniutes);
            gps.GPSColor = Color.Yellow;
            MySession.Static.Gpss.SendAddGps(EntityID, ref gps, 0L, true);
        }

        public static void FormatGridName(PlayerInfo Data, Result result)
        {
            try
            {
                string GridName = FileSaver.CheckInvalidCharacters(result.biggestGrid.DisplayName);
                result.GridName = GridName;


                bool Test = Data.Grids.Any(x => x.GridName.Equals(GridName));

                Log.Warn("Running GridName Checks: {" + GridName + "} :" + Test);

                if (Test)
                {
                    //There is already a grid with that name!
                    bool NameCheckDone = false;
                    int a = 1;
                    while (!NameCheckDone)
                    {

                        if (Data.Grids.Any(x => x.GridName.Equals(GridName + "[" + a + "]")))
                        {
                            a++;
                        }
                        else
                        {
                            Hangar.Debug("Name check done! " + a);
                            NameCheckDone = true;
                            break;
                        }

                    }
                    //Main.Debug("Saving grid name: " + GridName);
                    GridName = GridName + "[" + a + "]";
                    result.grids[0].DisplayName = GridName;
                    result.biggestGrid.DisplayName = GridName;
                    result.GridName = GridName;
                }
            }
            catch (Exception e)
            {
                Log.Warn("eeerror", e);
            }
        }

    }
}







