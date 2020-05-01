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

namespace QuantumHangar
{
    class GridMethods
    {
        public static bool SaveGrid(string path, string filename, bool keepOriginalOwner, bool keepProjection, List<MyObjectBuilder_CubeGrid> objectBuilders)
        {
            Main.Debug("Starting Save!");
            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), filename);
            definition.CubeGrids = objectBuilders.Select(x => (MyObjectBuilder_CubeGrid)x.Clone()).ToArray();

            /* Reset ownership as it will be different on the new server anyway */
            foreach (MyObjectBuilder_CubeGrid cubeGrid in definition.CubeGrids)
            {
                foreach (MyObjectBuilder_CubeBlock cubeBlock in cubeGrid.CubeBlocks)
                {
                    if (!keepOriginalOwner)
                    {
                        cubeBlock.Owner = 0L;
                        cubeBlock.BuiltBy = 0L;
                    }

                    /* Remove Projections if not needed */
                    if (!keepProjection)
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
                IMyEntity entity;

                MyAPIGateway.Entities.TryGetEntityById(grid.EntityId, out entity);



                if (entity != null)
                {
                    entity.Close();
                }


            }

            Main.Debug("SaveGridPath: " + path);

            return MyObjectBuilderSerializer.SerializeXML(path, false, builderDefinition);
        }

        public static bool LoadGrid(string path, Vector3D playerPosition, bool keepOriginalLocation, IMyPlayer player, bool force = false, CommandContext context = null, Main Plugin = null)
        {

            if (MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_Definitions myObjectBuilder_Definitions))
            {
                var shipBlueprints = myObjectBuilder_Definitions.ShipBlueprints;


                if (shipBlueprints == null)
                {

                    Main.Debug("No ShipBlueprints in File '" + path + "'");

                    if (context != null)
                        Chat.Respond("There arent any Grids in your file to import!",context);

                    return false;
                }

                if (!HangarChecks.BlockLimitChecker(context, Plugin, shipBlueprints, player))
                {
                    Main.Debug("Block Limiter Checker Failed");
                    return false;
                }





                if (!Plugin.Config.AutoOrientateToSurface)
                {
                    foreach (var shipBlueprint in shipBlueprints)
                    {

                        if (!LoadShipBlueprint(shipBlueprint, playerPosition, keepOriginalLocation, context, force))
                        {

                            Main.Debug("Error Loading ShipBlueprints from File '" + path + "'");
                            return false;
                        }
                    }

                    return true;
                }
                else
                {


                    List<MyObjectBuilder_CubeGrid> TotalGrids = new List<MyObjectBuilder_CubeGrid>();
                    List<MyObjectBuilder_Cockpit> cockpits = new List<MyObjectBuilder_Cockpit>();
                    Vector3D direction = playerPosition;


                    foreach (var shipBlueprint in shipBlueprints)
                    {
                        TotalGrids.AddRange(shipBlueprint.CubeGrids.ToList());
                        foreach (MyObjectBuilder_CubeGrid grid in shipBlueprint.CubeGrids)
                        {
                            cockpits.AddRange(grid.CubeBlocks.OfType<MyObjectBuilder_Cockpit>().ToList());
                        }
                    }

                    MyObjectBuilder_CubeGrid[] array = TotalGrids.ToArray();
                    if (array.Length == 0)
                    {
                        return false;
                    }


                        Main.Debug("Total Grids to be pasted: " + TotalGrids.Count());


                        if (cockpits.Count > 0)
                        {
                            //Main.Debug("Cockpits found!");
                            foreach (MyObjectBuilder_Cockpit Block in cockpits)
                            {
                                if (Block.IsMainCockpit)
                                {
                                    Main.Debug("Main cockpit found! Attempting to Align!");
                                    direction = new Vector3D(Block.Orientation.x, Block.Orientation.y, Block.Orientation.z);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Main.Debug("No Cockpits");
                        }



                        Vector3D position = playerPosition;

                        float gravityOffset = 0f;
                        float gravityRotation = 0f;

                        Vector3 vector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(position);
                        if (vector == Vector3.Zero)
                        {
                            vector = MyGravityProviderSystem.CalculateArtificialGravityInPoint(position);
                        }
                        Vector3D vector3D;
                        if (vector != Vector3.Zero)
                        {
                            Main.Debug("Attempting to correct!");
                            vector.Normalize();
                            vector3D = -vector;
                            position += vector * gravityOffset;
                            if (direction == Vector3D.Zero)
                            {
                                direction = Vector3D.CalculatePerpendicularVector(vector);
                                if (gravityRotation != 0f)
                                {
                                    MatrixD matrixa = MatrixD.CreateFromAxisAngle(vector3D, gravityRotation);
                                    direction = Vector3D.Transform(direction, matrixa);
                                }
                            }
                        }
                        else if (direction == Vector3D.Zero)
                        {
                            direction = Vector3D.Right;
                            vector3D = Vector3D.Up;
                        }
                        else
                        {
                            vector3D = Vector3D.CalculatePerpendicularVector(-direction);
                        }

                    //Re orientate

                    MatrixD worldMatrix = MatrixD.CreateWorld(position, direction, vector3D);



                    return AlignToPlanetMath(array, TotalGrids,position,direction,vector3D,context);







                    //MyEntities.CreateFromObjectBuilder(grid, true,null,null,null,null,true,true);





                }
            }

            return false;
        }

        private static bool AlignToPlanetMath(MyObjectBuilder_CubeGrid[] array, List<MyObjectBuilder_CubeGrid> TotalGrids, Vector3D position, Vector3D direction, Vector3D vector3D, CommandContext context)
        {

            MatrixD worldMatrix = MatrixD.CreateWorld(position, direction, vector3D);

            int num = 0;
            MatrixD matrix = MatrixD.Identity;

            Parallel.For(0, array.Length, i =>
            {
                array[i] = (MyObjectBuilder_CubeGrid)TotalGrids[i].Clone();
                if (array[i].CubeBlocks.Count > num)
                {
                    num = array[i].CubeBlocks.Count;
                    matrix = (array[i].PositionAndOrientation.HasValue ? array[i].PositionAndOrientation.Value.GetMatrix() : MatrixD.Identity);
                }

            });

            MyEntities.RemapObjectBuilderCollection(array);
            MatrixD matrix2;
            if (true)
            {
                Vector3D value = Vector3D.Zero;
                if (TotalGrids[0].PositionAndOrientation.HasValue)
                {
                    value = TotalGrids[0].PositionAndOrientation.Value.Position;
                }
                matrix2 = MatrixD.CreateWorld(-value, direction, vector3D);
            }
            else
            {
                //matrix2 = MatrixD.CreateWorld(-prefabDefinition.BoundingSphere.Center, Vector3D.Forward, Vector3D.Up);
            }
            //bool ignoreMemoryLimits2 = MyEntities.IgnoreMemoryLimits;
            MyEntities.IgnoreMemoryLimits = true;


            Parallel.For(0, array.Length, j =>
            {
                MatrixD newWorldMatrix;

                    if (array[j].PositionAndOrientation.HasValue)
                    {
                        MatrixD matrix3 = array[j].PositionAndOrientation.Value.GetMatrix() * MatrixD.Invert(matrix);
                        newWorldMatrix = matrix3 * worldMatrix;
                        array[j].PositionAndOrientation = new MyPositionAndOrientation(newWorldMatrix);
                    }
                    else
                    {
                        newWorldMatrix = worldMatrix;
                        array[j].PositionAndOrientation = new MyPositionAndOrientation(worldMatrix);
                    }
                


            });







            /* Where do we want to paste the grids? Lets find out. */
            var pos = FindPastePosition(array, position);
            if (pos == null)
            {

                Log.Warn("No free Space found!");

                if (context != null)
                    Chat.Respond("No free space available!",context);

                return false;
            }

            var newPosition = pos.Value;

            /* Update GridsPosition if that doesnt work get out of here. */
            if (!UpdateGridsPosition(array, newPosition))
            {

                if (context != null)
                    Chat.Respond("The File to be imported does not seem to be compatible with the server!",context);

                return false;
            }


            MyEntities.RemapObjectBuilderCollection(array);

            ParallelSpawner spawner = new ParallelSpawner(array);
            spawner.Start();

            return true;
        }

        private static bool LoadShipBlueprint(MyObjectBuilder_ShipBlueprintDefinition shipBlueprint,
            Vector3D playerPosition, bool keepOriginalLocation, CommandContext context = null, bool force = false)
        {
            Chat chat = new Chat(context);
            var grids = shipBlueprint.CubeGrids;



            if (grids == null || grids.Length == 0)
            {

                Main.Debug("No grids in blueprint!");

                if (context != null)
                    chat.Respond("No grids in blueprint!");

                return false;
            }



            if (!keepOriginalLocation)
            {

                /* Where do we want to paste the grids? Lets find out. */
                var pos = FindPastePosition(grids, playerPosition);
                if (pos == null)
                {

                    Main.Debug("No free Space found!");

                    if (context != null)
                        chat.Respond("No free space available!");

                    return false;
                }

                var newPosition = pos.Value;

                /* Update GridsPosition if that doesnt work get out of here. */
                if (!UpdateGridsPosition(grids, newPosition))
                {

                    if (context != null)
                        chat.Respond("The File to be imported does not seem to be compatible with the server!");

                    return false;
                }

            }
            else if (!force)
            {
                var sphere = FindBoundingSphere(grids);

                var position = grids[0].PositionAndOrientation.Value;

                sphere.Center = position.Position;

                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities);

                foreach (var entity in entities)
                {

                    if (entity is MyCubeGrid)
                    {

                        if (context != null)
                            chat.Respond("There are potentially other grids in the way. If you are certain is free you can set 'force' to true!");

                        return false;
                    }
                }





            }

            /* Remapping to prevent any key problems upon paste. */
            MyEntities.RemapObjectBuilderCollection(grids);


            ParallelSpawner spawner = new ParallelSpawner(grids);

            spawner.Start();


            return true;
        }



        public static List<MyCubeGrid> FindGridList(string gridNameOrEntityId, MyCharacter character, Settings Config)
        {

            List<MyCubeGrid> grids = new List<MyCubeGrid>();

            if (gridNameOrEntityId == null && character == null)
                return new List<MyCubeGrid>();



            if (Config.EnableSubGrids)
            {


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

                        MyCubeGrid grid = node.NodeData;


                        if (Config.AutoDisconnectGearConnectors)
                        {
                            bool ClearEntitiyID = true;
                            foreach (long ID in grid.BigOwners)
                            {
                                if (ID != EntitiyID)
                                {
                                    ClearEntitiyID = false;
                                }
                            }

                            if (!ClearEntitiyID)
                            {
                                continue;
                            }
                        }

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

                        if (grid.Physics == null)
                            continue;

                        grids.Add(grid);
                    }
                }
            }

            return grids;
        }
        private static Vector3D? FindPastePosition(MyObjectBuilder_CubeGrid[] grids, Vector3D playerPosition)
        {

            BoundingSphere sphere = FindBoundingSphere(grids);

            /* 
             * Now we know the radius that can house all grids which will now be 
             * used to determine the perfect place to paste the grids to. 
             */
            return MyEntities.FindFreePlace(playerPosition, sphere.Radius);
        }

        private static BoundingSphereD FindBoundingSphere(MyObjectBuilder_CubeGrid[] grids)
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

        private static bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition)
        {

            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;


            foreach(MyObjectBuilder_CubeGrid grid in grids) {

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

        public static bool BackupSignleGridStatic(string dir, ulong playerId, List<MyCubeGrid> grids,
             HashSet<long> alreadyExportedGrids, bool background = true)
        {
            //Log.Info("A");
            MyCubeGrid biggestGrid = null;

            long blockCount = 0;



            foreach (var grid in grids)
            {

                long count = grid.BlocksCount;

                blockCount += count;

                if (biggestGrid == null || biggestGrid.BlocksCount < count)
                    biggestGrid = grid;
            }

            long entityId = biggestGrid.EntityId;

            if (alreadyExportedGrids != null)
            {

                if (alreadyExportedGrids.Contains(entityId))
                    return false;

                alreadyExportedGrids.Add(entityId);
            }


            //Log.Info("B");
            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in grids)
            {

                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }


            if (background)
            {

                MyAPIGateway.Parallel.StartBackground(() =>
                {
                    BackupGrid(dir, playerId, biggestGrid, entityId, objectBuilders);
                });

            }
            else
            {

                return BackupGrid(dir, playerId, biggestGrid, entityId, objectBuilders);
            }



            return true;
        }

        private static bool BackupGrid(string dir, ulong playerId, MyCubeGrid biggestGrid, long entityId, List<MyObjectBuilder_CubeGrid> objectBuilders)
        {

            try
            {

                string pathForPlayer = CreatePathForPlayer(dir, playerId);
                string gridName = biggestGrid.DisplayName;
                pathForPlayer = Path.Combine(pathForPlayer, gridName + ".sbc");



                Log.Info("SavedDir: " + pathForPlayer);
                bool saved = SaveGrid(pathForPlayer, gridName, true, false, objectBuilders);



                return saved;
            }
            catch (Exception e)
            {
                Main.Debug("Error on Export Grid!", e, Main.ErrorType.Fatal);
                return false;
            }
        }

        public static string CreatePathForPlayer(string path, ulong playerId)
        {

            var folder = Path.Combine(path, playerId.ToString());
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string CreatePathForGrid(string pathForPlayer, string gridName, long entityId)
        {

            foreach (var c in Path.GetInvalidFileNameChars())
                gridName = gridName.Replace(c, '_');

            var folder = Path.Combine(pathForPlayer, gridName + "_" + entityId);
            Directory.CreateDirectory(folder);

            return folder;
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
                Main.Debug("Matrix Error!", e, Main.ErrorType.Trace);
                return null;
            }

        }

    }

    public class HangarChecks
    {
        


        //Hangar Save
        public static bool RequireCurrency(CommandContext Context, Main Plugin, Result result)
        {
            //MyObjectBuilder_Definitions(MyParticlesManager)



            Chat chat = new Chat(Context);
            IMyPlayer Player = Context.Player;
            if (!Plugin.Config.RequireCurrency)
            {
                
                return true;
            }
            else
            {
                long SaveCost = 0;
                switch (Plugin.Config.HangarSaveCostType)
                {
                    case CostType.BlockCount:

                        foreach (MyCubeGrid grid in result.grids)
                        {
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                {
                                    //If grid is station
                                    SaveCost += Convert.ToInt64(grid.BlocksCount * Plugin.Config.CustomStaticGridCurrency);
                                }
                                else
                                {
                                    //If grid is large grid
                                    SaveCost += Convert.ToInt64(grid.BlocksCount * Plugin.Config.CustomLargeGridCurrency);
                                }
                            }
                            else
                            {
                                //if its a small grid
                                SaveCost += Convert.ToInt64(grid.BlocksCount * Plugin.Config.CustomSmallGridCurrency);
                            }


                        }

                        //Multiply by 


                        break;


                    case CostType.Fixed:

                        SaveCost = Convert.ToInt64(Plugin.Config.CustomStaticGridCurrency);

                        break;


                    case CostType.PerGrid:

                        foreach (MyCubeGrid grid in result.grids)
                        {
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                {
                                    //If grid is station
                                    SaveCost += Convert.ToInt64(Plugin.Config.CustomStaticGridCurrency);
                                }
                                else
                                {
                                    //If grid is large grid
                                    SaveCost += Convert.ToInt64(Plugin.Config.CustomLargeGridCurrency);
                                }
                            }
                            else
                            {
                                //if its a small grid
                                SaveCost += Convert.ToInt64(Plugin.Config.CustomSmallGridCurrency);
                            }
                        }

                        break;
                }

                EconUtils.TryGetPlayerBalance(Player.SteamUserId, out long Balance);


                if (Balance >= SaveCost)
                {
                    //Check command status!
                    string command = result.biggestGrid.DisplayName;
                    var confirmationCooldownMap = Plugin.ConfirmationsMap;
                    if (confirmationCooldownMap.TryGetValue(Player.IdentityId, out CurrentCooldown confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Confirmed command! Update player balance!
                            confirmationCooldownMap.Remove(Player.IdentityId);
                            chat.Respond("Confirmed! Saving grid!");

                            Player.RequestChangeBalance(-1 * SaveCost);
                            return true;
                        }
                        else
                        {
                            chat.Respond("Saving this grid in your hangar will cost " + SaveCost + " SC. Run this command again within 30 secs to continue!");
                            confirmationCooldown.StartCooldown(command);
                            return false;
                        }
                    }
                    else
                    {
                        chat.Respond("Saving this grid in your hangar will cost " + SaveCost + " SC. Run this command again within 30 secs to continue!");
                        confirmationCooldown = new CurrentCooldown();
                        confirmationCooldown.StartCooldown(command);
                        confirmationCooldownMap.Add(Player.IdentityId, confirmationCooldown);
                        return false;
                    }
                }
                else
                {
                    long Remaing = SaveCost - Balance;
                    chat.Respond("You need an additional " + Remaing + " SC to perform this action!");
                    return false;
                }
            }
        }

        public static bool CheckHanagarLimits(CommandContext Context, Main Plugin, out PlayerInfo Data)
        {
            IMyPlayer Player = Context.Player;
            string path = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);


            int MaxStorage = Plugin.Config.NormalHangarAmount;
            if (Context.Player.PromoteLevel >= MyPromoteLevel.Scripter)
            {
                MaxStorage = Plugin.Config.ScripterHangarAmount;
            }


            //Get PlayerInfo from file
            try
            {
                Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(path, "PlayerInfo.json")));

                if (Data.Grids.Count >= MaxStorage)
                {
                    Chat.Respond("You have reached your hangar limit!", Context);
                    return false;
                }



                return true;

            }
            catch
            {
                //Main.Debug("HangarLimitCheck Error: ", e, Main.ErrorType.Fatal);
                Data = new PlayerInfo();
                return true;
                //New player. Go ahead and create new. Should not have a timer.
            }
        }

        public static bool ExtensiveLimitChecker(CommandContext Context, Main Plugin, Result result, PlayerInfo Data)
        {
            //Begin Single Slot Save!
            Chat chat = new Chat(Context);

            int TotalBlocks = 0;
            int TotalPCU = 0;
            int StaticGrids = 0;
            int LargeGrids = 0;
            int SmallGrids = 0;
            foreach (MyCubeGrid grid in result.grids)
            {
                TotalBlocks += grid.BlocksCount;
                TotalPCU += grid.BlocksPCU;

                if (grid.GridSizeEnum == MyCubeSize.Large)
                {
                    if (grid.IsStatic)
                    {
                        StaticGrids += 1;
                    }
                    else
                    {
                        LargeGrids += 1;
                    }
                }
                else
                {
                    SmallGrids += 1;
                }

            }

            if (Plugin.Config.SingleMaxBlocks != 0)
            {


                if (TotalBlocks > Plugin.Config.SingleMaxBlocks)
                {
                    int remainder = TotalBlocks - Plugin.Config.SingleMaxBlocks;

                    chat.Respond("Grid is " + remainder + " blocks over the max slot block limit! " + TotalBlocks + "/" + Plugin.Config.SingleMaxBlocks);
                    return false;
                }

            }

            if (Plugin.Config.SingleMaxPCU != 0)
            {
                if (TotalPCU > Plugin.Config.SingleMaxPCU)
                {
                    int remainder = TotalPCU - Plugin.Config.SingleMaxBlocks;

                    chat.Respond("Grid is " + remainder + " PCU over the slot hangar PCU limit! " + TotalPCU + "/" + Plugin.Config.SingleMaxPCU);
                    return false;
                }
            }

            if (Plugin.Config.AllowStaticGrids)
            {
                if (Plugin.Config.SingleMaxLargeGrids != 0 && StaticGrids > Plugin.Config.SingleMaxStaticGrids)
                {
                    int remainder = StaticGrids - Plugin.Config.SingleMaxStaticGrids;

                    chat.Respond("You are " + remainder + " static grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (StaticGrids > 0)
                {
                    chat.Respond("Saving Static Grids is disabled!");
                    return false;
                }
            }

            if (Plugin.Config.AllowLargeGrids)
            {
                if (Plugin.Config.SingleMaxLargeGrids != 0 && LargeGrids > Plugin.Config.SingleMaxLargeGrids)
                {
                    int remainder = LargeGrids - Plugin.Config.SingleMaxLargeGrids;

                    chat.Respond("You are " + remainder + " large grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (LargeGrids > 0)
                {
                    chat.Respond("Saving Large Grids is disabled!");
                    return false;
                }
            }

            if (Plugin.Config.AllowSmallGrids)
            {
                if (Plugin.Config.SingleMaxSmallGrids != 0 && SmallGrids > Plugin.Config.SingleMaxSmallGrids)
                {
                    int remainder = LargeGrids - Plugin.Config.SingleMaxLargeGrids;

                    chat.Respond("You are " + remainder + " small grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (SmallGrids > 0)
                {
                    chat.Respond("Saving Small Grids is disabled!");
                    return false;
                }
            }


            //Hangar total limit!
            foreach (GridStamp Grid in Data.Grids)
            {
                TotalBlocks += Grid.NumberofBlocks;
                TotalPCU += Grid.GridPCU;

                StaticGrids += Grid.StaticGrids;
                LargeGrids += Grid.LargeGrids;
                SmallGrids += Grid.SmallGrids;

            }

            if (Plugin.Config.TotalMaxBlocks != 0 && TotalBlocks > Plugin.Config.TotalMaxBlocks)
            {
                int remainder = TotalBlocks - Plugin.Config.TotalMaxBlocks;

                chat.Respond("Grid is " + remainder + " blocks over the total hangar block limit! " + TotalBlocks + "/" + Plugin.Config.TotalMaxBlocks);
                return false;
            }

            if (Plugin.Config.TotalMaxPCU != 0 && TotalPCU > Plugin.Config.TotalMaxPCU)
            {

                int remainder = TotalPCU - Plugin.Config.TotalMaxPCU;
                chat.Respond("Grid is " + remainder + " PCU over the total hangar PCU limit! " + TotalPCU + "/" + Plugin.Config.TotalMaxPCU);
                return false;
            }


            if (Plugin.Config.TotalMaxStaticGrids != 0 && StaticGrids > Plugin.Config.TotalMaxStaticGrids)
            {
                int remainder = StaticGrids - Plugin.Config.TotalMaxStaticGrids;

                chat.Respond("You are " + remainder + " static grid over the total hangar limit!");
                return false;
            }


            if (Plugin.Config.TotalMaxLargeGrids != 0 && LargeGrids > Plugin.Config.TotalMaxLargeGrids)
            {
                int remainder = LargeGrids - Plugin.Config.TotalMaxLargeGrids;

                chat.Respond("You are " + remainder + " large grid over the total hangar limit!");
                return false;
            }


            if (Plugin.Config.TotalMaxSmallGrids != 0 && SmallGrids > Plugin.Config.TotalMaxSmallGrids)
            {
                int remainder = LargeGrids - Plugin.Config.TotalMaxSmallGrids;

                chat.Respond("You are " + remainder + " small grid over the total hangar limit!");
                return false;
            }




            return true;
        }

        public static bool CheckPlayerTimeStamp(CommandContext Context, Main Plugin, ref PlayerInfo Data)
        {
            //Check timestamp before continuing!
            if (Data == null)
            {
                //New players
                return true;
            }


            if (Data.Timer != null)
            {
                TimeStamp Old = Data.Timer;
                //There is a time limit!
                TimeSpan Subtracted = DateTime.Now.Subtract(Old.OldTime);
                //Log.Info("TimeSpan: " + Subtracted.TotalMinutes);
                if (Subtracted.TotalMinutes <= Plugin.Config.WaitTime)
                {
                    int RemainingTime = (int)Plugin.Config.WaitTime - Convert.ToInt32(Subtracted.TotalMinutes);
                    Chat.Respond("You have " + RemainingTime + " mins before you can perform this action!", Context);
                    return false;
                }
                else
                {
                    Data.Timer = null;
                    return true;
                }
            }

            return true;

        }

        public static bool CheckEnemyDistance(CommandContext Context, Main Plugin)
        {
            IMyPlayer Player = Context.Player;
            var fc = MyAPIGateway.Session.Factions.GetObjectBuilder();
            var faction = fc.Factions.FirstOrDefault(f => f.Members.Any(a => a.PlayerId == Player.IdentityId));

            //Check enemy location! If under limit return!
            foreach (MyPlayer OnlinePlayer in MySession.Static.Players.GetOnlinePlayers())
            {
                if (faction == null || !faction.Members.Any(m => m.PlayerId == OnlinePlayer.Identity.IdentityId))
                {
                    if (Vector3D.Distance(Player.GetPosition(), OnlinePlayer.GetPosition()) == 0)
                    {
                        //Some kinda stupid faction bug
                        continue;
                    }

                    if (Vector3D.Distance(Player.GetPosition(), OnlinePlayer.GetPosition()) <= Plugin.Config.DistanceCheck)
                    {
                        Chat.Respond("Unable to load grid! Enemy within " + Plugin.Config.DistanceCheck + "m!",Context);
                        return false;
                    }
                }
            }

            return true;

        }

        public static bool CheckInGravity(CommandContext Context, Main Plugin)
        {
            IMyPlayer Player = Context.Player;
            var fc = MyAPIGateway.Session.Factions.GetObjectBuilder();
            var faction = fc.Factions.FirstOrDefault(f => f.Members.Any(a => a.PlayerId == Player.IdentityId));

            /*
            //Check enemy location! If under 30km return!
            foreach (MyPlayer OnlinePlayer in MySession.Static.Players.GetOnlinePlayers())
            {
                if (faction == null || !faction.Members.Any(m => m.PlayerId == OnlinePlayer.Identity.IdentityId))
                {
                    if (Vector3D.Distance(Player.GetPosition(), OnlinePlayer.GetPosition()) == 0)
                    {
                        //Some kinda stupid faction bug
                        continue;
                    }

                    if (Vector3D.Distance(Player.GetPosition(), OnlinePlayer.GetPosition()) <= Plugin.Config.DistanceCheck)
                    {
                        Context.Respond("Unable to load grid! Enemy within " + Plugin.Config.DistanceCheck + "m!");
                        return false;
                    }
                }
            }
            */

            return true;

        }

        public static Result GetGrids(CommandContext Context, Main Plugin, MyCharacter character, string GridName = null)
        {
            List<MyCubeGrid> grids = GridMethods.FindGridList(GridName, character, Plugin.Config);
            Chat chat = new Chat(Context);


            Result Return = new Result();
            Return.grids = grids;
            MyCubeGrid biggestGrid = new MyCubeGrid();

            if (grids == null)
            {
                chat.Respond("Multiple grids found. Try to rename them first or try a different subgrid for identification!");
                Return.GetGrids = false;
                return Return;
            }

            if (grids.Count == 0)
            {
                chat.Respond("No grids found. Check your viewing angle or try the correct name!");
                Return.GetGrids = false;
                return Return;
            }


            foreach (var grid in grids)
            {
                if (biggestGrid.BlocksCount < grid.BlocksCount)
                {
                    biggestGrid = grid;
                }
            }


            if (biggestGrid == null)
            {
                chat.Respond("Grid incompatible!");
                Return.GetGrids = false;
                return Return;
            }

            Return.biggestGrid = biggestGrid;

            long playerId;

            if (biggestGrid.BigOwners.Count == 0)
                playerId = 0;
            else
                playerId = biggestGrid.BigOwners[0];


            if (playerId != Context.Player.IdentityId)
            {
                chat.Respond("You are not the owner of this grid!");
                Return.GetGrids = false;
                return Return;
            }

            Return.GetGrids = true;
            return Return;
        }
        public static Result AdminGetGrids(CommandContext Context, Main Plugin, MyCharacter character, string GridName = null)
        {
            List<MyCubeGrid> grids = GridMethods.FindGridList(GridName, character, Plugin.Config);
            Chat chat = new Chat(Context);

            Result Return = new Result();
            Return.grids = grids;
            MyCubeGrid biggestGrid = new MyCubeGrid();

            if (grids == null)
            {
                chat.Respond("Multiple grids found. Try to rename them first or try a different subgrid for identification!");
                Return.GetGrids = false;
                return Return;
            }

            if (grids.Count == 0)
            {
                chat.Respond("No grids found. Check your viewing angle or try the correct name!");
                Return.GetGrids = false;
                return Return;
            }



            foreach (var grid in grids)
            {
                if (biggestGrid == null || biggestGrid.BlocksCount < grid.BlocksCount)
                {
                    biggestGrid = grid;
                }
            }


            if (biggestGrid == null)
            {
                chat.Respond("Grid incompatible!");
                Return.GetGrids = false;
                return Return;
            }

            Return.biggestGrid = biggestGrid;

            long playerId;

            if (biggestGrid.BigOwners.Count == 0)
                playerId = 0;
            else
                playerId = biggestGrid.BigOwners[0];


            //Context.Respond("Preparing " + biggestGrid.DisplayName);
            Return.GetGrids = true;
            return Return;
        }

        public static bool CheckForExistingGrids(PlayerInfo Data, Result result)
        {
            if (Data == null)
            {
                //If Player info is empty, return true. (No data in hangar)
                return true;
            }

            if (Data.Grids.Any(x => x.GridName == result.grids[0].DisplayName))
            {
                return false;
            }
            return true;
        }

        public static bool BeginSave(CommandContext Context, Main Plugin, Result result, PlayerInfo Data)
        {

            IMyPlayer Player = Context.Player;
            string path = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);
            if (GridMethods.BackupSignleGridStatic(Plugin.Config.FolderDirectory, Player.SteamUserId, result.grids, null, true))
            {
                Chat.Respond("Save Complete!",Context);
                TimeStamp stamp = new TimeStamp();
                stamp.OldTime = DateTime.Now;
                stamp.PlayerID = Player.Identity.IdentityId;


                //Load player file and update!




                //Fill out grid info and store in file
                //GridStamp Grid = new GridStamp();

                GetBPDetails(result, Plugin.Config, out GridStamp Grid);



                Data.Grids.Add(Grid);
                Data.Timer = stamp;


                //Overwrite file
                File.WriteAllText(Path.Combine(path, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));



                return true;
            }
            else
            {
                Chat.Respond("Export Failed!",Context);
                return false;
            }

        }

        public static bool GetBPDetails(Result result, Settings Config, out GridStamp Grid)
        {
            //CreateNewGridStamp
            Grid = new GridStamp();






            float DisassembleRatio = 0;
            double EstimatedValue = 0;

            Grid.BlockTypeCount.Add("Reactors", 0);
            Grid.BlockTypeCount.Add("Turrets", 0);
            Grid.BlockTypeCount.Add("StaticGuns", 0);
            Grid.BlockTypeCount.Add("Refineries", 0);
            Grid.BlockTypeCount.Add("Assemblers", 0);

            foreach (MyCubeGrid SingleGrid in result.grids)
            {
                if (SingleGrid.GridSizeEnum == MyCubeSize.Large)
                {
                    if (SingleGrid.IsStatic)
                    {
                        Grid.StaticGrids += 1;
                        EstimatedValue += SingleGrid.BlocksCount * Config.StaticGridMarketMultiplier;
                    }
                    else
                    {
                        Grid.LargeGrids += 1;
                        EstimatedValue += SingleGrid.BlocksCount * Config.LargeGridMarketMultiplier;
                    }
                }
                else
                {
                    Grid.SmallGrids += 1;
                    EstimatedValue += SingleGrid.BlocksCount * Config.SmallGridMarketMultiplier;
                }


                foreach (MyCubeBlock SingleBlock in SingleGrid.GetFatBlocks())
                {
                    var Block = (IMyCubeBlock)SingleBlock;



                    if (Block as IMyLargeTurretBase != null)
                    {
                        Grid.BlockTypeCount["Turrets"] += 1;
                    }
                    if (Block as IMySmallGatlingGun != null)
                    {
                        Grid.BlockTypeCount["Turrets"] += 1;
                    }

                    if (Block as IMyGunBaseUser != null)
                    {
                        Grid.BlockTypeCount["StaticGuns"] += 1;
                    }

                    if (Block as IMyRefinery != null)
                    {
                        Grid.BlockTypeCount["Refineries"] += 1;
                    }
                    if (Block as IMyAssembler != null)
                    {
                        Grid.BlockTypeCount["Assemblers"] += 1;
                    }


                    //Main.Debug("Block:" + Block.BlockDefinition + " ratio: " + Block.BlockDefinition.);
                    DisassembleRatio += SingleBlock.BlockDefinition.DeformationRatio;



                    Grid.NumberofBlocks += 1;


                }

                Grid.BlockTypeCount["Reactors"] += SingleGrid.NumberOfReactors;
                Grid.NumberOfGrids += 1;
                Grid.GridMass += SingleGrid.Mass;
                Grid.GridPCU += SingleGrid.BlocksPCU;
            }

            //Get Total Build Percent
            Grid.GridBuiltPercent = DisassembleRatio / Grid.NumberofBlocks;



            //Get default
            Grid.GridName = result.grids[0].DisplayName;
            Grid.GridID = result.grids[0].EntityId;
            Grid.MarketValue = EstimatedValue;


            //Get faction


            //MyPlayer player = MySession.Static.Players.GetPlayerByName(SelectedGrid.Seller);





            return true;
        }


        public static bool GetPublicOfferBPDetails(MyObjectBuilder_ShipBlueprintDefinition[] definition, out GridStamp Grid)
        {
            //CreateNewGridStamp
            Grid = new GridStamp();


            float DisassembleRatio = 0;

            Grid.BlockTypeCount.Add("Reactors", 0);
            Grid.BlockTypeCount.Add("Turrets", 0);
            Grid.BlockTypeCount.Add("StaticGuns", 0);
            Grid.BlockTypeCount.Add("Refineries", 0);
            Grid.BlockTypeCount.Add("Assemblers", 0);

            foreach (MyObjectBuilder_ShipBlueprintDefinition d in definition)
            {

                foreach (MyObjectBuilder_CubeGrid grid in d.CubeGrids)
                {
                    if (grid.GridSizeEnum == MyCubeSize.Large)
                    {
                        Grid.LargeGrids += 1;
                    }
                    else
                    {
                        Grid.SmallGrids += 1;
                    }

                    foreach (MyObjectBuilder_CubeBlock Block in grid.CubeBlocks)
                    {

                        if (Block as IMyLargeTurretBase != null)
                        {
                            Grid.BlockTypeCount["Turrets"] += 1;
                        }
                        if (Block as IMySmallGatlingGun != null)
                        {
                            Grid.BlockTypeCount["Turrets"] += 1;
                        }

                        if (Block as IMyGunBaseUser != null)
                        {
                            Grid.BlockTypeCount["StaticGuns"] += 1;
                        }

                        if (Block as IMyRefinery != null)
                        {
                            Grid.BlockTypeCount["Refineries"] += 1;
                        }
                        if (Block as IMyAssembler != null)
                        {
                            Grid.BlockTypeCount["Assemblers"] += 1;
                        }


                        //Main.Debug("Block:" + Block.BlockDefinition + " ratio: " + Block.BlockDefinition.);
                        DisassembleRatio += Block.DeformationRatio;



                        Grid.NumberofBlocks += 1;
                    }
                }
            }

            Grid.GridBuiltPercent = DisassembleRatio / Grid.NumberofBlocks;



            //Get faction


            //MyPlayer player = MySession.Static.Players.GetPlayerByName(SelectedGrid.Seller);





            return true;
        }


        public static bool AdminBeginSave(CommandContext Context, Main Plugin, Result result, PlayerInfo Data)
        {
            IMyPlayer Player = Context.Player;
            string path = GridMethods.CreatePathForPlayer(Plugin.Config.FolderDirectory, Player.SteamUserId);
            if (GridMethods.BackupSignleGridStatic(Plugin.Config.FolderDirectory, Player.SteamUserId, result.grids, null, true))
            {
                Chat.Respond("Save Complete!",Context);

                //Load player file and update!

                int MaxPCU = 0;
                foreach (var a in result.grids)
                {
                    MaxPCU = MaxPCU + a.BlocksPCU;
                }


                //Fill out grid info and store in file
                GridStamp Grid = new GridStamp();

                Grid.GridName = result.grids[0].DisplayName;
                Grid.GridID = result.grids[0].EntityId;
                Grid.GridPCU = MaxPCU;
                Grid.MarketValue = result.grids[0].Mass;

                Data.Grids.Add(Grid);


                //Overwrite file
                File.WriteAllText(Path.Combine(path, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
                return true;
            }
            else
            {
                Chat.Respond("Export Failed!",Context);
                return false;
            }

        }

        public static bool CheckGravity(CommandContext Context, Main Plugin) {


            if (!Plugin.Config.AllowInGravity)
            {
                if (!Vector3D.IsZero(Context.Player.GetPosition())){
                    Chat.Respond("Saving & Loading in gravity has been disabled!", Context);
                    return false;
                }
            }

            return true;
        }

        //Hagar Load

        public static bool CheckIfOnMarket(CommandContext Context, GridStamp Grid, Main Plugin, MyIdentity NewPlayer)
        {
            if (!Plugin.Config.GridMarketEnabled)
            {
                //If the grid market was turned off
                return true;
            }

            Chat chat = new Chat(Context);

            if (Grid.GridForSale)
            {
                if (Plugin.Config.RequireRestockFee)
                {
                    double CostAmount = Plugin.Config.RestockAmount;
                    string command = Grid.GridName;

                    var confirmationCooldownMap = Plugin.ConfirmationsMap;

                    if (confirmationCooldownMap.TryGetValue(NewPlayer.IdentityId, out CurrentCooldown confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Remove grid;

                            confirmationCooldownMap.Remove(NewPlayer.IdentityId);

                            //Update Balance etc
                            List<IMyPlayer> Seller = new List<IMyPlayer>();
                            MyAPIGateway.Players.GetPlayers(Seller, x => x.IdentityId == NewPlayer.IdentityId);

                            Seller[0].TryGetBalanceInfo(out long SellerBalance);
                            if (SellerBalance < CostAmount)
                            {
                                long remainder = Convert.ToInt64(CostAmount - SellerBalance);
                                chat.Respond("You need an additional " + remainder + "sc to perform this action!");
                                return false;
                            }

                            chat.Respond("Confirmed! Removing grid from market");

                            Seller[0].RequestChangeBalance(Convert.ToInt64(-1 * CostAmount));

                            try
                            {

                                MarketList Item = Main.GridList.First(x => x.Name == Grid.GridName);

                                //We dont need to remove the item here anymore. (When the server broadcasts, we can remove it there)
                                //Main.GridList.Remove(Item);



                                //We need to send to all to add one item to the list!
                                CrossServerMessage SendMessage = new CrossServerMessage();
                                SendMessage.Type = CrossServer.MessageType.RemoveItem;
                                SendMessage.List.Add(Item);

                                Plugin.MarketServers.Update(SendMessage);
                                Main.Debug("Point4");
                            }
                            catch (Exception e)
                            {
                                Main.Debug("Cannot remove grid from market! Perhaps Grid isnt on the market?", e, Main.ErrorType.Warn);
                            }

                            Grid.GridForSale = false;

                        }
                        else
                        {
                            chat.Respond("This grid is on the market! Removing it will cost " + CostAmount + "sc. Run this command again within 30 secs to continue!");
                            confirmationCooldown.StartCooldown(command);
                            return false;
                        }

                    }
                    else
                    {
                        chat.Respond("This grid is on the market! Removing it will cost " + CostAmount + "sc. Run this command again within 30 secs to continue!");

                        confirmationCooldown = new CurrentCooldown();
                        confirmationCooldown.StartCooldown(command);

                        confirmationCooldownMap.Add(NewPlayer.IdentityId, confirmationCooldown);
                        return false;
                    }

                }
                else
                {
                    try
                    {

                        MarketList Item = Main.GridList.First(x => x.Name == Grid.GridName);

                        //We dont need to remove the item here anymore. (When the server broadcasts, we can remove it there)
                        //Main.GridList.Remove(Item);



                        //We need to send to all to add one item to the list!
                        CrossServerMessage SendMessage = new CrossServerMessage();
                        SendMessage.Type = CrossServer.MessageType.RemoveItem;
                        SendMessage.List.Add(Item);

                        Plugin.MarketServers.Update(SendMessage);
                        Main.Debug("Point4");
                    }
                    catch (Exception e)
                    {
                        Main.Debug("Cannot remove grid from market! Perhaps Grid isnt on the market?", e, Main.ErrorType.Warn);
                    }

                    Grid.GridForSale = false;
                }


            }
            return true;

        }

        public static bool CheckGridLimits(CommandContext Context, MyIdentity NewPlayer, GridStamp Grid)
        {

            MyBlockLimits blockLimits = NewPlayer.BlockLimits;

            MyBlockLimits a = MySession.Static.GlobalBlockLimits;

            if (a.PCU <= 0)
            {
                //PCU Limits on server is 0
                //Skip PCU Checks
                Main.Debug("PCU Server limits is 0!");
                return true;
            }

            //Main.Debug("PCU Limit from Server:"+a.PCU);
            //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
            //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

            int CurrentPcu = blockLimits.PCUBuilt;
            Main.Debug("Current PCU: " + CurrentPcu);

            int MaxPcu = blockLimits.PCU + CurrentPcu;

            int pcu = MaxPcu - CurrentPcu;
            //Main.Debug("MaxPcu: " + pcu);
            Main.Debug("Grid PCU: " + Grid.GridPCU);


            Main.Debug("Current player PCU:" + CurrentPcu);

            //Find the difference
            if (MaxPcu - CurrentPcu <= Grid.GridPCU)
            {
                int Need = Grid.GridPCU - (MaxPcu - CurrentPcu);
                Chat.Respond("PCU limit reached! You need an additional " + Need + " pcu to perform this action!", Context);
                return false;
            }

            return true;
        }


        public static bool BlockLimitChecker(CommandContext Context, Main Plugin, MyObjectBuilder_ShipBlueprintDefinition[] shipblueprints, IMyPlayer Player)
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
            if (!Plugin.Config.EnableBlackListBlocks)
            {
                return true;
            }


            else
            {
                //If we are using built in server block limits..
                if (Plugin.Config.SBlockLimits)
                {
                    //& the server blocklimits is not enabled... Return true
                    if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.NONE)
                    {
                        return true;
                    }


                    //Cycle each grid in the ship blueprints
                    foreach (var shipBlueprint in shipblueprints)
                    {
                        foreach (var CubeGrid in shipBlueprint.CubeGrids)
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
                                    if (Plugin.Config.SBlockLimits)
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
                    }




                    if (MySession.Static.MaxGridSize != 0 && BiggestGrid > MySession.Static.MaxGridSize)
                    {
                        Chat.Respond("Biggest grid is over Max grid size! ", Context);
                        return false;
                    }

                    //Need too loop player identities in dictionary. Do this via seperate function
                    if (PlayerIdentityLoop(Context, BlocksAndOwnerForLimits, FinalBlocksCount, Plugin) == true)
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
                    if(Main.CheckFuture == null)
                    {
                        //BlockLimiter is null!
                        Chat.Respond("Blocklimiter Plugin not installed or Loaded!", Context);
                        Main.Debug("BLimiter plugin not installed or loaded! May require a server restart!");
                        return false;
                    }
                    

                    List<MyObjectBuilder_CubeGrid> grids = new List<MyObjectBuilder_CubeGrid>();
                    foreach (var shipBlueprint in shipblueprints)
                    {
                        foreach (var CubeGrid in shipBlueprint.CubeGrids)
                        {
                            grids.Add(CubeGrid);
                        }
                    }

                    Main.Debug("Grids count: "+grids.Count());
                    object value = Main.CheckFuture.Invoke(null, new object[] { grids.ToArray() , Player.Identity.IdentityId});

                    //Convert to value return type
                    bool ValueReturn = (bool)value;
                    if (!ValueReturn)
                    {
                        //Main.Debug("Cannont load grid in due to BlockLimiter Configs!");
                        return true;
                    }
                    else
                    {
                        Chat.Respond("Grid would be over Server-Blocklimiter limits!",Context);
                        Main.Debug("Cannont load grid in due to BlockLimiter Configs!");
                        return false;
                    }



                }



            }








        }

        public static bool PlayerIdentityLoop(CommandContext Context, Dictionary<long, Dictionary<string, int>> BlocksAndOwnerForLimits, int blocksToBuild, Main Plugin)
        {
            foreach (KeyValuePair<long, Dictionary<string, int>> Player in BlocksAndOwnerForLimits)
            {

                Dictionary<string, int> PlayerBuiltBlocks = Player.Value;
                MyIdentity myIdentity = MySession.Static.Players.TryGetIdentity(Player.Key);

                Chat chat = new Chat(Context);
                if (myIdentity != null)
                {
                    MyBlockLimits blockLimits = myIdentity.BlockLimits;
                    if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.PER_FACTION && MySession.Static.Factions.GetPlayerFaction(myIdentity.IdentityId) == null)
                    {
                        chat.Respond("ServerLimits are set PerFaction. You are not in a faction! Contact an Admin!");
                        return false;
                    }

                    if (blockLimits != null)
                    {


                        if (MySession.Static.MaxBlocksPerPlayer != 0 && blockLimits.BlocksBuilt + blocksToBuild > blockLimits.MaxBlocks)
                        {
                            chat.Respond("Cannot load grid! You would be over your Max Blocks!");
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

                                        chat.Respond("Player " + myIdentity.DisplayName + " would be over their " + ServerBlockLimits.Key + " limits! " + TotalNumberOfBlocks + "/" + ServerLimit);
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
        public static bool LoadGrid(CommandContext Context, Main Plugin, string path, string IDPath, MyIdentity NewPlayer, PlayerInfo Data, GridStamp Grid)
        {
            Chat chat = new Chat(Context);
            Vector3D playerPosition = NewPlayer.Character.PositionComp.GetPosition();
            IMyPlayer Player = Context.Player;

            if (!File.Exists(path))
            {
                chat.Respond("Grid doesnt exist! Contact an admin!");
                return false;
            }


            if (GridMethods.LoadGrid(path, playerPosition, false, Player, true, Context, Plugin))
            {
                
                chat.Respond("Load Complete!");
                Data.Grids.Remove(Grid);
                File.Delete(path);

                TimeStamp stamp = new TimeStamp();
                stamp.OldTime = DateTime.Now;
                stamp.PlayerID = Player.Identity.IdentityId;

                Data.Timer = stamp;

                File.WriteAllText(Path.Combine(IDPath, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
                return true;
            }
            else
            {
               
                //chat.Respond("Load Failed!");
                return false;
            }
        }

        //Begin Market!
        public static bool SellOnMarket(Main Plugin, string IDPath, GridStamp Grid, PlayerInfo Data, IMyPlayer Player, long NumPrice, string Description)
        {
            string path = Path.Combine(IDPath, Grid.GridName + ".sbc");
            if (!File.Exists(path))
            {
                //Context.Respond("Grid doesnt exist! Contact an admin!");
                return false;
            }


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
                    Main.Debug("Player " + Player.DisplayName + " has a bugged faction model! Attempting to fix!");
                    factionBuilder = fc.Factions.First(f => f.Members.Any(m => m.PlayerId == Player.IdentityId));
                    MyObjectBuilder_FactionMember member = factionBuilder.Members.First(x => x.PlayerId == Player.Identity.IdentityId);

                    bool IsFounder;
                    bool IsLeader;

                    IsFounder = member.IsFounder;
                    IsLeader = member.IsLeader;

                    factionBuilder.Members.Remove(member);
                    factionBuilder.Members.Add(member);
                }
                catch (Exception a)
                {
                    Main.Debug("Welp tbh fix failed! Please why no fix. :(", a, Main.ErrorType.Trace);
                }

                //Bugged player!
            }



            MarketList List = new MarketList();
            List.Name = Grid.GridName;
            List.Description = Description;
            List.Seller = Player.DisplayName;
            List.Price = NumPrice;
            List.Steamid = Player.SteamUserId;
            List.MarketValue = Grid.MarketValue;
            List.SellerFaction = Grid.SellerFaction;
            List.GridMass = Grid.GridMass;
            List.SmallGrids = Grid.SmallGrids;
            List.LargeGrids = Grid.LargeGrids;
            List.StaticGrids = Grid.StaticGrids;
            List.NumberofBlocks = Grid.NumberofBlocks;
            List.MaxPowerOutput = Grid.MaxPowerOutput;
            List.GridBuiltPercent = Grid.GridBuiltPercent;
            List.JumpDistance = Grid.JumpDistance;
            List.NumberOfGrids = Grid.NumberOfGrids;
            List.BlockTypeCount = Grid.BlockTypeCount;
            List.PCU = Grid.GridPCU;
            List.GridDefinition = Definition;




            //We need to send to all to add one item to the list!
            CrossServerMessage SendMessage = new CrossServerMessage();
            SendMessage.Type = CrossServer.MessageType.AddItem;
            SendMessage.GridDefinition.Add(GridSell);
            SendMessage.List.Add(List);

            Plugin.MarketServers.Update(SendMessage);



            File.WriteAllText(Path.Combine(IDPath, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
            return true;
        }
    }

    public class HangarScans
    {
        public static bool SellOnMarket(CrossServer MarketServer, string IDPath, GridStamp Grid, PlayerInfo Data, MyIdentity Player, long NumPrice, string Description)
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
                        Main.Debug("Player " + Player.DisplayName + " has a bugged faction model! Attempting to fix!");
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
                        Main.Debug("Welp tbh fix failed! Please why no fix. :(", a, Main.ErrorType.Trace);
                    }

                    //Bugged player!
                }



                MarketList List = new MarketList();
                List.Name = Grid.GridName;
                List.Description = Description;
                List.Seller = "Auction House";
                List.Price = NumPrice;
                List.Steamid = 0;
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




                //We need to send to all to add one item to the list!
                CrossServerMessage SendMessage = new CrossServerMessage();
                SendMessage.Type = CrossServer.MessageType.AddItem;
                SendMessage.GridDefinition.Add(GridSell);
                SendMessage.List.Add(List);


                MarketServer.Update(SendMessage);






            });

            return true;
        }


        public static void AutoSell(CrossServer MarketServer, string HangarDir, int SellMaxDayCount)
        {
            String[] subdirectoryEntries = Directory.GetDirectories(HangarDir);
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
                    if (LastLogin.AddDays(SellMaxDayCount) < DateTime.Now)
                    {
                        Main.Debug("Grids will be auto sold by auction!");
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
                catch (Exception e)
                {

                    Main.Debug("Unable File IO exception!", e, Main.ErrorType.Warn);
                    //File is prob null/missing
                    continue;
                }


                foreach (GridStamp grid in Data.Grids)
                {
                    //
                    if (grid.GridForSale == true)
                    {
                        // Main.Debug("On attempting to autosell grid, the grid is already for sale. Skipping");
                        continue;
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

                    if (!SellOnMarket(MarketServer, subdir, grid, Data, identity, Price, Description))
                    {
                        Main.Debug("Unkown error on grid sell! Grid doesnt exist! (Dont manually delete files!)");
                        return;
                    }
                }


                File.WriteAllText(Path.Combine(subdir, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));






            }
        }
        public static void AutoHangar(Settings Config)
        {
            if (true && MySession.Static.Ready)
            {

                List<MyCubeGrid> ExportedGrids = new List<MyCubeGrid>();


                foreach (MyCubeGrid cubeGrid in MyEntities.GetEntities().OfType<MyCubeGrid>())
                {
                    if (cubeGrid == null || cubeGrid.Physics == null)
                        return;

                    bool HangarGrid = true;

                    Main.Debug("Checking grid: " + cubeGrid.DisplayName);
                    List<long> Owners = cubeGrid.BigOwners;

                    foreach (long player in cubeGrid.BigOwners)
                    {
                        MyIdentity identity;
                        DateTime LastLogin;
                        identity = MySession.Static.Players.GetAllIdentities().First(x => x.IdentityId == player);

                        if (identity == null)
                        {
                            continue;
                        }

                        try
                        {
                            //MyPlayer.PlayerId PlayerID = MySession.Static.Players.GetAllPlayers().First(x => x.SteamId == SteamID);
                            //CurrentPlayer = MySession.Static.Players.GetPlayerById(0);
                            LastLogin = identity.LastLoginTime;
                            Main.Debug(LastLogin.ToString() + " || " + DateTime.Now.ToString());
                            if (LastLogin.AddDays(Config.AutoHangarDayAmount) > DateTime.Now)
                            {
                                HangarGrid = false;
                            }
                        }
                        catch
                        {
                            //Perhaps players was removed? Should we delete thy folder? Nah. WE SHALL SELL
                            continue;
                        }
                    }



                    if (HangarGrid)
                    {
                        if (cubeGrid.IsRespawnGrid && Config.DeleteRespawnPods)
                        {
                            cubeGrid.Close();
                            continue;
                        }


                        Main.Debug("Adding grid to hangar export queue! " + cubeGrid.DisplayName);


                        ExportedGrids.Add(cubeGrid);
                    }
                    //Simple physics check

                }


                foreach (MyCubeGrid cubeGrid in ExportedGrids)
                {
                    Result result = new Result();

                    result.grids.Add(cubeGrid);
                    result.biggestGrid = cubeGrid;
                    result.GetGrids = true;
                    ulong id = 0;
                    try
                    {
                        id = MySession.Static.Players.TryGetSteamId(cubeGrid.BigOwners[0]);
                    }
                    catch
                    {
                        Main.Debug("Grid: " + cubeGrid.DisplayName + "doesnt have an owner! Skipping save!");
                        continue;
                    }


                    Main.Debug("A");
                    if (id == 0)
                    {
                        Main.Debug("A1");
                        //Context.Respond("Unable to find grid owners steamID! Perhaps they were purged from the server?");
                        return;
                    }


                    Main.Debug("B");
                    string path = GridMethods.CreatePathForPlayer(Config.FolderDirectory, id);
                    PlayerInfo Data = new PlayerInfo();
                    string GridName = cubeGrid.DisplayName;
                    //Get PlayerInfo from file
                    try
                    {
                        Data = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(path, "PlayerInfo.json")));


                        if (Data.Grids.Any(x => x.GridName == cubeGrid.DisplayName))
                        {
                            //There is already a grid with that name!
                            bool NameCheckDone = false;
                            int a = 0;
                            while (!NameCheckDone)
                            {
                                a++;
                                if (!Data.Grids.Any(x => x.GridName == cubeGrid.DisplayName + "[" + a + "]"))
                                {
                                    NameCheckDone = true;
                                    break;
                                }

                            }
                            Main.Debug("Saving grid name: " + GridName);
                            GridName = cubeGrid.DisplayName + "[" + a + "]";
                            result.grids[0].DisplayName = GridName;
                            result.biggestGrid.DisplayName = GridName;
                        }
                    }
                    catch
                    {

                        //New player. Go ahead and create new. Should not have a timer.
                    }
                    //Need to check if the name is already a thing.

                    result.biggestGrid.DisplayName = GridName;
                    if (GridMethods.BackupSignleGridStatic(Config.FolderDirectory, id, result.grids, null, true))
                    {
                        //Load player file and update!
                        //Fill out grid info and store in file
                        HangarChecks.GetBPDetails(result, Config, out GridStamp Grid);

                        Grid.GridName = GridName;
                        Data.Grids.Add(Grid);
                        //Deleteall grids
                        cubeGrid.Close();
                        //MyEntities.GetEntityById(cubeGrid.EntityId).Delete();


                        File.WriteAllText(Path.Combine(path, "PlayerInfo.json"), JsonConvert.SerializeObject(Data));
                        Main.Debug("Grid " + result.biggestGrid.DisplayName + " was sent to Hangar due to inactivity!");
                        //Main.Debug("G");
                    }
                    else
                        Main.Debug("Grid " + result.biggestGrid.DisplayName + " FAILED to Hangar due to inactivity!");
                }

            }


        }


    }


    public class EconUtils
    {
        /*This will handle the cross server Econ/Econ tools
         * 
         * WE can change the balance of any player anytime via steamID/playerID and balance.
         * All we have to do is check to see if that player exsists.
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
         * 
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

                long NewBalance = Account.AccountBalance - OriginalBalance;
                MyBankingSystem.ChangeBalance(IdentityID, NewBalance);



                Main.Debug("Player " + IdentityID + " account has been updated! ");

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
                Main.Debug("SteamID: " + steamID);
                long IdentityID = MySession.Static.Players.TryGetIdentityId(steamID);
                Main.Debug("IdentityID: " + IdentityID);
                balance = MyBankingSystem.GetBalance(IdentityID);
                return true;
            }
            catch (Exception e)
            {
                Main.Debug("Unkown keen player error!", e, Main.ErrorType.Fatal);
                balance = 0;
                return false;
            }


        }
    }



    public class Result
    {
        public List<MyCubeGrid> grids = new List<MyCubeGrid>();
        public MyCubeGrid biggestGrid;
        public bool GetGrids;

    }

}
