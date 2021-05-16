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
using Sandbox.Game.World.Generator;
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

        //Rexxars spicy ParallelSpawner
        private readonly int _maxCount;
        private readonly IEnumerable<MyObjectBuilder_CubeGrid> _grids;
        private readonly Action<HashSet<MyCubeGrid>> _callback;
        private readonly HashSet<MyCubeGrid> _spawned;
        private readonly Chat _Response;
        private static int Timeout = 20000;
        public bool _AlignToGravity = false;
        private Vector3D SpawnPosition;

        public ParallelSpawner(IEnumerable<MyObjectBuilder_CubeGrid> grids, Chat chat, bool AlignToGravity = false, Action<HashSet<MyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Count();
            _callback = callback;
            _spawned = new HashSet<MyCubeGrid>();
            _Response = chat;
            _AlignToGravity = AlignToGravity;
        }

        public bool Start(bool LoadInOriginalPosition, Vector3D Target)
        {
            if (_grids.Count() == 0)
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

                        o.PlayerPresenceTier = MyUpdateTiersPlayerPresence.Normal;
                        o.CreatePhysics = true;
                        o.EntityId = 0;


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
                // Run the keepOriginal position check with orientated bounding box

                var BoundingBox = FindBoundingBox(_grids);
                List<MyEntity> entities = new List<MyEntity>();


                // So for some reason the following check is not exact. Just close. so we have to go in and double check if we want exact results
                MyGamePruningStructure.GetAllEntitiesInOBB(ref BoundingBox, entities);

                bool PotentialGrids = false;
                foreach (var entity in entities)
                {
                    if (entity is MyCubeGrid)
                    {
                        MyCubeGrid Grid = entity as MyCubeGrid;
                        var GridBox = new MyOrientedBoundingBoxD(Grid.PositionComp.LocalAABB, Grid.PositionComp.WorldMatrixRef);
                        //Log.Info($"{entity.DisplayName}");
                        if (BoundingBox.Intersects(ref GridBox))
                        {
                            PotentialGrids = true;
                            _Response.Respond("There are potentially other grids in the way. Attempting to spawn around the location to avoid collisions.");
                            break;
                        }
                    }
                }

                //If there are no potential grids goahead and spawn the grid in
                if (!PotentialGrids)
                    return true;
            }


            Vector3D? SpawnPos = null;

            /* Where do we want to paste the grids? Lets find out. */
            Vector3 gravityDirectionalVector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(Target);
            if (gravityDirectionalVector.LengthSquared() > Vector3.Zero.LengthSquared())
            {
                //Gravity spawning... Gotta do this the old fasion way
                SpawnPos = FindPastePosition(Target);

            }
            else
            {
                //Mainly for space
                SpawnPos = FindNewSpawnPosition(Target);
            }


            if (SpawnPos == null || !SpawnPos.HasValue)
            {
                _Response.Respond("No free spawning zone found! Stopping load!");
                return false;
            }


            /* Update GridsPosition if that doesnt work get out of here. */
            if (!UpdateGridsPosition(SpawnPos.Value))
            {
                _Response.Respond("The File to be imported does not seem to be compatible with the server!");
                return false;
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

            return MyEntities.FindFreePlaceCustom(Target, (float)sphere.Radius, 90, 10, 1.5f, 10);
        }

        private static BoundingSphereD FindBoundingSphere(IEnumerable<MyObjectBuilder_CubeGrid> grids)
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

        private Vector3D? FindNewSpawnPosition(Vector3D Target)
        {
            BoundingBoxD physicalGroupAABB = FindBoundingBox(_grids).GetAABB();
            physicalGroupAABB.Inflate(50.0);

            BoundingBoxD box = physicalGroupAABB.GetInflated(physicalGroupAABB.HalfExtents * 10.0);
            box.Translate(Target - box.Center);

            List<MyObjectSeed> m_objectsInRange = new List<MyObjectSeed>();
            List<BoundingBoxD> m_obstaclesInRange = new List<BoundingBoxD>();
            List<MyEntity> m_entitiesInRange = new List<MyEntity>();
            Vector3D vector3D = Target;



            m_objectsInRange.Clear();
            MyProceduralWorldGenerator.Static.OverlapAllAsteroidSeedsInSphere(new BoundingSphereD(box.Center, box.HalfExtents.AbsMax()), m_objectsInRange);
            foreach (MyObjectSeed item3 in m_objectsInRange)
            {
                m_obstaclesInRange.Add(item3.BoundingVolume);
            }
            m_objectsInRange.Clear();


            m_objectsInRange.Clear();
            MyProceduralWorldGenerator.Static.GetAllInSphere<MyStationCellGenerator>(new BoundingSphereD(box.Center, box.HalfExtents.AbsMax()), m_objectsInRange);
            foreach (MyObjectSeed item4 in m_objectsInRange)
            {
                MyStation myStation = item4.UserData as MyStation;
                if (myStation != null)
                {
                    BoundingBoxD item = new BoundingBoxD(myStation.Position - MyStation.SAFEZONE_SIZE, myStation.Position + MyStation.SAFEZONE_SIZE);
                    if (item.Contains(vector3D) != 0)
                    {
                        m_obstaclesInRange.Add(item);
                    }
                }
            }


            m_objectsInRange.Clear();
            MyGamePruningStructure.GetTopMostEntitiesInBox(ref box, m_entitiesInRange);
            foreach (MyEntity item5 in m_entitiesInRange)
            {
                if (!(item5 is MyPlanet))
                {
                    m_obstaclesInRange.Add(item5.PositionComp.WorldAABB.GetInflated(physicalGroupAABB.HalfExtents));
                }
            }

            int num = 35;
            int num2 = 0;
            BoundingBoxD? boundingBoxD = null;

            //Flag: True if invalid spawn
            bool flag = false;
            bool flag2 = false;
            while (num2 < num)
            {
                num2++;
                flag = false;
                foreach (BoundingBoxD item6 in m_obstaclesInRange)
                {
                    ContainmentType containmentType = item6.Contains(vector3D);
                    if (containmentType == ContainmentType.Contains || containmentType == ContainmentType.Intersects)
                    {
                        if (!boundingBoxD.HasValue)
                        {
                            boundingBoxD = item6;
                        }
                        boundingBoxD = boundingBoxD.Value.Include(item6);
                        boundingBoxD = boundingBoxD.Value.Inflate(1.0);
                        vector3D = ClosestPointOnBounds(boundingBoxD.Value, vector3D);


                        flag = true;
                        break;
                    }
                }


                if (!flag)
                {
                    flag2 = true;
                    break;
                }
            }


            m_obstaclesInRange.Clear();
            m_entitiesInRange.Clear();
            m_objectsInRange.Clear();

            if (flag2)
            {
                //GetClosestSurfacePointGlobal








                SpawnPosition = vector3D;
                return vector3D;
            }

            return null;


        }

        private Vector3D ClosestPointOnBounds(BoundingBoxD b, Vector3D p)
        {
            Vector3D vector3D = (p - b.Center) / b.HalfExtents;
            switch (vector3D.AbsMaxComponent())
            {
                case 0:
                    if (vector3D.X > 0.0)
                    {
                        p.X = b.Max.X;
                    }
                    else
                    {
                        p.X = b.Min.X;
                    }
                    break;
                case 1:
                    if (vector3D.Y > 0.0)
                    {
                        p.Y = b.Max.Y;
                    }
                    else
                    {
                        p.Y = b.Min.Y;
                    }
                    break;
                case 2:
                    if (vector3D.Z > 0.0)
                    {
                        p.Z = b.Max.Z;
                    }
                    else
                    {
                        p.Z = b.Min.Z;
                    }
                    break;
            }
            return p;
        }

        private static MyOrientedBoundingBoxD FindBoundingBox(IEnumerable<MyObjectBuilder_CubeGrid> grids)
        {
            BoundingBox First = grids.First().CalculateBoundingBox();
            Parallel.ForEach(grids, grid =>
            {
                var GridBox = grid.CalculateBoundingBox();
                First.Include(ref GridBox);
            });

            return new MyOrientedBoundingBoxD(First, grids.First().PositionAndOrientation.Value.GetMatrix());
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
            var grid = (MyCubeGrid)entity;
            _spawned.Add(grid);

            if (_spawned.Count < _maxCount)
                return;

            foreach (MyCubeGrid g in _spawned)
            {
                MyAPIGateway.Entities.AddEntity(g, true);
            }


            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                _callback?.Invoke(_spawned);
            });

        }








        /*  Align to gravity code.
         * 
         * 
         */

        private void EnableRequiredItemsOnLoad(IEnumerable<MyObjectBuilder_CubeGrid> _grid)
        {
            foreach (var grid in _grid)
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
            }

        }

        private void CalculateGridPosition(Vector3D Target)
        {

            List<MyObjectBuilder_CubeGrid> TotalGrids = new List<MyObjectBuilder_CubeGrid>();
            List<MyObjectBuilder_Cockpit> cockpits = new List<MyObjectBuilder_Cockpit>();
            Vector3D forwardVector = Vector3D.Zero;


            //Hangar.Debug("Total Grids to be pasted: " + _grids.Count());

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
                //Hangar.Debug("Attempting to correct grid orientation!");
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

        private void BeginAlignToGravity(IEnumerable<MyObjectBuilder_CubeGrid> AllGrids, Vector3D Target, Vector3D forwardVector, Vector3D upVector)
        {
            //Create WorldMatrix
            MatrixD worldMatrix = MatrixD.CreateWorld(Target, forwardVector, upVector);

            int num = 0;
            MatrixD referenceMatrix = MatrixD.Identity;
            MatrixD rotationMatrix = MatrixD.Identity;

            //Find biggest grid and get their postion matrix
            Parallel.ForEach(AllGrids, grid =>
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
            Parallel.ForEach(AllGrids, grid =>
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
