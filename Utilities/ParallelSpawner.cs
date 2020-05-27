using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace QuantumHangar
{
    public class ParallelSpawner
    {
        //Rexxars spicy ParallelSpawner
        private readonly int _maxCount;
        private readonly MyObjectBuilder_CubeGrid[] _grids;
        private readonly Action<HashSet<IMyCubeGrid>> _callback;
        private readonly HashSet<IMyCubeGrid> _spawned;

        public ParallelSpawner(MyObjectBuilder_CubeGrid[] grids, Action<HashSet<IMyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Length;
            _callback = callback;
            _spawned = new HashSet<IMyCubeGrid>();
        }

        public void Start()
        {
            foreach (var o in _grids)
                MyAPIGateway.Entities.CreateFromObjectBuilderParallel(o, false, Increment);
        }

        public void Increment(IMyEntity entity)
        {
            var grid = (IMyCubeGrid)entity;
            _spawned.Add(grid);

            if (_spawned.Count < _maxCount)
                return;

            foreach (var g in _spawned)
            {
                MyAPIGateway.Entities.AddEntity(g, true);
            }

            _callback?.Invoke(_spawned);
        }
    }

    public class AlignToGravity
    {
        private readonly MyObjectBuilder_ShipBlueprintDefinition[] _ShipBlueprints;
        private readonly Vector3D _PlayerPosition;
        private readonly Chat chat;

        public AlignToGravity(MyObjectBuilder_ShipBlueprintDefinition[] ShipBlueprints, Vector3D PlayerPosition, Chat Context)
        {
            _ShipBlueprints = ShipBlueprints;
            _PlayerPosition = PlayerPosition;

            //Command context is for giving chat messages on grid spawning. You can remove this
            chat = Context;
        }

        private bool CalculateGridPosition()
        {
            
            List<MyObjectBuilder_CubeGrid> TotalGrids = new List<MyObjectBuilder_CubeGrid>();
            List<MyObjectBuilder_Cockpit> cockpits = new List<MyObjectBuilder_Cockpit>();
            Vector3D direction = _PlayerPosition;


            

            //Get all cockpit blkocks on the grid
            foreach (var shipBlueprint in _ShipBlueprints)
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
                //Simple grid/objectbuilder null check. If there are no gridys then why continue?
                return false;
            }
            Hangar.Debug("Total Grids to be pasted: " + array.Count());

            if (cockpits.Count > 0)
            {
                //Main.Debug("Cockpits found!");
                foreach (MyObjectBuilder_Cockpit Block in cockpits)
                {
                    if (Block.IsMainCockpit)
                    {
                        Hangar.Debug("Main cockpit found! Attempting to Align!");
                        direction = new Vector3D(Block.Orientation.x, Block.Orientation.y, Block.Orientation.z);
                        break;
                    }
                }
            }
            else
            {
                Hangar.Debug("No Cockpits. Continuing based off of grid pivot point!");
            }


            //Attempt to get gravity/Artificial gravity to align the grids to
            Vector3D position = _PlayerPosition;

            //Here you can adjust the offset from the surface and rotation.
            //Unfortunatley we move the grid again after this to find a free space around the character. Perhaps later i can incorporate that into
            //LordTylus's existing grid checkplament method
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
                Hangar.Debug("Attempting to correct grid orientation!");
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


            return BeginAlignToGravity(array, position, direction, vector3D);
        }

        private bool BeginAlignToGravity(MyObjectBuilder_CubeGrid[] AllGrids, Vector3D position, Vector3D direction, Vector3D vector3D)
        {
            //Create WorldMatrix
            MatrixD worldMatrix = MatrixD.CreateWorld(position, direction, vector3D);

            int num = 0;
            MatrixD matrix = MatrixD.Identity;

            //Find biggest grid and get their postion matrix
            Parallel.For(0, AllGrids.Length, i =>
            {
                //Option to clone the BP
                //array[i] = (MyObjectBuilder_CubeGrid)TotalGrids[i].Clone();
                if (AllGrids[i].CubeBlocks.Count > num)
                {
                    num = AllGrids[i].CubeBlocks.Count;
                    matrix = (AllGrids[i].PositionAndOrientation.HasValue ? AllGrids[i].PositionAndOrientation.Value.GetMatrix() : MatrixD.Identity);
                }

            });

            MatrixD matrix2;


            //Align to Main/Biggest grid
            Vector3D value = Vector3D.Zero;
            if (AllGrids[0].PositionAndOrientation.HasValue)
            {
                value = AllGrids[0].PositionAndOrientation.Value.Position;
            }
            matrix2 = MatrixD.CreateWorld(-value, direction, vector3D);


            //Huh? (Keen does this so i guess i will too) My guess so it can create large entities
            MyEntities.IgnoreMemoryLimits = true;

            //Update each grid in the array
            Parallel.For(0, AllGrids.Length, j =>
            {
                MatrixD newWorldMatrix;

                if (AllGrids[j].PositionAndOrientation.HasValue)
                {
                    MatrixD matrix3 = AllGrids[j].PositionAndOrientation.Value.GetMatrix() * MatrixD.Invert(matrix);
                    newWorldMatrix = matrix3 * worldMatrix;
                    AllGrids[j].PositionAndOrientation = new MyPositionAndOrientation(newWorldMatrix);
                }
                else
                {
                    newWorldMatrix = worldMatrix;
                    AllGrids[j].PositionAndOrientation = new MyPositionAndOrientation(worldMatrix);
                }



            });


            /* Where do we want to paste the grids? Lets find out. based from the character/position */
            var pos = FindPastePosition(AllGrids, position);
            if (pos == null)
            {

                Hangar.Debug("No free Space found!");


                chat.Respond("No free space available!");

                return false;
            }

            var newPosition = pos.Value;

            /* Update GridsPosition via xyz. (We already have the orientation correct) if that doesnt work get out of here. */
            if (!UpdateGridsPosition(AllGrids, newPosition))
            {

                chat.Respond("The File to be imported does not seem to be compatible with the server!");

                return false;
            }


            //Remap to prevent bad stuff
            MyEntities.RemapObjectBuilderCollection(AllGrids);

            //Use Rexxars spciy spaghetti code for parallel spawning of ALL grids
            ParallelSpawner spawner = new ParallelSpawner(AllGrids);
            spawner.Start();

            //Return completeted
            return true;
        }

        //These three methods based off of LordTylus grid spawning
        private bool UpdateGridsPosition(MyObjectBuilder_CubeGrid[] grids, Vector3D newPosition)
        {
            //Based off of LordTylus for grid xyz position
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

        //Start grid spawning
        public bool Start()
        {
            //Return bool whether it was a success or not
            return CalculateGridPosition();
        }

    }

    public class Chat
    {
        private CommandContext _context;
        private bool _mod;

        //Simple chat class so i can control the colors easily
        public Chat(CommandContext context, bool Mod = false)
        {
            _context = context;
            _mod = Mod;
        }

        public void Respond(string response)
        {
            if (_context == null)
                return;


            if (_mod)
            {
                //Should fix admin commands
                _context.Respond(response);
            }
            else
            {
                _context.Respond(response, Color.Yellow, "Hangar");
            }
        }

        

        public static void Respond(string response, CommandContext context)
        {
            context.Respond(response, Color.Yellow, "Hangar");
        }
    }
}
