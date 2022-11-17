using NLog;
using QuantumHangar.Utilities;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using Torch.Commands;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
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
        private readonly Chat _response;
        private MyObjectBuilder_CubeGrid _biggestGrid;
        private const int Timeout = 6000;
        public static Settings Config => Hangar.Config;


        //Bounds
        private BoundingSphereD _sphereD;
        private MyOrientedBoundingBoxD _boxD;
        private BoundingBoxD _boxAab;

        //Delta
        private Vector3D _delta3D; //This should be a vector from the grids center, to that of the CENTER of the grid
        private Vector3D _targetPos = Vector3D.Zero;


        public ParallelSpawner(IEnumerable<MyObjectBuilder_CubeGrid> grids, Chat chat,
            Action<HashSet<MyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Count();
            _callback = callback;
            _spawned = new HashSet<MyCubeGrid>();
            _response = chat;
        }


        public bool Start(Vector3D target, bool loadInOriginalPosition = true)
        {
            _targetPos = target;
            if (!_grids.Any())
                //Simple grid/objectbuilder null check. If there are no gridys then why continue?
                return true;


            // Fix for recent keen update. (if grids have projected grids saved then they will get the infinite streaming bug)
            foreach (var cubeGrid in _grids)
            {
                //cubeGrid.PlayerPresenceTier = MyUpdateTiersPlayerPresence.Normal;
                cubeGrid.CreatePhysics = true;

                // Set biggest grid in grid group
                if (_biggestGrid == null || _biggestGrid.CubeBlocks.Count < cubeGrid.CubeBlocks.Count)
                    _biggestGrid = cubeGrid;

                foreach (var block in cubeGrid.CubeBlocks.OfType<MyObjectBuilder_Projector>())
                {
                    block.ProjectedGrid = null;
                    block.ProjectedGrids?.Clear();
                }
            }


            //Remap to fix entity conflicts
            MyEntities.RemapObjectBuilderCollection(_grids);


            //This should return more than a bool (we only need to run on game thread to find a safe spot)

            var spawn = GameEvents.InvokeAsync(CalculateSafePositionAndSpawn, loadInOriginalPosition);


            if (spawn.Wait(Timeout))
            {
                if (spawn.Result)
                {
                    foreach (var o in _grids)
                        MyAPIGateway.Entities.CreateFromObjectBuilderParallel(o, false, Increment);
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                _response.Respond("Parallel Grid Spawner Timed out!");
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
                _delta3D = _sphereD.Center - _biggestGrid.PositionAndOrientation.Value.Position;
                //Log.Info("Delta: " + Delta3D);


                //This has to be ran on the main game thread!
                if (keepOriginalLocation)
                    //If the original spot is clear, return true and spawn
                    if (OriginalSpotClear())
                    {
                        if (Config.DigVoxels) DigVoxels();
                        return true;
                    }


                //This is for aligning to gravity. If we are trying to find a safe spot, lets check gravity, and if we did recalculate, lets re-calc grid bounds
                if (CalculateGridPosition(_targetPos))
                {
                    //We only need to align to gravity if a new spawn position is required
                    EnableRequiredItemsOnLoad();

                    FindGridBounds();
                }


                //Find new spawn position either around character or last save (Target is specified on spawn call)
                var pos = FindPastePosition(_targetPos);
                if (!pos.HasValue)
                {
                    _response.Respond("No free spawning zone found! Stopping load!");
                    return false;
                }

                // Update grid position
                _targetPos = pos.Value;
                UpdateGridsPosition(_targetPos);

                if (Config.DigVoxels) DigVoxels();
                return true;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex);
                return false;
            }
        }

        // DigVoxels will dig voxels around the grids if necessary.
        //
        // Grids built inside dug out voxel (e.g. underground bases) have the problem that once saved
        // in hangar, voxels are likely to regenerate, and the grids cannot be loaded again without
        // exploding, requiring either admin assistance to dig it out, or give up on the grids.
        //
        // However it is not desirable to dig out all voxels around the grids unconditionally, grids placed
        // on voxels (e.g. surface bases) can load just fine and voxels under them should remain intact.
        //
        // Therefore the criteria is: if the grids center is inside voxels then dig voxels around, otherwise not.
        // The current digging strategy is simply a bounding box around it, aligned on the biggest grid and as
        // large as necessary for the whole grid to fit, plus an arbitrary 2 meters to fit a character standing.
        //
        // The grids center and the bounding box are determined by FindGridBounds.
        //
        // It is worth noting that this behavior can be used to dig out voxels for free, e.g. a long line of
        // blocks in each direction would result in a large cube dug out; or, in gravity a station grid that was
        // previously held by voxels would now appear to float in the air. These are definitely quirks but
        // not game breaking, as players can achieve the same result themselves.
        //
        // Needs to run in the game thread.
        private void DigVoxels()
        {
            var center = new BoundingBoxD(_sphereD.Center - 0.1f, _sphereD.Center + 0.1f);
            foreach (var voxel in MySession.Static.VoxelMaps.Instances)
            {
                if (voxel.StorageName == null) continue; // invalid voxel

                // ignore ghost asteroids: planets don't have physics linked directly to entity and
                // asteroids do have physics and they're disabled when in ghost placement mode, but not null
                if (!(voxel is MyPlanet) && (voxel.Physics == null || !voxel.Physics.Enabled)) continue;

                // before we check if it's inside voxel material (complex), there's a fast check
                // we can do: if it's not near the voxel map, then bail out
                if (!voxel.IsBoxIntersectingBoundingBoxOfThisVoxelMap(ref center)) continue;

                // dig only if the grids center is inside voxel material
                // this allows to leave untouched grids that are partially in voxels on purpose
                if (!voxel.IsAnyAabbCornerInside(ref MatrixD.Identity, center)) continue;

                // in practice, this is reached only once because voxel material cannot overlap

                var box = new BoundingBoxD(_boxAab.Min, _boxAab.Max);
                box.Inflate(2f); // just enough for one character height
                var shape = MyAPIGateway.Session.VoxelMaps.GetBoxVoxelHand();
                shape.Boundaries = box;
                shape.Transform = _biggestGrid.PositionAndOrientation.Value.GetMatrix();
                MyAPIGateway.Session.VoxelMaps.CutOutShape(voxel, shape);
            }
        }

        private Vector3D? FindPastePosition(Vector3D target)
        {
            //Log.info($"BoundingSphereD: {SphereD.Center}, {SphereD.Radius}");
            //Log.info($"MyOrientedBoundingBoxD: {BoxD.Center}, {BoxD.GetAABB()}");
            MyGravityProviderSystem.CalculateNaturalGravityInPoint(target, out var val);
            if (val == 0)
                //following method is what SEworldgen uses. We only really need to use this in space
                return FindSuitableJumpLocationSpace(target);

            return MyEntities.FindFreePlaceCustom(target, (float)_sphereD.Radius, 90, 10, 1.5f, 10);
        }


        private Vector3D? FindSuitableJumpLocationSpace(Vector3D desiredLocation)
        {
            var mObjectsInRange = new List<MyObjectSeed>();
            var mEntitiesInRange = new List<MyEntity>();


            var inflated = _sphereD;
            inflated.Radius *= 1.5;
            inflated.Center = desiredLocation;

            var vector3D = desiredLocation;

            MyProceduralWorldGenerator.Static.OverlapAllAsteroidSeedsInSphere(inflated, mObjectsInRange);
            var mObstaclesInRange = mObjectsInRange.Select(item3 => item3.BoundingVolume).ToList();
            mObjectsInRange.Clear();

            MyProceduralWorldGenerator.Static.GetAllInSphere<MyStationCellGenerator>(inflated, mObjectsInRange);
            mObstaclesInRange.AddRange(mObjectsInRange.Select(item4 => item4.UserData).OfType<MyStation>().Select(myStation => new BoundingBoxD(myStation.Position - MyStation.SAFEZONE_SIZE, myStation.Position + MyStation.SAFEZONE_SIZE)).Where(item => item.Contains(vector3D) != 0));

            mObjectsInRange.Clear();


            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref inflated, mEntitiesInRange);
            mObstaclesInRange.AddRange(from item5 in mEntitiesInRange where !(item5 is MyPlanet) select item5.PositionComp.WorldAABB.GetInflated(inflated.Radius));

            const int num = 10;
            var num2 = 0;
            BoundingBoxD? boundingBoxD = null;
            var flag2 = false;
            while (num2 < num)
            {
                num2++;
                var flag = false;
                foreach (var item6 in mObstaclesInRange
                             .Select(item6 => new { item6, containmentType = item6.Contains(vector3D) })
                             .Where(@t =>
                                 @t.containmentType == ContainmentType.Contains ||
                                 @t.containmentType == ContainmentType.Intersects)
                             .Select(@t => @t.item6))
                {
                    boundingBoxD ??= item6;
                    boundingBoxD = boundingBoxD.Value.Include(item6);
                    boundingBoxD = boundingBoxD.Value.Inflate(1.0);
                    vector3D = ClosestPointOnBounds(boundingBoxD.Value, vector3D);
                    flag = true;
                    break;
                }

                if (flag) continue;
                flag2 = true;
                break;
            }

            mObstaclesInRange.Clear();
            mEntitiesInRange.Clear();
            mObjectsInRange.Clear();
            if (flag2) return vector3D;
            return null;
        }

        private static Vector3D ClosestPointOnBounds(BoundingBoxD b, Vector3D p)
        {
            var vector3D = (p - b.Center) / b.HalfExtents;
            switch (vector3D.AbsMaxComponent())
            {
                case 0:
                    p.X = vector3D.X > 0.0 ? b.Max.X : b.Min.X;
                    break;
                case 1:
                    p.Y = vector3D.Y > 0.0 ? b.Max.Y : b.Min.Y;
                    break;
                case 2:
                    p.Z = vector3D.Z > 0.0 ? b.Max.Z : b.Min.Z;
                    break;
            }

            return p;
        }


        private void FindGridBounds()
        {
            _boxAab = new BoundingBoxD();
            _boxAab.Include(_biggestGrid.CalculateBoundingBox());

            var biggestGridMatrix = _biggestGrid.PositionAndOrientation.Value.GetMatrix();
            var biggestGridMatrixToLocal = MatrixD.Invert(biggestGridMatrix);


            var corners = new Vector3D[8];
            foreach (var grid in _grids)
            {
                if (grid == _biggestGrid)
                    continue;


                BoundingBoxD box = grid.CalculateBoundingBox();

                var worldBox = new MyOrientedBoundingBoxD(box, grid.PositionAndOrientation.Value.GetMatrix());
                worldBox.Transform(biggestGridMatrixToLocal);
                worldBox.GetCorners(corners, 0);

                foreach (var corner in corners) _boxAab.Include(corner);
            }

            var sphere = BoundingSphereD.CreateFromBoundingBox(_boxAab);
            _boxD = new MyOrientedBoundingBoxD(_boxAab, biggestGridMatrix);
            _sphereD = new BoundingSphereD(_boxD.Center, sphere.Radius);


            //Test bounds to make sure they are in the right spot

            /*

            long ID = MySession.Static.Players.TryGetIdentityId(76561198045096439);
            Vector3D[] array = new Vector3D[8];
            BoxD.GetCorners(array, 0);

            for (int i = 0; i <= 7; i++)
            {
                CharacterUtilities.SendGps(array[i], i.ToString(), ID, 10);
            }
            */

            //Log.Info($"HangarDebug: {BoxD.ToString()}");
        }

        private bool OriginalSpotClear()
        {
            var entities = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInOBB(ref _boxD, entities);

            var spotCleared = true;
            if (!entities.OfType<MyCubeGrid>()
                    .Select(entity => new MyOrientedBoundingBoxD(entity.PositionComp.LocalAABB, entity.WorldMatrix))
                    .Select(obb => _boxD.Contains(ref obb))
                    .Any(type => type == ContainmentType.Contains || type == ContainmentType.Intersects))
                return spotCleared;
            spotCleared = false;
            _response.Respond(
                "There are potentially other grids in the way. Attempting to spawn around the location to avoid collisions.");

            return spotCleared;
        }


        private void UpdateGridsPosition(Vector3D targetPos)
        {
            //Log.Info("New Grid Position: " + TargetPos);

            //Translated point
            targetPos = targetPos - _delta3D;


            //Now need to create a delta change from the initial position to the target position
            var delta = targetPos - _biggestGrid.PositionAndOrientation.Value.Position;
            ParallelTasks.Parallel.ForEach(_grids, grid =>
            {
                Vector3D currentPos = grid.PositionAndOrientation.Value.Position;
                //MatrixD worldMatrix = MatrixD.CreateWorld(CurrentPos + Delta, grid.PositionAndOrientation.Value.Orientation.Forward, grid.PositionAndOrientation.Value.Orientation.Up,);
                grid.PositionAndOrientation = new MyPositionAndOrientation(currentPos + delta,
                    grid.PositionAndOrientation.Value.Orientation.Forward,
                    grid.PositionAndOrientation.Value.Orientation.Up);
            });
        }

        public void Increment(IMyEntity entity)
        {
            var grid = (MyCubeGrid)entity;
            _spawned.Add(grid);

            if (_spawned.Count < _maxCount)
                return;

            foreach (var g in _spawned) MyAPIGateway.Entities.AddEntity(g, true);

            _callback?.Invoke(_spawned);
        }


        /*  Align to gravity code.
         * 
         * 
         */

        private void EnableRequiredItemsOnLoad()
        {
            //This really doesnt need to be ran on the game thread since we are still altering the grid before spawn

            ParallelTasks.Parallel.ForEach(_grids, grid =>
            {
                grid.LinearVelocity = new SerializableVector3();
                grid.AngularVelocity = new SerializableVector3();

                var counter = 0;
                foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_Thrust>())
                {
                    counter++;
                    block.Enabled = true;
                }

                foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_Reactor>()) block.Enabled = true;

                foreach (var block in grid.CubeBlocks.OfType<MyObjectBuilder_BatteryBlock>())
                {
                    block.Enabled = true;
                    block.SemiautoEnabled = true;
                    block.ProducerEnabled = true;
                    block.ChargeMode = 0;
                }

                grid.DampenersEnabled = true;
            });
        }

        private bool CalculateGridPosition(Vector3D target)
        {
            var forwardVector = Vector3D.Zero;


            //Hangar.Debug("Total Grids to be pasted: " + _grids.Count());

            //Attempt to get gravity/Artificial gravity to align the grids to


            //Here you can adjust the offset from the surface and rotation.
            //Unfortunatley we move the grid again after this to find a free space around the character. Perhaps later i can incorporate that into
            //LordTylus's existing grid checkplament method
            var gravityRotation = 0f;

            var gravityDirectionalVector = MyGravityProviderSystem.CalculateNaturalGravityInPoint(target);
            
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
                        var matrixA = MatrixD.CreateFromAxisAngle(upDirectionalVector, gravityRotation);
                        forwardVector = Vector3D.Transform(forwardVector, matrixA);
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

            BeginAlignToGravity(target, forwardVector, upDirectionalVector);
            return true;
        }

        private void BeginAlignToGravity(Vector3D target, Vector3D forwardVector, Vector3D upVector)
        {
            //Create WorldMatrix
            var worldMatrix = MatrixD.CreateWorld(target, forwardVector, upVector);

            var num = 0;
            var referenceMatrix = MatrixD.Identity;
            var rotationMatrix = MatrixD.Identity;

            //Find biggest grid and get their postion matrix
            ParallelTasks.Parallel.ForEach(_grids, grid =>
            {
                //Option to clone the BP
                //array[i] = (MyObjectBuilder_CubeGrid)TotalGrids[i].Clone();
                if (grid.CubeBlocks.Count <= num) return;
                num = grid.CubeBlocks.Count;
                referenceMatrix = grid.PositionAndOrientation.Value.GetMatrix();
                rotationMatrix = FindRotationMatrix(grid);
            });

            //Huh? (Keen does this so i guess i will too) My guess so it can create large entities
            MyEntities.IgnoreMemoryLimits = true;

            //Update each grid in the array
            ParallelTasks.Parallel.ForEach(_grids, grid =>
            {
                if (grid.PositionAndOrientation.HasValue)
                {
                    var matrix3 = grid.PositionAndOrientation.Value.GetMatrix() * MatrixD.Invert(referenceMatrix) *
                                  rotationMatrix;
                    grid.PositionAndOrientation = new MyPositionAndOrientation(matrix3 * worldMatrix);
                }
                else
                {
                    grid.PositionAndOrientation = new MyPositionAndOrientation(worldMatrix);
                }
            });
        }

        public static MatrixD FindRotationMatrix(MyObjectBuilder_CubeGrid cubeGrid)
        {
            var resultMatrix = MatrixD.Identity;
            var cockpits = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_Cockpit>()
                .Where(blk => !(blk is MyObjectBuilder_CryoChamber) && blk.SubtypeName.IndexOf("bathroom", StringComparison.InvariantCultureIgnoreCase) == -1)
                .ToList();

            MyObjectBuilder_CubeBlock referenceBlock =
                cockpits.Find(blk => blk.IsMainCockpit) ?? cockpits.FirstOrDefault();


            if (referenceBlock == null)
            {
                var remoteControls = cubeGrid.CubeBlocks.OfType<MyObjectBuilder_RemoteControl>().ToList();
                referenceBlock = remoteControls.Find(blk => blk.IsMainCockpit) ?? remoteControls.FirstOrDefault();
            }

            referenceBlock ??= cubeGrid.CubeBlocks.OfType<MyObjectBuilder_LandingGear>().FirstOrDefault();


            if (referenceBlock == null) return resultMatrix;
            switch (referenceBlock.BlockOrientation.Up)
            {
                case Base6Directions.Direction.Right:
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(-90));
                    break;
                case Base6Directions.Direction.Left:
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(90));
                    break;
                case Base6Directions.Direction.Down:
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Forward, MathHelper.ToRadians(180));
                    break;
                case Base6Directions.Direction.Forward:
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Left, MathHelper.ToRadians(-90));
                    break;
                case Base6Directions.Direction.Backward:
                    resultMatrix *= MatrixD.CreateFromAxisAngle(Vector3D.Left, MathHelper.ToRadians(90));
                    break;
            }


            return resultMatrix;
        }
    }


    // Simple chat class so i can control the colors easily.
    public class Chat
    {
        private readonly CommandContext _context;
        private readonly bool _mod;
        private readonly Action<string, Color, string> _send;
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(Chat));
        private static readonly string Author = "Hangar";
        private static readonly Color ChatColor = Color.Yellow;


        // Respond to a command.
        public Chat(CommandContext context, bool mod = false)
        {
            _context = context;
            _mod = mod;
        }

        // Respond without a command, delegate how to send the response.
        public Chat(Action<string, Color, string> sender)
        {
            _send = sender;
        }

        public void Respond(string response)
        {
            //Log.Warn(response+" Mod: "+ _mod);
            if (_mod)
            {
                if (_context == null)
                    return;

                //Should fix admin commands
                _context.Respond(response);
            }
            else
            {
                Send(response, ChatColor, Author);
            }
        }

        private void Send(string response, Color color = default, string sender = null)
        {
            if (_context != null)
            {
                _context.Respond(response, color, sender);
                return;
            }

            _send?.Invoke(response, color, sender);
            return;
        }

        public static void Send(string response, ulong target)
        {
            var scripted = new ScriptedChatMsg()
            {
                Author = Author,
                Text = response,
                Font = MyFontEnum.White,
                Color = ChatColor,
                Target = Sync.Players.TryGetIdentityId(target)
            };

            Log.Info($"{Author} (to {Torch.Managers.ChatManager.ChatManagerServer.GetMemberName(target)}): {response}");
            MyMultiplayerBase.SendScriptedChatMessage(ref scripted);
        }
    }
}