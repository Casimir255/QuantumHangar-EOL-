using NLog;
using ParallelTasks;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Groups;
using VRageMath;

namespace QuantumHangar.Utils
{
    public static class GridUtilities
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static Settings Config => Hangar.Config;

        public static bool FindGridList(string gridNameOrEntityId, MyCharacter character, out List<MyCubeGrid> Grids)
        {
            Grids = new List<MyCubeGrid>();

            if (string.IsNullOrEmpty(gridNameOrEntityId) && character == null)
                return false;


            if (Config.EnableSubGrids)
            {
                //If we include subgrids in the grid grab
                ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups;

                if (string.IsNullOrEmpty(gridNameOrEntityId) && character != null)
                    groups = GridFinder.FindLookAtGridGroup(character);
                else
                    groups = GridFinder.FindGridGroup(gridNameOrEntityId);

                //Should only get one group
                if (groups.Count > 1)
                    return false;

                Grids.AddRange(groups.SelectMany(group => group.Nodes, (group, node) => node.NodeData)
                    .Where(Grid => Grid.Physics != null && !Grid.IsPreview && !Grid.MarkedForClose));
            }
            else
            {
                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups;

                groups = string.IsNullOrEmpty(gridNameOrEntityId)
                    ? GridFinder.FindLookAtGridGroupMechanical(character)
                    : GridFinder.FindGridGroupMechanical(gridNameOrEntityId);

                //Should only get one group
                if (groups.Count > 1)
                    return false;


                Grids.AddRange(groups.SelectMany(group => group.Nodes, (group, node) => node.NodeData)
                    .Where(Grid => Grid.Physics != null && !Grid.IsPreview && !Grid.MarkedForClose));
            }

            return Grids != null && Grids.Count != 0;
        }

        public static ConcurrentBag<List<MyCubeGrid>> FindGridList(long playerId, bool includeConnectedGrids)
        {
            var grids = new ConcurrentBag<List<MyCubeGrid>>();

            if (includeConnectedGrids)
                foreach (var gridList in MyCubeGridGroups.Static.Physical.Groups.ToList()
                             .Select(group =>
                                 group.Nodes.Select(groupNodes => groupNodes.NodeData).Where(grid =>
                                     grid != null && !grid.MarkedForClose && !grid.MarkedAsTrash).ToList())
                             .Where(gridList => gridList.Count != 0 && gridList.IsPlayerOwner(playerId)))
                    grids.Add(gridList);
            else
                foreach (var gridList in MyCubeGridGroups.Static.Mechanical.Groups.ToList()
                             .Select(group =>
                                 group.Nodes.Select(groupNodes => groupNodes.NodeData).Where(grid =>
                                     grid != null && !grid.MarkedForClose && !grid.MarkedAsTrash).ToList())
                             .Where(gridList => gridList.Count != 0 && gridList.IsPlayerOwner(playerId)))
                    grids.Add(gridList);

            return grids;
        }

        private static bool IsPlayerOwner(this IEnumerable<MyCubeGrid> Grids, long playerId)
        {
            Grids.BiggestGrid(out var Grid);


            /* No biggest grid should not be possible, unless the gridgroup only had projections -.- just skip it. */
            if (Grid == null || Grid.BigOwners.Count == 0)
                return false;

            return Grid.BigOwners.Contains(playerId);
        }

        public static void BiggestGrid(this IEnumerable<MyCubeGrid> Grids, out MyCubeGrid BiggestGrid)
        {
            BiggestGrid = Grids.Aggregate((i1, i2) => i1.BlocksCount > i2.BlocksCount ? i1 : i2);
        }

        public static void BiggestGrid(this IEnumerable<MyObjectBuilder_CubeGrid> Grids,
            out MyObjectBuilder_CubeGrid BiggestGrid)
        {
            BiggestGrid = Grids.Aggregate((i1, i2) => i1.CubeBlocks.Count > i2.CubeBlocks.Count ? i1 : i2);
        }

        public static long GetBiggestOwner(this MyCubeGrid grid)
        {
            var FatBlocks = grid.GetFatBlocks().ToList();

            var TotalFatBlocks = 0;


            var owners = new Dictionary<long, int>();
            foreach (var fat in FatBlocks.Where(fat => fat.IsFunctional && fat.IDModule != null))
            {
                //WTF happened here?
                //if (fat.OwnerId == 0)
                //   Log.Error($"WTF: {fat.BlockDefinition.Id} - {fat.GetType()} - {fat.OwnerId}");


                TotalFatBlocks++;

                if (fat.OwnerId == 0) continue;
                if (!owners.ContainsKey(fat.OwnerId))
                    owners.Add(fat.OwnerId, 1);
                else
                    owners[fat.OwnerId] += 1;
            }

            return owners.Count == 0 ? 0 : owners.FirstOrDefault(x => x.Value == owners.Values.Max()).Key;
        }


        public static void Close(this IEnumerable<MyCubeGrid> Grids, string Reason = "Grid was Hangared")
        {
            var Builder = new StringBuilder();
            Builder.AppendLine("Closing the following grids: ");
            foreach (var Grid in Grids)
            {
                Builder.Append(Grid.DisplayName + ", ");
                Grid.Close();
            }

            Builder.AppendLine("Reason: " + Reason);

            Log.Info(Builder.ToString());
        }
    }

    public static class GridFinder
    {
        //Thanks LordTylus, I was too lazy to create my own little utils
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static ConcurrentBag<List<MyCubeGrid>> FindGridList(long playerId, bool includeConnectedGrids)
        {
            var grids = new ConcurrentBag<List<MyCubeGrid>>();

            if (includeConnectedGrids)
                Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group =>
                {
                    var gridList = group.Nodes.Select(groupNodes => groupNodes.NodeData)
                        .Where(grid => !grid.MarkedForClose && !grid.MarkedAsTrash && grid.InScene).ToList();

                    if (IsPlayerIdCorrect(playerId, gridList))
                        grids.Add(gridList);
                });
            else
                Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group =>
                {
                    var gridList = group.Nodes.Select(groupNodes => groupNodes.NodeData).Where(grid => !grid.MarkedForClose && !grid.MarkedAsTrash && grid.InScene).ToList();

                    if (IsPlayerIdCorrect(playerId, gridList))
                        grids.Add(gridList);
                });

            return grids;
        }

        private static bool IsPlayerIdCorrect(long playerId, List<MyCubeGrid> gridList)
        {
            MyCubeGrid biggestGrid = null;

            foreach (var grid in gridList.Where(grid => biggestGrid == null || biggestGrid.BlocksCount < grid.BlocksCount))
                biggestGrid = grid;

            /* No biggest grid should not be possible, unless the gridgroup only had projections -.- just skip it. */
            if (biggestGrid == null)
                return false;

            var hasOwners = biggestGrid.BigOwners.Count != 0;

            if (hasOwners) return playerId == biggestGrid.BigOwners[0];
            return playerId == 0L;
        }

        public static List<MyCubeGrid> FindGridList(string gridNameOrEntityId, MyCharacter character,
            bool includeConnectedGrids)
        {
            var grids = new List<MyCubeGrid>();

            if (gridNameOrEntityId == null && character == null)
                return new List<MyCubeGrid>();

            if (includeConnectedGrids)
            {
                ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> groups;

                groups = gridNameOrEntityId == null ? FindLookAtGridGroup(character) : FindGridGroup(gridNameOrEntityId);

                if (groups.Count > 1)
                    return null;

                grids.AddRange(groups.SelectMany(group => group.Nodes, (group, node) => node.NodeData).Where(grid => grid.Physics != null));
            }
            else
            {
                ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> groups;

                groups = gridNameOrEntityId == null ? FindLookAtGridGroupMechanical(character) : FindGridGroupMechanical(gridNameOrEntityId);

                if (groups.Count > 1)
                    return null;

                grids.AddRange(groups.SelectMany(group => group.Nodes, (group, node) => node.NodeData).Where(grid => grid.Physics != null));
            }

            return grids;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindGridGroup(string gridName)
        {
            var groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();
            Parallel.ForEach(MyCubeGridGroups.Static.Physical.Groups, group =>
            {
                foreach (var _ in from groupNodes in @group.Nodes select groupNodes.NodeData into grid where !grid.MarkedForClose && !grid.MarkedAsTrash && grid.InScene where grid.DisplayName.Equals(gridName) || grid.EntityId + "" == gridName select grid)
                {
                    groups.Add(group);
                }
            });

            return groups;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group> FindLookAtGridGroup(
            MyCharacter controlledEntity)
        {
            const float range = 5000;

            Matrix worldMatrix = controlledEntity.GetHeadMatrix(true); // dead center of player cross hairs, or the direction the player is looking with ALT.
            Vector3D startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
            Vector3D endPosition = worldMatrix.Translation + worldMatrix.Forward * range;

            var list = new Dictionary<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group, double>();
            var ray = new RayD(startPosition, worldMatrix.Forward);

            foreach (var group in MyCubeGridGroups.Static.Physical.Groups)
            foreach (var distance in from groupNodes in @group.Nodes select groupNodes.NodeData into cubeGrid where cubeGrid != null where !cubeGrid.MarkedForClose && !cubeGrid.MarkedAsTrash && cubeGrid.InScene where ray.Intersects(cubeGrid.PositionComp.WorldAABB).HasValue let hit = cubeGrid.RayCastBlocks(startPosition, endPosition) where hit.HasValue select (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length())
            {
                if (list.TryGetValue(group, out var oldDistance))
                {
                    if (!(distance < oldDistance)) continue;
                    list.Remove(group);
                    list.Add(group, distance);
                }
                else
                {
                    list.Add(group, distance);
                }
            }

            var bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridPhysicalGroupData>.Group>();


            if (list.Count == 0)
                return bag;

            // find the closest Entity.
            var item = list.OrderBy(f => f.Value).First();
            bag.Add(item.Key);

            return bag;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group> FindGridGroupMechanical(
            string gridName)
        {
            var groups = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();
            Parallel.ForEach(MyCubeGridGroups.Static.Mechanical.Groups, group =>
            {
                foreach (var _ in from groupNodes in @group.Nodes select groupNodes.NodeData into grid where !grid.MarkedForClose && !grid.MarkedAsTrash && grid.InScene where grid.DisplayName.Equals(gridName) || grid.EntityId + "" == gridName select grid)
                {
                    groups.Add(group);
                }
            });

            return groups;
        }

        public static ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>
            FindLookAtGridGroupMechanical(IMyCharacter controlledEntity)
        {
            try
            {
                const float range = 5000;

                Matrix worldMatrix = controlledEntity.GetHeadMatrix(true); // dead center of player cross hairs, or the direction the player is looking with ALT.
                Vector3D startPosition = worldMatrix.Translation + worldMatrix.Forward * 0.5f;
                Vector3D endPosition = worldMatrix.Translation + worldMatrix.Forward * (range + 0.5f);

                var list = new Dictionary<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group, double>();
                var ray = new RayD(startPosition, worldMatrix.Forward);

                foreach (var group in MyCubeGridGroups.Static.Mechanical.Groups)
                foreach (var distance in from groupNodes in @group.Nodes select groupNodes.NodeData into cubeGrid where cubeGrid != null where !cubeGrid.MarkedForClose && !cubeGrid.MarkedAsTrash && cubeGrid.InScene where ray.Intersects(cubeGrid.PositionComp.WorldAABB).HasValue let hit = cubeGrid.RayCastBlocks(startPosition, endPosition) where hit.HasValue select (startPosition - cubeGrid.GridIntegerToWorld(hit.Value)).Length())
                {
                    if (list.TryGetValue(group, out var oldDistance))
                    {
                        if (!(distance < oldDistance)) continue;
                        list.Remove(group);
                        list.Add(group, distance);
                    }
                    else
                    {
                        list.Add(group, distance);
                    }
                }

                var bag = new ConcurrentBag<MyGroups<MyCubeGrid, MyGridMechanicalGroupData>.Group>();

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