using Havok;
using QuantumHangar.Utilities;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Voxels;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
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
        private readonly Chat _Response;
        private static int Timeout = 6000;
        public bool _AlignToGravity = false;

        public ParallelSpawner(MyObjectBuilder_CubeGrid[] grids, Chat chat, bool AlignToGravity = false, Action<HashSet<IMyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Length;
            _callback = callback;
            _spawned = new HashSet<IMyCubeGrid>();
            _Response = chat;
            _AlignToGravity = AlignToGravity;
        }

        public bool Start(bool LoadInOriginalPosition, Vector3D Target)
        {
            if (_grids.Length == 0)
            {
                //Simple grid/objectbuilder null check. If there are no gridys then why continue?
                return true;
            }


            MyEntities.RemapObjectBuilderCollection(_grids);


            if (_AlignToGravity)
            {
                EnableRequiredItemsOnLoad(_grids);
                CalculateGridPosition(Target);
            }


            

            Task<bool> Spawn = GameEvents.InvokeAsync<bool, Vector3D, bool>(CalculateSafePositionAndSpawn, LoadInOriginalPosition, Target);
            if (Spawn.Wait(Timeout))
            {
                if (Spawn.Result)
                {
                    foreach (var o in _grids)
                    {
                        MyAPIGateway.Entities.CreateFromObjectBuilderParallel(o, false, Increment);
                    }
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                _Response.Respond("Parrallel Grid Spawner Timed out!");
                return false;
            }
        }




        private bool CalculateSafePositionAndSpawn(bool keepOriginalLocation, Vector3D Target)
        {
            //This has to be ran on the main game thread!
            if (keepOriginalLocation)
            {
                var BoundingBox = FindBoundingBox(_grids);

               

                //sphere.Center = Target;
                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllEntitiesInOBB(ref BoundingBox, entities);

               
                Hangar.Debug(BoundingBox.ToString());

                Hangar.Debug(entities.Count.ToString());
                bool PotentialGrids = false;
                foreach (var entity in entities)
                {
                    if (entity is MyCubeGrid )
                    {
                        PotentialGrids = true;

                        BoundingBox Box = entity.PositionComp.LocalAABB;
                        ContainmentType Type = BoundingBox.Contains(ref Box);
                        
                       Hangar.Debug(entity.DisplayName + " is intersecting spawn area! Containment Type: "+ Type.ToString());
                        

                        _Response.Respond("There are potentially other grids in the way. Attempting to spawn around the location to avoid collisions.");
                        break;
                    }
                }


                if (PotentialGrids)
                {
                    var pos = FindPastePosition(Target);
                    if (!pos.HasValue)
                    {
                        _Response.Respond("No free spawning zone found! Stopping load!");
                        return false;
                    }

                    UpdateGridsPosition(pos.Value);
                    return true;
                }
                else
                {
                    return true;
                }
            }


            //Everything else is loading for near point
            if (!keepOriginalLocation)
            {
                /* Where do we want to paste the grids? Lets find out. */
                var pos = FindPastePosition(Target);
                if (pos == null)
                {
                    _Response.Respond("No free spawning zone found! Stopping load!");
                    return false;
                }

                var newPosition = pos.Value;

                /* Update GridsPosition if that doesnt work get out of here. */
                if (!UpdateGridsPosition(newPosition))
                {
                    _Response.Respond("The File to be imported does not seem to be compatible with the server!");
                    return false;
                }
            }
            return true;
        }

        private Vector3D? FindPastePosition(Vector3D Target)
        {

            BoundingSphereD sphere = FindBoundingSphere(_grids);
            /* 
             * Now we know the radius that can house all grids which will now be 
             * used to determine the perfect place to paste the grids to. 
             */

            return MyEntities.FindFreePlaceCustom(Target, (float)sphere.Radius, 90, 10, 1.5f, 5);
        }

        private static BoundingSphereD FindBoundingSphere(MyObjectBuilder_CubeGrid[] grids)
        {

            Vector3? vector = null;
            float radius = 0F;

            Parallel.ForEach(grids, grid =>
            {

                var gridSphere = grid.CalculateBoundingSphere();
                
                /* If this is the first run, we use the center of that grid, and its radius as it is */
                if (vector == null)
                {

                    vector = gridSphere.Center;
                    radius = gridSphere.Radius;
                    return;
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

            });
            return new BoundingSphereD(vector.Value, radius);
        }

        private static MyOrientedBoundingBoxD FindBoundingBox(MyObjectBuilder_CubeGrid[] grids)
        {
            BoundingBox First = grids[0].CalculateBoundingBox();
            Parallel.ForEach(grids, grid =>
            {
                var GridBox = grid.CalculateBoundingBox();
                First.Include(ref GridBox);
            });

            return new MyOrientedBoundingBoxD(First, grids[0].PositionAndOrientation.Value.GetMatrix());
        }


        private bool UpdateGridsPosition(Vector3D newPosition)
        {

            bool firstGrid = true;
            double deltaX = 0;
            double deltaY = 0;
            double deltaZ = 0;

            Parallel.ForEach(_grids, grid =>
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
            });
            return true;
        }

        public void Increment(IMyEntity entity)
        {
            var grid = (IMyCubeGrid)entity;
            _spawned.Add(grid);

            if (_spawned.Count < _maxCount)
                return;

            foreach (MyCubeGrid g in _spawned)
            {
                MyAPIGateway.Entities.AddEntity(g, true);
            }

            _callback?.Invoke(_spawned);

        }



        /*  Align to gravity code.
         * 
         * 
         */

        private void EnableRequiredItemsOnLoad(MyObjectBuilder_CubeGrid[] _grid)
        {
            for (int i = 0; i < _grid.Count(); i++)
            {
                _grid[i].LinearVelocity = new SerializableVector3();
                _grid[i].AngularVelocity = new SerializableVector3();

                int counter = 0;
                foreach (MyObjectBuilder_Thrust Block in _grid[i].CubeBlocks.OfType<MyObjectBuilder_Thrust>())
                {
                    counter++;
                    Block.Enabled = true;
                }

                foreach (MyObjectBuilder_Reactor Block in _grid[i].CubeBlocks.OfType<MyObjectBuilder_Reactor>())
                {
                    Block.Enabled = true;
                }

                foreach (MyObjectBuilder_BatteryBlock Block in _grid[i].CubeBlocks.OfType<MyObjectBuilder_BatteryBlock>())
                {
                    Block.Enabled = true;
                    Block.SemiautoEnabled = true;
                    Block.ProducerEnabled = true;
                    Block.ChargeMode = 0;
                }

                _grid[i].DampenersEnabled = true;
            }

        }


        private void CalculateGridPosition(Vector3D Target)
        {

            List<MyObjectBuilder_CubeGrid> TotalGrids = new List<MyObjectBuilder_CubeGrid>();
            List<MyObjectBuilder_Cockpit> cockpits = new List<MyObjectBuilder_Cockpit>();
            Vector3D forwardVector = Vector3D.Zero;


            Hangar.Debug("Total Grids to be pasted: " + _grids.Count());

            //Attempt to get gravity/Artificial gravity to align the grids to


            //Here you can adjust the offset from the surface and rotation.
            //Unfortunatley we move the grid again after this to find a free space around the character. Perhaps later i can incorporate that into
            //LordTylus's existing grid checkplament method
            float gravityRotation = 0f;

            Vector3 gravityDirectionalVector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(Target);
            if (gravityDirectionalVector == Vector3.Zero)
            {
                gravityDirectionalVector = MyGravityProviderSystem.CalculateArtificialGravityInPoint(Target);
            }
            Vector3D upDirectionalVector;
            if (gravityDirectionalVector != Vector3.Zero)
            {
                Hangar.Debug("Attempting to correct grid orientation!");
                gravityDirectionalVector.Normalize();
                upDirectionalVector = -gravityDirectionalVector;

                if (forwardVector == Vector3D.Zero)
                {
                    forwardVector = Vector3D.CalculatePerpendicularVector(gravityDirectionalVector);
                    if (gravityRotation != 0f)
                    {
                        MatrixD matrixa = MatrixD.CreateFromAxisAngle(upDirectionalVector, gravityRotation);
                        forwardVector = Vector3D.Transform(forwardVector, matrixa);
                    }
                }
            }
            else if (forwardVector == Vector3D.Zero)
            {
                forwardVector = Vector3D.Right;
                upDirectionalVector = Vector3D.Up;
            }
            else
            {
                upDirectionalVector = Vector3D.CalculatePerpendicularVector(-forwardVector);
            }

            BeginAlignToGravity(_grids, Target, forwardVector, upDirectionalVector);
        }

        private void BeginAlignToGravity(MyObjectBuilder_CubeGrid[] AllGrids, Vector3D Target, Vector3D forwardVector, Vector3D upVector)
        {
            //Create WorldMatrix
            MatrixD worldMatrix = MatrixD.CreateWorld(Target, forwardVector, upVector);

            int num = 0;
            MatrixD referenceMatrix = MatrixD.Identity;
            MatrixD rotationMatrix = MatrixD.Identity;

            //Find biggest grid and get their postion matrix
            Parallel.For(0, AllGrids.Length, i =>
            {
                //Option to clone the BP
                //array[i] = (MyObjectBuilder_CubeGrid)TotalGrids[i].Clone();
                if (AllGrids[i].CubeBlocks.Count > num)
                {
                    num = AllGrids[i].CubeBlocks.Count;
                    referenceMatrix = AllGrids[i].PositionAndOrientation.Value.GetMatrix();
                    rotationMatrix = FindRotationMatrix(AllGrids[i]);
                }

            });

            //Huh? (Keen does this so i guess i will too) My guess so it can create large entities
            MyEntities.IgnoreMemoryLimits = true;

            //Update each grid in the array
            Parallel.For(0, AllGrids.Length, j =>
            {
                if (AllGrids[j].PositionAndOrientation.HasValue)
                {
                    MatrixD matrix3 = AllGrids[j].PositionAndOrientation.Value.GetMatrix() * MatrixD.Invert(referenceMatrix) * rotationMatrix;
                    AllGrids[j].PositionAndOrientation = new MyPositionAndOrientation(matrix3 * worldMatrix);
                }
                else
                {
                    AllGrids[j].PositionAndOrientation = new MyPositionAndOrientation(worldMatrix);
                }
            });
        }

        private MatrixD FindRotationMatrix(MyObjectBuilder_CubeGrid cubeGrid)
        {


            var resultMatrix = MatrixD.Identity;
            var cockpits = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_Cockpit>()
                .Where(blk =>
                {
                    return !(blk is MyObjectBuilder_CryoChamber)
                        && blk.SubtypeName.IndexOf("bathroom", StringComparison.InvariantCultureIgnoreCase) == -1;
                })
                .ToList();

            MyObjectBuilder_CubeBlock referenceBlock = cockpits.Find(blk => blk.IsMainCockpit) ?? cockpits.FirstOrDefault();




            if (referenceBlock == null)
            {
                var remoteControls = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_RemoteControl>().ToList();
                referenceBlock = remoteControls.Find(blk => blk.IsMainCockpit) ?? remoteControls.FirstOrDefault();


            }

            if (referenceBlock == null)
            {
                referenceBlock = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_LandingGear>().FirstOrDefault();
            }





            if (referenceBlock != null)
            {
                if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Right)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(-90));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Left)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(90));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Down)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(180));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Forward)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Left, MathHelper.ToRadians(-90));
                else if (referenceBlock.BlockOrientation.Up == Base6Directions.Direction.Backward)
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Left, MathHelper.ToRadians(90));
            }



            return resultMatrix;
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
