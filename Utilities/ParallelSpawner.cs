using Havok;
using NLog;
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
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private readonly int _maxCount;
        private readonly IEnumerable<MyObjectBuilder_CubeGrid> _grids;
        private readonly Action<HashSet<MyCubeGrid>> _callback;
        private readonly HashSet<MyCubeGrid> _spawned;
        private readonly Chat _Response;
        private MyObjectBuilder_CubeGrid _BiggestGrid;
        private static int Timeout = 6000;


        //Bounds
        private BoundingSphereD SphereD;
        private MyOrientedBoundingBoxD BoxD;
        private BoundingBoxD BoxAAB = new BoundingBoxD();

        //Delta
        private Vector3D Delta3D; //This should be a vector from the grids center, to that of the CENTER of the grid
        private Vector3D TargetPos = Vector3D.Zero;


        public ParallelSpawner(IEnumerable<MyObjectBuilder_CubeGrid> grids, Chat chat, Action<HashSet<MyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Count();
            _callback = callback;
            _spawned = new HashSet<MyCubeGrid>();
            _Response = chat;
        }


        public bool Start(Vector3D Target, bool LoadInOriginalPosition = true)
        {
            TargetPos = Target;
            if (_grids.Count() == 0)
            {
                //Simple grid/objectbuilder null check. If there are no gridys then why continue?
                return true;
            }


            // Fix for recent keen update. (if grids have projected grids saved then they will get the infinite streaming bug)
            foreach (var cubeGrid in _grids)
            {
                cubeGrid.PlayerPresenceTier = MyUpdateTiersPlayerPresence.Normal;
                cubeGrid.CreatePhysics = true;

                // Set biggest grid in grid group
                if (_BiggestGrid == null || _BiggestGrid.CubeBlocks.Count < cubeGrid.CubeBlocks.Count)
                    _BiggestGrid = cubeGrid;

                foreach (var block in cubeGrid.CubeBlocks.OfType<MyObjectBuilder_Projector>())
                {
                    block.ProjectedGrid = null;
                    block.ProjectedGrids?.Clear();
                }
            }





            //Remap to fix entity conflicts
            MyEntities.RemapObjectBuilderCollection(_grids);


            //This should return more than a bool (we only need to run on game thread to find a safe spot)

            Task<bool> Spawn = GameEvents.InvokeAsync<bool, bool>(CalculateSafePositionAndSpawn, LoadInOriginalPosition);
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



        private bool CalculateSafePositionAndSpawn(bool keepOriginalLocation)
        {
            try
            {
                //Calculate all required grid bounding objects
                FindGridBounds();

                //The center of the grid is not the actual center
                // Log.Info("SphereD Center: " + SphereD.Center);
                Delta3D = SphereD.Center - _BiggestGrid.PositionAndOrientation.Value.Position;
                //Log.Info("Delta: " + Delta3D);


                //This has to be ran on the main game thread!
                if (keepOriginalLocation)
                {
                    //If the original spot is clear, return true and spawn
                    if (OriginalSpotClear())
                        return true;
                }


                //This is for aligning to gravity. If we are trying to find a safe spot, lets check gravity, and if we did recalculate, lets re-calc grid bounds
                if (CalculateGridPosition(TargetPos))
                {
                    //We only need to align to gravity if a new spawn position is required
                    EnableRequiredItemsOnLoad();

                    FindGridBounds();
                }


                //Find new spawn position either around character or last save (Target is specified on spawn call)
                var pos = FindPastePosition(TargetPos);
                if (!pos.HasValue)
                {
                    _Response.Respond("No free spawning zone found! Stopping load!");
                    return false;
                }

                // Update grid position
                TargetPos = pos.Value;
                UpdateGridsPosition(TargetPos);

                return true;

            }
            catch (Exception Ex)
            {
                Log.Fatal(Ex);
                return false;
            }
        }

        private Vector3D? FindPastePosition(Vector3D Target)
        {
            //Log.info($"BoundingSphereD: {SphereD.Center}, {SphereD.Radius}");
            //Log.info($"MyOrientedBoundingBoxD: {BoxD.Center}, {BoxD.GetAABB()}");

            return MyEntities.FindFreePlaceCustom(Target, (float)SphereD.Radius, 90, 10, 1.5f, 10);
        }


        private void FindGridBounds()
        {
            BoxAAB = new BoundingBoxD();
            BoxAAB.Include(_BiggestGrid.CalculateBoundingBox());

            var biggestGridWorldToLocal = MatrixD.Invert(_BiggestGrid.PositionAndOrientation.Value.GetMatrix());

            Vector3D[] corners = new Vector3D[8];
            foreach (var grid in _grids)
            {
                if (grid == _BiggestGrid) continue;
                var box = new BoundingBoxD();
                box.Include(grid.CalculateBoundingBox());
                var worldBox = new MyOrientedBoundingBoxD(box, grid.PositionAndOrientation.Value.GetMatrix());
                worldBox.Transform(biggestGridWorldToLocal);
                worldBox.GetCorners(corners, 0);
                foreach (var corner in corners)
                {
                    BoxAAB.Include(corner);
                }
            }

            BoundingSphereD Sphere = BoundingSphereD.CreateFromBoundingBox(BoxAAB);
            BoxD = new MyOrientedBoundingBoxD(BoxAAB, _BiggestGrid.PositionAndOrientation.Value.GetMatrix());
            SphereD = new BoundingSphereD(BoxD.Center, Sphere.Radius);

        }

        private bool OriginalSpotClear()
        {
            List<MyEntity> entities = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInOBB(ref BoxD, entities);

            bool SpotCleared = true;
            foreach (var entity in entities)
            {
                if (entity is MyCubeGrid)
                {

                    MyOrientedBoundingBoxD OBB = new MyOrientedBoundingBoxD(entity.PositionComp.LocalAABB, entity.WorldMatrix);

                    ContainmentType Type = BoxD.Contains(ref OBB);

                    //Log.Info($"{entity.DisplayName} Type: {Type.ToString()}");

                    if (Type == ContainmentType.Contains || Type == ContainmentType.Intersects)
                    {
                        SpotCleared = false;
                        _Response.Respond("There are potentially other grids in the way. Attempting to spawn around the location to avoid collisions.");
                        break;
                    }
                }
            }

            return SpotCleared;
        }



        private void UpdateGridsPosition(Vector3D TargetPos)
        {
            //Log.Info("New Grid Position: " + TargetPos);

            //Translated point
            TargetPos = TargetPos - Delta3D;


            //Now need to create a delta change from the initial position to the target position
            Vector3D Delta = TargetPos - _BiggestGrid.PositionAndOrientation.Value.Position;
            Parallel.ForEach(_grids, grid =>
            {
                Vector3D CurrentPos = grid.PositionAndOrientation.Value.Position;
                //MatrixD worldMatrix = MatrixD.CreateWorld(CurrentPos + Delta, grid.PositionAndOrientation.Value.Orientation.Forward, grid.PositionAndOrientation.Value.Orientation.Up,);
                grid.PositionAndOrientation = new MyPositionAndOrientation(CurrentPos + Delta, grid.PositionAndOrientation.Value.Orientation.Forward, grid.PositionAndOrientation.Value.Orientation.Up);

            });
        }

        public void Increment(IMyEntity entity)
        {
            var grid = (MyCubeGrid)entity;
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

        private void EnableRequiredItemsOnLoad()
        {
            //This really doesnt need to be ran on the game thread since we are still altering the grid before spawn

            Parallel.ForEach(_grids, grid =>
            {

                grid.LinearVelocity = new SerializableVector3();
                grid.AngularVelocity = new SerializableVector3();

                int counter = 0;
                foreach (MyObjectBuilder_Thrust Block in grid.CubeBlocks.OfType<MyObjectBuilder_Thrust>())
                {
                    counter++;
                    Block.Enabled = true;
                }

                foreach (MyObjectBuilder_Reactor Block in grid.CubeBlocks.OfType<MyObjectBuilder_Reactor>())
                {
                    Block.Enabled = true;
                }

                foreach (MyObjectBuilder_BatteryBlock Block in grid.CubeBlocks.OfType<MyObjectBuilder_BatteryBlock>())
                {
                    Block.Enabled = true;
                    Block.SemiautoEnabled = true;
                    Block.ProducerEnabled = true;
                    Block.ChargeMode = 0;
                }

                grid.DampenersEnabled = true;
            });

        }

        private bool CalculateGridPosition(Vector3D Target)
        {
            Vector3D forwardVector = Vector3D.Zero;


            //Hangar.Debug("Total Grids to be pasted: " + _grids.Count());

            //Attempt to get gravity/Artificial gravity to align the grids to


            //Here you can adjust the offset from the surface and rotation.
            //Unfortunatley we move the grid again after this to find a free space around the character. Perhaps later i can incorporate that into
            //LordTylus's existing grid checkplament method
            float gravityRotation = 0f;

            Vector3 gravityDirectionalVector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(Target);

            bool AllowAlignToNatrualGravity = false;
            if (AllowAlignToNatrualGravity && gravityDirectionalVector == Vector3.Zero)
            {
                gravityDirectionalVector = MyGravityProviderSystem.CalculateArtificialGravityInPoint(Target);
            }


            if (gravityDirectionalVector == Vector3.Zero)
                return false;


            //Calculate and apply grid rotation
            Vector3D upDirectionalVector;
            if (gravityDirectionalVector != Vector3.Zero)
            {
               
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

            BeginAlignToGravity(Target, forwardVector, upDirectionalVector);
            return true;

        }

        private void BeginAlignToGravity(Vector3D Target, Vector3D forwardVector, Vector3D upVector)
        {
            //Create WorldMatrix
            MatrixD worldMatrix = MatrixD.CreateWorld(Target, forwardVector, upVector);

            int num = 0;
            MatrixD referenceMatrix = MatrixD.Identity;
            MatrixD rotationMatrix = MatrixD.Identity;

            //Find biggest grid and get their postion matrix
            Parallel.ForEach(_grids, grid =>
            {
                //Option to clone the BP
                //array[i] = (MyObjectBuilder_CubeGrid)TotalGrids[i].Clone();
                if (grid.CubeBlocks.Count > num)
                {
                    num = grid.CubeBlocks.Count;
                    referenceMatrix = grid.PositionAndOrientation.Value.GetMatrix();
                    rotationMatrix = FindRotationMatrix(grid);
                }
            });

            //Huh? (Keen does this so i guess i will too) My guess so it can create large entities
            MyEntities.IgnoreMemoryLimits = true;

            //Update each grid in the array
            Parallel.ForEach(_grids, grid =>
            {
                if (grid.PositionAndOrientation.HasValue)
                {
                    MatrixD matrix3 = grid.PositionAndOrientation.Value.GetMatrix() * MatrixD.Invert(referenceMatrix) * rotationMatrix;
                    grid.PositionAndOrientation = new MyPositionAndOrientation(matrix3 * worldMatrix);
                }
                else
                {
                    grid.PositionAndOrientation = new MyPositionAndOrientation(worldMatrix);
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
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(Chat));
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

            //Log.Warn(response+" Mod: "+ _mod);
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
    }
}
