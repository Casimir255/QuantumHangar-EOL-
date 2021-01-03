using NLog;
using QuantumHangar.HangarChecks;
using QuantumHangar.Utilities;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.ObjectBuilders;

namespace QuantumHangar.Serialization
{
    public static class GridSerializer
    {


        private static Settings Config { get { return Hangar.Config; } }
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static bool SaveGridsAndClose(IEnumerable<MyCubeGrid> Grids, string Path, string GridName)
        {
            Task<IEnumerable<MyObjectBuilder_CubeGrid>> GridTask = GameEvents.InvokeAsync<IEnumerable<MyCubeGrid>, IEnumerable<MyObjectBuilder_CubeGrid>>(GetObjectBuilders, Grids);
            if (!GridTask.Wait(5000))
            {
                return false;
            }
            else
            {
                CloseAllGrids(Grids);
                SaveGridToFile(Path, GridName, GridTask.Result);
                return true;
            }
        }

        private static void RemoveCharacters(MyCubeGrid Grid)
        {
            foreach (var blck in Grid.GetFatBlocks().OfType<MyCockpit>())
            {
                if (blck.Pilot != null)
                {
                    blck.RequestRemovePilot();
                    blck.RemovePilot();
                }
            }
        }

        private static IEnumerable<MyObjectBuilder_CubeGrid> GetObjectBuilders(IEnumerable<MyCubeGrid> Grids)
        {
            List<MyObjectBuilder_CubeGrid> Return = new List<MyObjectBuilder_CubeGrid>();

            foreach (MyCubeGrid grid in Grids)
            {
                RemoveCharacters(grid);

                if (!(grid.GetObjectBuilder() is MyObjectBuilder_CubeGrid objectBuilder))
                    throw new ArgumentException(grid + " has a ObjectBuilder thats not for a CubeGrid");

                Return.Add(objectBuilder);
            }

            return Return;
        }


        private static bool SaveGridToFile(string SavePath, string GridName, IEnumerable<MyObjectBuilder_CubeGrid> GridBuilders)
        {
            MyObjectBuilder_ShipBlueprintDefinition definition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_ShipBlueprintDefinition>();

            definition.Id = new MyDefinitionId(new MyObjectBuilderType(typeof(MyObjectBuilder_ShipBlueprintDefinition)), GridName);
            definition.CubeGrids = GridBuilders.ToArray();
            //PrepareGridForSave(definition);

            MyObjectBuilder_Definitions builderDefinition = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_Definitions>();
            builderDefinition.ShipBlueprints = new MyObjectBuilder_ShipBlueprintDefinition[] { definition };




            Log.Warn("Saving grid @" + Path.Combine(SavePath, GridName + ".sbc"));
            return MyObjectBuilderSerializer.SerializeXML(Path.Combine(SavePath, GridName + ".sbc"), false, builderDefinition);
        }

        public static void CloseAllGrids(IEnumerable<MyCubeGrid> Grids)
        {
            Grids.ForEach(x => x.Close());
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

                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to deserialize grid: " + Path + " from file! Is this even an sbc?");
            }

            return false;


        }

        private static bool TryGetGridsFromDefinition(MyObjectBuilder_Definitions Definition, out IEnumerable<MyObjectBuilder_CubeGrid> Grids)
        {
            //Should be able to load shipdef and prefabs that people load/drag in
            Grids = new List<MyObjectBuilder_CubeGrid>();
            if (Definition.Prefabs != null && Definition.Prefabs.Count() != 0)
            {
                foreach (var prefab in Definition.Prefabs)
                {
                    Grids = Grids.Concat(prefab.CubeGrids);
                }
                return true;
            }
            else if (Definition.ShipBlueprints != null && Definition.ShipBlueprints.Count() != 0)
            {
                foreach (var shipBlueprint in Definition.ShipBlueprints)
                {

                    Grids = Grids.Concat(shipBlueprint.CubeGrids);
                }
                return true;
            }
            else
            {
                Log.Error("Invalid Definition file!");
                return false;
            }
        }



        public static void TransferGridOwnership(IEnumerable<MyObjectBuilder_CubeGrid> Grids, long Player)
        {
            if (Config.OnLoadTransfer)
            {
                //Will transfer pcu to new player
                foreach (MyObjectBuilder_CubeGrid CubeGridDef in Grids)
                {
                    foreach (MyObjectBuilder_CubeBlock block in CubeGridDef.CubeBlocks)
                    {
                        block.Owner = Player;
                        block.BuiltBy = Player;
                    }
                }
            }

        }


    }
}
