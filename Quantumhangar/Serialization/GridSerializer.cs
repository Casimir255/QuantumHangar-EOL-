using NLog;
using QuantumHangar.Utilities;
using QuantumHangar.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace QuantumHangar.Serialization
{
    public static class GridSerializer
    {
        private static Settings Config => Hangar.Config;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static async Task<bool> SaveGridsAndClose(IEnumerable<MyCubeGrid> grids, string path, string gridName,
            long ownerIdentity)
        {
            var currentThread = Thread.CurrentThread;
            if (currentThread == MyUtils.MainThread)
            {
                //Log.Warn("Running on game thread!");

                var gridObjects = GetObjectBuilders(grids);
                grids.Close();


                await Task.Run(() =>
                {
                    SaveGridToFile(path, gridName, gridObjects);
                    PluginDependencies.BackupGrid(gridObjects.ToList(), ownerIdentity);
                });

                return true;
            }
            //Log.Warn("Not running on game thread!");

            //Log.Info(Grids.Count());
            var gridTask =
                GameEvents.InvokeAsync(
                    GetObjectBuilders, grids);
            if (!gridTask.Wait(5000))
            {
                Log.Info("Grid saving timed out!");
                return false;
            }
            else
            {
                grids.Close();
                //ClearAllAttachments(GridTask.Result);
                SaveGridToFile(path, gridName, gridTask.Result);
                PluginDependencies.BackupGrid(gridTask.Result.ToList(), ownerIdentity);
                return true;
            }
        }

        private static void RemoveCharacters(MyCubeGrid grid)
        {
            foreach (var black in grid.GetFatBlocks().OfType<MyCockpit>())
                if (black.Pilot != null)
                {
                    black.RequestRemovePilot();
                    black.RemovePilot();
                }
        }

        private static IEnumerable<MyObjectBuilder_CubeGrid> GetObjectBuilders(IEnumerable<MyCubeGrid> grids)
        {
            //Log.Info("Collecting ObjectBuilders");
            var @return = new List<MyObjectBuilder_CubeGrid>();

            foreach (var grid in grids)
            {
                //Log.Info(grid.DisplayName);
                RemoveCharacters(grid);
                //Log.Info("Removed Characters!");
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");
                // Log.Info("Adding objectbuilder!");
                @return.Add(objectBuilder);
            }

            return @return;
        }

        public static void ResetGear(IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            foreach (var grid in grids)
            foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_LandingGear>())
            {
                //No sense and relocking something that is already off
                if (!block.Enabled)
                    continue;

                block.IsLocked = false;
                block.AutoLock = true;
                block.FirstLockAttempt = false;
                block.AttachedEntityId = null;
                block.MasterToSlave = null;
                block.GearPivotPosition = null;
                block.OtherPivot = null;
                block.LockMode = SpaceEngineers.Game.ModAPI.Ingame.LandingGearMode.Unlocked;
            }
        }


        private static bool SaveGridToFile(string savePath, string gridName,
            IEnumerable<MyObjectBuilder_CubeGrid> gridBuilders)
        {
            var definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)),
                gridName);
            definition.CubeGrids = gridBuilders.ToArray();
            //PrepareGridForSave(definition);

            var builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new[] { definition };


            Log.Warn("Saving grid @" + Path.Combine(savePath, gridName + ".sbc"));
            return MyObjectBuilderSerializer.SerializeXML(Path.Combine(savePath, gridName + ".sbc"), false,
                builderDefinition);
        }

        public static bool LoadGrid(string path, out IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            grids = Enumerable.Empty<MyObjectBuilder_CubeGrid>();
            if (!File.Exists(path))
            {
                Log.Error("Grid doesnt exsist @" + path);
                return false;
            }

            try
            {
                if (MyObjectBuilderSerializer.DeserializeXML(path, out MyObjectBuilder_Definitions def))
                {
                    if (!TryGetGridsFromDefinition(def, out grids))
                        return false;

                    ResetGear(grids);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to deserialize grid: " + path + " from file! Is this even an sbc?");
            }

            return false;
        }

        private static bool TryGetGridsFromDefinition(MyObjectBuilder_Definitions definition,
            out IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            //Should be able to load shipdef and prefabs that people load/drag in
            grids = new List<MyObjectBuilder_CubeGrid>();
            if (definition.Prefabs != null && definition.Prefabs.Count() != 0)
            {
                grids = definition.Prefabs.Aggregate(grids, (current, prefab) => current.Concat(prefab.CubeGrids));
                return true;
            }

            if (definition.ShipBlueprints != null && definition.ShipBlueprints.Count() != 0)
            {
                grids = definition.ShipBlueprints.Aggregate(grids, (current, shipBlueprint) => current.Concat(shipBlueprint.CubeGrids));
                return true;
            }
            Log.Error("Invalid Definition file!");
            return false;
        }


        public static void TransferGridOwnership(IEnumerable<MyObjectBuilder_CubeGrid> grids, long player,
            bool force = false)
        {
            if (!force && !Config.OnLoadTransfer) return;
            //Will transfer pcu to new player
            foreach (var cubeGridDef in grids)
            foreach (var block in cubeGridDef.CubeBlocks)
            {
                block.Owner = player;
                block.BuiltBy = player;
            }
        }
    }
}