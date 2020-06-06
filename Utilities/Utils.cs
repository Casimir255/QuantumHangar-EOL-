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

            //Main.Debug("SaveGridPath: " + path);

            return MyObjectBuilderSerializer.SerializeXML(path, false, builderDefinition);
        }

        public bool LoadGrid(string GridName, MyCharacter Player, bool keepOriginalLocation, Chat chat, bool force = false)
        {
            string path = Path.Combine(FolderPath, GridName+ ".sbc");

            if (!File.Exists(path))
            {
                chat.Respond("Grid doesnt exist! Admin should check logs for more information.");
                //


                return false;
            }


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
                    long PlayerID = Player.GetPlayerIdentityId();
                    //Will transfer pcu to new player
                    foreach (MyObjectBuilder_ShipBlueprintDefinition definition in shipBlueprints)
                    {

                        foreach (MyObjectBuilder_CubeGrid CubeGridDef in definition.CubeGrids)
                        {
                            foreach (MyObjectBuilder_CubeBlock block in CubeGridDef.CubeBlocks)
                            {

                                block.Owner = PlayerID;
                                block.BuiltBy = PlayerID;
                                
                            }
                        }
                    }
                }



                
 




                if (keepOriginalLocation || !Config.AutoOrientateToSurface)
                {
                    foreach (var shipBlueprint in shipBlueprints)
                    {

                        if (!LoadShipBlueprint(shipBlueprint, Player.PositionComp.GetPosition(), true, chat))
                        {

                            Hangar.Debug("Error Loading ShipBlueprints from File '" + path + "'");
                            return false;
                        }
                    }

                    //File.Delete(path);
                    return true;
                }
                else
                {
                    Hangar.Debug("Attempting to align grid to gravity!");
                    AlignToGravity GravityAligner = new AlignToGravity(shipBlueprints, Player.PositionComp.GetPosition(), chat);


                    if (GravityAligner.Start())
                    {
                        //File.Delete(path);
                        return true;
                    }
                    
                }
            }

            return false;
        }

        private bool LoadShipBlueprint(MyObjectBuilder_ShipBlueprintDefinition shipBlueprint,
            Vector3D playerPosition, bool keepOriginalLocation, Chat chat, bool force = false)
        {
            
            var grids = shipBlueprint.CubeGrids;

            if (grids == null || grids.Length == 0)
            {

                Hangar.Debug("No grids in blueprint!");
                chat.Respond("No grids in blueprint!");

                return false;
            }


            bool LoadNearPosition = false;
            //For loading in the same location

            ParallelSpawner Spawner = new ParallelSpawner(grids);
            var position = grids[0].PositionAndOrientation.Value;
            if (keepOriginalLocation)
            {
                var sphere = FindBoundingSphere(grids);

                

                sphere.Center = position.Position;

                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities);

                foreach (var entity in entities)
                {
                    if (entity is MyCubeGrid)
                    {
                            chat.Respond("There are potentially other grids in the way. Loading near the original point.");

                        LoadNearPosition = true;
                    }
                }

                if (!LoadNearPosition)
                {
                    /* Remapping to prevent any key problems upon paste. */
                    MyEntities.RemapObjectBuilderCollection(grids);

                    Spawner.Start();

                    return true;
                }
            }



            /*
             *  Everything else is loading for near player
             * 
             * 
             * 
             */

            

                /* Where do we want to paste the grids? Lets find out. */
                var pos = FindPastePosition(grids, position.Position);
                if (pos == null)
                {

                    Hangar.Debug("No free Space found!");
                    chat.Respond("No free space available!");

                    return false;
                }

                var newPosition = pos.Value;

                /* Update GridsPosition if that doesnt work get out of here. */
                if (!UpdateGridsPosition(grids, newPosition))
                {
                        chat.Respond("The File to be imported does not seem to be compatible with the server!");

                    return false;
                }

                
            MyEntities.RemapObjectBuilderCollection(grids);
            Spawner.Start();
            return true;
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
                        foreach (MyLandingGear gear in grid.GetFatBlocks<MyLandingGear>())
                        {
                            gear.AutoLock = false;
                            gear.RequestLock(false);
                        }

                    }
                    else if (Config.AutoDisconnectGearConnectors && grid.BigOwners.Contains(character.GetPlayerIdentityId()))
                    {
                        //This will check to see 
                        foreach (MyLandingGear gear in grid.GetFatBlocks<MyLandingGear>())
                        {
                            IMyEntity Grid = gear.GetAttachedEntity();
                            if (Grid == null || Grid.EntityId == 0)
                            {
                                continue;
                            }

                            MyCubeGrid attactedGrid = (MyCubeGrid)Grid;
                            if ((MyCubeGrid)attactedGrid == null || attactedGrid.EntityId == 0)
                            {
                                //If grid is attacted to voxel or something
                                gear.AutoLock = false;
                                gear.RequestLock(false);

                                continue;
                            }


                            //If the attaced grid is enemy
                            if (!attactedGrid.BigOwners.Contains(character.GetPlayerIdentityId()))
                            {
                                gear.AutoLock = false;
                                gear.RequestLock(false);

                            }
                        }
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
            }

            return grids;
        }
        private Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids, Vector3D playerPosition)
        {

            BoundingSphere sphere = FindBoundingSphere(grids);

            /* 
             * Now we know the radius that can house all grids which will now be 
             * used to determine the perfect place to paste the grids to. 
             */
            return MyEntities.FindFreePlace(playerPosition, sphere.Radius);
        }

        private BoundingSphereD FindBoundingSphere(MyObjectBuilder_CubeGrid[] grids)
        {

            Vector3? vector = null;
            float radius = 0F;

            foreach (var grid in grids)
            {

                var gridSphere = grid.CalculateBoundingSphere();

                /* If this is the first run, we use the center of that grid, and its radius as it is */
                if (vector == null)
                {

                    vector = gridSphere.Center;
                    radius = gridSphere.Radius;
                    continue;
                }


                /* 
                 * If its not the first run, we use the vector we already have and 
                 * figure out how far it is away from the center of the subgrids sphere. 
                 */
                float distance = Vector3.Distance(vector.Value, gridSphere.Center);

                /* 
                 * Now we figure out how big our new radius must be to house both grids
                 * so the distance between the center points + the radius of our subgrid.
                 */
                float newRadius = distance + gridSphere.Radius;

                /*
                 * If the new radius is bigger than our old one we use that, otherwise the subgrid 
                 * is contained in the other grid and therefore no need to make it bigger. 
                 */
                if (newRadius > radius)
                    radius = newRadius;
            }

            return new BoundingSphereD(vector.Value, radius);
        }

        private bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition)
        {

            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;


            foreach (MyObjectBuilder_CubeGrid grid in grids)
            {

                var position = grid.PositionAndOrientation;

                var realPosition = position.Value;

                var currentPosition = realPosition.Position;

                if (firstGrid)
                {
                    deltaX = newPosition.X - currentPosition.X;
                    deltaY = newPosition.Y - currentPosition.Y;
                    deltaZ = newPosition.Z - currentPosition.Z;

                    currentPosition.X = newPosition.X;
                    currentPosition.Y = newPosition.Y;
                    currentPosition.Z = newPosition.Z;

                    firstGrid = false;

                }
                else
                {

                    currentPosition.X += deltaX;
                    currentPosition.Y += deltaY;
                    currentPosition.Z += deltaZ;
                }

                realPosition.Position = currentPosition;
                grid.PositionAndOrientation = realPosition;


            }


            return true;
        }

        public bool SaveGrids(List<MyCubeGrid> grids)
        {

            MyCubeGrid biggestGrid = null;


            foreach (MyCubeGrid grid in grids)
            {
                int count = grid.BlocksCount;

                if (biggestGrid == null || biggestGrid.BlocksCount < count)
                    biggestGrid = grid;
            }



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
                string gridName = biggestGrid.DisplayName;

                //Need To check grid name
                gridName = FileSaver.CheckInvalidCharacters(gridName);
                string GridSavePath = Path.Combine(FolderPath, gridName + ".sbc");

                //Log.Info("SavedDir: " + pathForPlayer);
                bool saved = SaveGridToFile(GridSavePath, gridName, objectBuilders);

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
            foreach(MyCubeGrid Grid in Grids)
            {
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


            if(!File.Exists(FilePath))
            {
                Data = Info;
                return true;
            }


            try
            {
                Info = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(FilePath));
            }
            catch(Exception e)
            {
                Log.Warn(e,"For some reason the file is broken");
                Data = Info;
                return false;
            }
            

            Data = Info;
            return true;
        }


    }



    public class GridFinder
    {

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



    public class Utilis
    {
        /*This will handle the cross server Econ/Econ tools
         * 
         * WE can change the balance of any player anytime via steamID/playerID and balance.
         * All we have to do is check to see if that player exsists.
         * 
         */

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
            myGps.Description = "This is where you must be to load your grid.";
            myGps.AlwaysVisible = true;

            MyGps gps = myGps;
            gps.DiscardAt = TimeSpan.FromMinutes(MySession.Static.ElapsedPlayTime.TotalMinutes + Miniutes);
            gps.GPSColor = Color.Yellow;
            MySession.Static.Gpss.SendAddGps(EntityID, ref gps, 0L, true);
        }


    }
}







