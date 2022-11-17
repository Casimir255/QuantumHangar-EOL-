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

        public static async Task<bool> SaveGridsAndClose(IEnumerable<MyCubeGrid> Grids, string Path, string GridName,
            long OwnerIdentity)
        {
            var CurrentThread = Thread.CurrentThread;
            if (CurrentThread == MyUtils.MainThread)
            {
                //Log.Warn("Running on game thread!");

                var grids = GetObjectBuilders(Grids);
                Grids.Close();


                await Task.Run(() =>
                {
                    SaveGridToFile(Path, GridName, grids);
                    PluginDependencies.BackupGrid(grids.ToList(), OwnerIdentity);
                });

                return true;
            }
            else
            {
                //Log.Warn("Not running on game thread!");

                //Log.Info(Grids.Count());
                var GridTask =
                    GameEvents.InvokeAsync<IEnumerable<MyCubeGrid>, IEnumerable<MyObjectBuilder_CubeGrid>>(
                        GetObjectBuilders, Grids);
                if (!GridTask.Wait(5000))
                {
                    Log.Info("Grid saving timed out!");
                    return false;
                }
                else
                {
                    Grids.Close();
                    //ClearAllAttachments(GridTask.Result);
                    SaveGridToFile(Path, GridName, GridTask.Result);
                    PluginDependencies.BackupGrid(GridTask.Result.ToList(), OwnerIdentity);
                    return true;
                }
            }
        }

        private static void RemoveCharacters(MyCubeGrid Grid)
        {
            foreach (var black in Grid.GetFatBlocks().OfType<MyCockpit>())
                if (black.Pilot != null)
                {
                    black.RequestRemovePilot();
                    black.RemovePilot();
                }
        }

        private static IEnumerable<MyObjectBuilder_CubeGrid> GetObjectBuilders(IEnumerable<MyCubeGrid> Grids)
        {
            //Log.Info("Collecting ObjectBuilders");
            var Return = new List<MyObjectBuilder_CubeGrid>();

            foreach (var grid in Grids)
            {
                //Log.Info(grid.DisplayName);
                RemoveCharacters(grid);
                //Log.Info("Removed Characters!");
                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");
                // Log.Info("Adding objectbuilder!");
                Return.Add(objectBuilder);
            }

            return Return;
        }

        public static void ResetGear(IEnumerable<MyObjectBuilder_CubeGrid> Grids)
        {
            foreach (var grid in Grids)
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


        private static bool SaveGridToFile(string SavePath, string GridName,
            IEnumerable<MyObjectBuilder_CubeGrid> GridBuilders)
        {
            var definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)),
                GridName);
            definition.CubeGrids = GridBuilders.ToArray();
            //PrepareGridForSave(definition);

            var builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };


            Log.Warn("Saving grid @" + Path.Combine(SavePath, GridName + ".sbc"));
            return MyObjectBuilderSerializer.SerializeXML(Path.Combine(SavePath, GridName + ".sbc"), false,
                builderDefinition);
        }

        public static bool LoadGrid(string Path, out IEnumerable<MyObjectBuilder_CubeGrid> Grids)
        {
            Grids = Enumerable.Empty<MyObjectBuilder_CubeGrid>();
            if (!File.Exists(Path))
            {
                Log.Error("Grid doesnt exsist @" + Path);
                return false;
            }

            try
            {
                if (MyObjectBuilderSerializer.DeserializeXML(Path, out MyObjectBuilder_Definitions Def))
                {
                    if (!TryGetGridsFromDefinition(Def, out Grids))
                        return false;

                    ResetGear(Grids);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to deserialize grid: " + Path + " from file! Is this even an sbc?");
            }

            return false;
        }

        private static bool TryGetGridsFromDefinition(MyObjectBuilder_Definitions Definition,
            out IEnumerable<MyObjectBuilder_CubeGrid> Grids)
        {
            //Should be able to load shipdef and prefabs that people load/drag in
            Grids = new List<MyObjectBuilder_CubeGrid>();
            if (Definition.Prefabs != null && Definition.Prefabs.Count() != 0)
            {
                Grids = Definition.Prefabs.Aggregate(Grids, (current, prefab) => current.Concat(prefab.CubeGrids));
                return true;
            }

            if (Definition.ShipBlueprints != null && Definition.ShipBlueprints.Count() != 0)
            {
                Grids = Definition.ShipBlueprints.Aggregate(Grids, (current, shipBlueprint) => current.Concat(shipBlueprint.CubeGrids));
                return true;
            }
            Log.Error("Invalid Definition file!");
            return false;
        }


        public static void TransferGridOwnership(IEnumerable<MyObjectBuilder_CubeGrid> Grids, long Player,
            bool Force = false)
        {
            if (!Force && !Config.OnLoadTransfer) return;
            //Will transfer pcu to new player
            foreach (var CubeGridDef in Grids)
            foreach (var block in CubeGridDef.CubeBlocks)
            {
                block.Owner = Player;
                block.BuiltBy = Player;
            }
        }
    }
}