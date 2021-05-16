using NLog;
using QuantumHangar.HangarChecks;
using QuantumHangar.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using SpaceEngineers.Game.Entities.Blocks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace QuantumHangar.Utils
{
    public class GridUtilities
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static bool KeepProjectionsOnSave = false;
        private static bool KeepOriginalOwner = true;

        private static Settings Config { get {return Hangar.Config;} }



        private readonly string FolderPath;
        private readonly ulong SteamID;
        private readonly Chat Chat;

        public GridUtilities(Chat Chat, ulong UserSteamID)
        {
            FolderPath = Path.Combine(Config.FolderDirectory, UserSteamID.ToString());
            Directory.CreateDirectory(FolderPath);
            SteamID = UserSteamID;
            this.Chat = Chat;
        }


        private static bool SaveGridToFile(string Path, string Name, IEnumerable<MyObjectBuilder_CubeGrid> GridBuilders)
        {

            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), Name);
            definition.CubeGrids = GridBuilders.ToArray();
            PrepareGridForSave(definition);

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };

            return MyObjectBuilderSerializer.SerializeXML(Path, false, builderDefinition);
        }


        public bool LoadGrid(string GridName, MyCharacter Player, long TargetPlayerID, bool keepOriginalLocation, Chat chat, Hangar Plugin, Vector3D GridSaveLocation, bool force = false, bool Admin = false)
        {
            string path = Path.Combine(FolderPath, GridName + ".sbc");

            if (!File.Exists(path))
            {
                Chat?.Respond("Grid doesnt exist! Admin should check logs for more information.");
                Log.Fatal("Grid doesnt exsist @" + path);
                return false;
            }

            try
            {
                if (MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_Definitions myObjectBuilder_Definitions))
                {
                    MyObjectBuilder_ShipBlueprintDefinition[] shipBlueprints = myObjectBuilder_Definitions.ShipBlueprints;
                    if (shipBlueprints == null)
                    {

                        Log.Warn("No ShipBlueprints in File '" + path + "'");
                        Chat?.Respond("There arent any Grids in your file to import!");
                        return false;
                    }


                    /*
                    if (!HangarChecker.BlockLimitChecker(shipBlueprints))
                    {
                        Log.Warn("Block Limiter Checker Failed");
                        return false;
                    }
                    */

                    if (Config.OnLoadTransfer)
                    {
                        Log.Warn("Transfering grid to: " + TargetPlayerID);
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


                    Vector3D PlayerPosition = Player?.PositionComp.GetPosition() ?? Vector3D.Zero;
                    

                    foreach (var shipBlueprint in shipBlueprints)
                    {
                        if (!LoadShipBlueprint(shipBlueprint, GridSaveLocation, PlayerPosition, keepOriginalLocation, chat))
                        {
                            //Hangar.Debug("Error Loading ShipBlueprints from File '" + path + "'");
                            return false;
                        }
                    }

                    File.Delete(path);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Chat?.Respond("This ship failed to load. Contact staff & Check logs!");
                Log.Error(ex, "Failed to deserialize grid: " + path + " from file! Is this a shipblueprint?");
            }

            return false;
        }


        private bool LoadShipBlueprint(MyObjectBuilder_ShipBlueprintDefinition shipBlueprint, Vector3D GridSaveLocation, Vector3D SpawningPlayerLocation, bool keepOriginalLocation, Chat chat)
        {
            var grids = shipBlueprint.CubeGrids;

            if (grids == null || grids.Length == 0)
            {
                Chat?.Respond("No grids in blueprint!");
                return false;
            }


            MyIdentity Identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(SteamID));
            

            if (Identity != null)
            {
                PluginDependencies.BackupGrid(grids.ToList(), Identity.IdentityId);
            }


            Vector3D TargetLocation;
            bool AlignToGravity = false;
            if (keepOriginalLocation || SpawningPlayerLocation == Vector3D.Zero)
            {
                TargetLocation = GridSaveLocation;
            }
            else
            {
                AlignToGravity = true;
                TargetLocation = SpawningPlayerLocation;
            }

            ParallelSpawner Spawner = new ParallelSpawner(grids, chat, AlignToGravity);
            Log.Info("Attempting Grid Spawning @" + TargetLocation.ToString());
            return  Spawner.Start(keepOriginalLocation, TargetLocation);
        }


        public bool SaveGrids(GridResult Grids, GridStamp Stamp)
        {
            List<MyObjectBuilder_CubeGrid> objectBuilders = new List<MyObjectBuilder_CubeGrid>();
            foreach (MyCubeGrid grid in Grids.Grids)
            {
                /* Remove characters from cockpits */
                Action P = delegate
                {

                    foreach (var blck in grid.GetFatBlocks().OfType<MyCockpit>())
                    {
                        if (blck.Pilot != null)
                        {
                            blck.RemovePilot();
                        }
                    }

                };

                Task KickPlayers = GameEvents.InvokeActionAsync(P);
                KickPlayers.Wait(5000);


                /* What else should it be? LOL? */
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                objectBuilders.Add(objectBuilder);
            }





            try
            {
                //Need To check grid name

                string GridSavePath = Path.Combine(FolderPath, Stamp.GridName + ".sbc");

                //Log.Info("SavedDir: " + pathForPlayer);
                bool saved = SaveGridToFile(GridSavePath, Stamp.GridName, objectBuilders);

                if (saved)
                {
                    DisposeGrids(Grids.Grids);
                }


                return saved;
            }
            catch (Exception e)
            {
                //Hangar.Debug("Saving Grid Failed!", e, Hangar.ErrorType.Fatal);
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


        public static bool FindGridList(string gridNameOrEntityId, MyCharacter character, out List<MyCubeGrid> Grids)
        {

            Grids = new List<MyCubeGrid>();

            if (string.IsNullOrEmpty(gridNameOrEntityId) && character == null)
                return false;

            if (Config.EnableSubGrids)
            {
                //If we include subgrids in the grid grab

                long EntitiyID = character.Entity.EntityId;


                ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups;

                if (String.IsNullOrEmpty(gridNameOrEntityId))
                    groups = GridFinder.FindLookAtGridGroup(character);
                else
                    groups = GridFinder.FindGridGroup(gridNameOrEntityId);

                //Should only get one group
                if (groups.Count > 1)
                    return false;

                foreach (var group in groups)
                {
                    foreach (var node in group.Nodes)
                    {
                        MyCubeGrid Grid = node.NodeData;

                        if (Grid.Physics == null || Grid.IsPreview || Grid.MarkedForClose)
                            continue;

                        Grids.Add(Grid);
                    }
                }

            }
            else
            {
                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups;

                if (string.IsNullOrEmpty(gridNameOrEntityId))
                    groups = GridFinder.FindLookAtGridGroupMechanical(character);
                else
                    groups = GridFinder.FindGridGroupMechanical(gridNameOrEntityId);

                //Should only get one group
                if (groups.Count > 1)
                    return false;


                foreach (var group in groups)
                {
                    foreach (var node in group.Nodes)
                    {
                        MyCubeGrid Grid = node.NodeData;

                        if (Grid.Physics == null || Grid.IsPreview || Grid.MarkedForClose)
                            continue;

                        Grids.Add(Grid);
                    }
                }

            }


            if (Grids == null || Grids.Count == 0)
            {
                return false;
            }





            return true;
        }

        private static void PrepareGridForSave(MyObjectBuilder_ShipBlueprintDefinition GridDefinition)
        {
            
            foreach (MyObjectBuilder_CubeGrid cubeGrid in GridDefinition.CubeGrids)
            {
                Parallel.ForEach(cubeGrid.CubeBlocks, CubeBlock => {

                    if (!KeepOriginalOwner)
                    {
                        CubeBlock.Owner = 0L;
                        CubeBlock.BuiltBy = 0L;
                    }

                    /* Remove Projections if not needed */
                    if (!KeepProjectionsOnSave)
                        if (CubeBlock is MyObjectBuilder_ProjectorBase projector)
                            projector.ProjectedGrids = null;

                    /* Remove Pilot and Components (like Characters) from cockpits */
                    if (CubeBlock is MyObjectBuilder_Cockpit cockpit)
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
                });
            }
        }


        public static ConcurrentBag<List<MyCubeGrid>> FindGridList(long playerId, bool includeConnectedGrids)
        {

            ConcurrentBag<List<MyCubeGrid>> grids = new ConcurrentBag<List<MyCubeGrid>>();

            if (includeConnectedGrids)
            {

                Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups.ToList(), group =>
                {

                    List<MyCubeGrid> gridList = new List<MyCubeGrid>();

                    foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                    {

                        MyCubeGrid grid = groupNodes.NodeData;

                        if (grid == null || grid.MarkedForClose || grid.MarkedAsTrash)
                            continue;

                        gridList.Add(grid);
                    }

                    if (gridList.Count != 0 && IsPlayerIdCorrect(playerId, gridList))
                        grids.Add(gridList);
                });

            }
            else
            {
                Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups.ToList(), group =>
                {

                    List<MyCubeGrid> gridList = new List<MyCubeGrid>();

                    foreach (MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Node groupNodes in group.Nodes)
                    {

                        MyCubeGrid grid = groupNodes.NodeData;

                        if (grid == null || grid.MarkedForClose || grid.MarkedAsTrash)
                            continue;

                        gridList.Add(grid);
                    }

                    if (gridList.Count != 0 && IsPlayerIdCorrect(playerId, gridList))
                        grids.Add(gridList);
                });
            }

            return grids;
        }

        private static bool IsPlayerIdCorrect(long playerId, List<MyCubeGrid> gridList)
        {

            MyCubeGrid Grid = null;

            BiggestGrid(gridList, out Grid);


            /* No biggest grid should not be possible, unless the gridgroup only had projections -.- just skip it. */
            if (Grid == null || Grid.BigOwners.Count == 0)
                return false;

            if (Grid.BigOwners.Contains(playerId))
                return true;

            return false;
        }




        public static void FormatGridName(PlayerHangar Data, GridStamp result)
        {
            try
            {
                result.GridName = FileSaver.CheckInvalidCharacters(result.GridName);
                // Log.Warn("Running GridName Checks: {" + GridName + "} :" + Test);

                if (Data.SelectedPlayerFile.AnyGridsMatch(result.GridName))
                {
                    //There is already a grid with that name!
                    bool NameCheckDone = false;
                    int a = 1;
                    while (!NameCheckDone)
                    {
                        if (Data.SelectedPlayerFile.AnyGridsMatch(result.GridName + "[" + a + "]"))
                        {
                            a++;
                        }
                        else
                        {
                            //Hangar.Debug("Name check done! " + a);
                            NameCheckDone = true;
                            break;
                        }

                    }
                    //Main.Debug("Saving grid name: " + GridName);
                    result.GridName = result.GridName + "[" + a + "]";
                }
            }
            catch (Exception e)
            {
                Log.Warn(e);
            }
        }


        public static bool BiggestGrid(IEnumerable<MyCubeGrid> Grids, out MyCubeGrid BiggestGrid)
        {
            BiggestGrid = Grids.Aggregate((i1, i2) => i1.BlocksCount > i2.BlocksCount ? i1 : i2);
            return BiggestGrid != null;
        }


        public static bool ValidateGridOwnership(IEnumerable<MyCubeGrid> grids, long IdentityID, Chat Chat)
        {
            foreach(var grid in grids)
            {
                

                if (!grid.BigOwners.Contains(IdentityID))
                {
                    Chat?.Respond("You are not the owner of " + grid.DisplayName);
                    return false;
                }
                    
            }

            return true;
        }


    }

    public static class GridFinder
    {
        //Thanks LordTylus, I was too lazy to create my own little utils
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


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

                        if (grid.MarkedForClose || grid.MarkedAsTrash || !grid.InScene)
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

                        if (grid.MarkedForClose || grid.MarkedAsTrash || !grid.InScene)
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

                    if (grid.MarkedForClose || grid.MarkedAsTrash || !grid.InScene)
                        continue;

                    /* Gridname is wrong ignore */
                    if (!grid.DisplayName.Equals(gridName) && grid.EntityId + "" != gridName)
                        continue;

                    groups.Add(group);
                }
            });

            return groups;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindLookAtGridGroup(MyCharacter controlledEntity)
        {

            const float range = 5000;
            Matrix worldMatrix;
            Vector3D startPosition;
            Vector3D endPosition;

            worldMatrix = controlledEntity.GetHeadMatrix(true, true, false); // dead center of player cross hairs, or the direction the player is looking with ALT.
            startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            endPosition = worldMatrix.Translation + worldMatrix.Forward * (range);

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Physical.Groups)
            {
                foreach (MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Node groupNodes in group.Nodes)
                {
                    
                    MyCubeGrid cubeGrid = groupNodes.NodeData;
                    if (cubeGrid != null)
                    {
                        if (cubeGrid.MarkedForClose || cubeGrid.MarkedAsTrash || !cubeGrid.InScene)
                            continue;

                        // check if the ray comes anywhere near the Grid before continuing.    
                        if (ray.Intersects(cubeGrid.PositionComp.WorldAABB).HasValue)
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

                    if (grid.MarkedForClose || grid.MarkedAsTrash || !grid.InScene)
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

                        MyCubeGrid cubeGrid = groupNodes.NodeData;

                        if (cubeGrid != null)
                        {

                            if (cubeGrid.MarkedForClose || cubeGrid.MarkedAsTrash || !cubeGrid.InScene)
                                continue;

                            // check if the ray comes anywhere near the Grid before continuing.    
                            if (ray.Intersects(cubeGrid.PositionComp.WorldAABB).HasValue)
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

}
