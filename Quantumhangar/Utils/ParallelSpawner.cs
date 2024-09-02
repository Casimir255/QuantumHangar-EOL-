using NLog;
using NLog.Targets.Wrappers;
using ParallelTasks;
using QuantumHangar.Utilities;
using QuantumHangar.Utils;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using Torch.Commands;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.Noise.Patterns;
using VRage.Utils;
using VRageMath;
using static QuantumHangar.Utils.CharacterUtilities;

namespace QuantumHangar
{
    public class ParallelSpawner
    {
        private static List<commandTimer> recentCommands = new List<commandTimer>();

        private class commandTimer
        {
            public long gridID;
            public int timer = 0;
            public ulong steamID;

            public commandTimer(ulong steam, long gridId) { 
                this.steamID= steam;
                this.gridID= gridId;
            }
        }



        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private ulong _playerSteamID;
        private readonly int _maxCount;
        private readonly int _spawnedCount;

        private readonly IEnumerable<MyObjectBuilder_CubeGrid> _grids;
        private readonly Action<HashSet<MyCubeGrid>> _callback;

        private readonly HashSet<MyCubeGrid> _spawned;
        private readonly Chat _response;
        private MyObjectBuilder_CubeGrid _biggestGrid;
        private PreviewBoxTimer shapeDisplay;
        private const int Timeout = 6000;
        public static Settings Config => Hangar.Config;


        //Bounds
        private BoundingSphereD _sphereD;
        private MyOrientedBoundingBoxD _boxD;
        private MatrixD _matrix;
        private BoundingBoxD _boxAab;
        private Vector3D _translation;

        //Delta
        private Vector3D _delta3D; //This should be a vector from the grids center, to that of the CENTER of the grid
        private Vector3D _targetPos = Vector3D.Zero;


        public ParallelSpawner(IEnumerable<MyObjectBuilder_CubeGrid> grids, Chat chat, ulong steamid, Action<HashSet<MyCubeGrid>> callback = null)
        {
            _grids = grids;
            _maxCount = grids.Count();
            _callback = callback;
            _spawned = new HashSet<MyCubeGrid>();
            _response = chat;
            _playerSteamID = steamid;

            shapeDisplay = new PreviewBoxTimer(steamid);
        }
        public void setBounds(MyOrientedBoundingBoxD boxD, BoundingBoxD _boxAab, Vector3D translation)
        {
            this._boxD = boxD;
            this._boxAab = _boxAab;
            this._translation = translation;
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





            //This should return more than a bool (we only need to run on game thread to find a safe spot)

            var spawn = GameEvents.InvokeAsync(CalculateSafePositionAndSpawn, loadInOriginalPosition);

       

            if (spawn.Wait(Timeout))
            {
                if (spawn.Result == false)
                    return false;

                //Remap to fix entity conflicts
                MyEntities.RemapObjectBuilderCollection(_grids);

                foreach (var o in _grids)
                {
                    IMyEntity entity = MyAPIGateway.Entities.CreateFromObjectBuilderParallel(o, false, Increment);
                }

                return true;
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
                bool foundPreviousAttempt = false;

                Log.Warn($"total Count: {recentCommands.Count}");

                //Get previous attempt


                commandTimer t = recentCommands.FirstOrDefault(x => x.steamID == _playerSteamID);
                if (t != null)
                {
                    Log.Warn($"Found record: {t.gridID} -> {_biggestGrid.EntityId}");
                    if (t.gridID == _biggestGrid.EntityId)
                        foundPreviousAttempt = true;
                    else
                        recentCommands.Remove(t);
                }


                FindGridBounds();


                //The center of the grid is not the actual center
                // Log.Info("SphereD Center: " + SphereD.Center);
                _delta3D = _sphereD.Center - _biggestGrid.PositionAndOrientation.Value.Position;
                //Log.Info("Delta: " + Delta3D);


                //This has to be ran on the main game thread!
                Log.Info($"KeepOriginalPos: {keepOriginalLocation}, Found Previous Attempt: {foundPreviousAttempt}");

                if (keepOriginalLocation)
                {
                    if (!OriginalSpotClear(foundPreviousAttempt))
                    {
                        //If the original spot is not clear and this is first time running, return false to display warning
                        if (!foundPreviousAttempt)
                        {
                            Log.Warn("Displaying spawn area!");
                            displaySpawnArea(_boxD);

                            //sends boxes to client
                            shapeDisplay.display();
                            recentCommands.Add(new commandTimer(_playerSteamID, _biggestGrid.EntityId));
                            return false;
                        }
                            

                        //Found previous attempt, and spot isnt clear still. Continue spawning around player

                    }
                    else
                    {
                        //If the original spot is clear, return true and spawn
                        if (Config.DigVoxels) 
                            DigVoxels();

                        PreviewBoxTimer.removeAll(_playerSteamID);
                        return true;
                    }
                }


                //This is for aligning to gravity. If we are trying to find a safe spot, lets check gravity, and if we did recalculate, lets re-calc grid bounds
                if (CalculateGridPosition(_targetPos))
                {
                    //We only need to align to gravity if a new spawn position is required
                    EnableRequiredItemsOnLoad();
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

                if (Config.DigVoxels) 
                    DigVoxels();


                //Remove the attempt after it found a good spot
                if (foundPreviousAttempt)
                    recentCommands.Remove(t);

                PreviewBoxTimer.removeAll(_playerSteamID);
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



            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(target);
            Vector3D closestSurfacePoint = planet.GetClosestSurfacePointGlobal(target);
            Vector3D planetCenter = planet.PositionComp.GetPosition();
            double Targetdistance = Vector3D.Distance(target, planetCenter);
            double lowestPointDistance = Vector3D.Distance(closestSurfacePoint, planetCenter);

            Log.Warn($"T{Targetdistance} L{lowestPointDistance}");
            if (Targetdistance < lowestPointDistance || Targetdistance - lowestPointDistance < 350)
            {
                return MyEntities.FindFreePlaceCustom(closestSurfacePoint, (float)_sphereD.Radius, 125, 15, 1.5f, 2.5f, null, true);
            }
          

            return MyEntities.FindFreePlaceCustom(target, (float)_sphereD.Radius, 125, 15, 1.5f, 5, null, true);
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

            //Legacy grid bounds calculated
            if (_boxD == null || _boxD.HalfExtent == Vector3D.Zero)
            {
                _boxAab = new BoundingBoxD();

                Log.Info("Null box!");
                var biggestGridMatrix = _biggestGrid.PositionAndOrientation.Value.GetMatrix();



                //Log.Warn($"F/UP: F {biggestGridMatrix.Forward}, U {biggestGridMatrix.Up}");
                //Log.Warn($"Orientation: X {_biggestGrid.PositionAndOrientation.Value.Orientation.X}, Y {_biggestGrid.PositionAndOrientation.Value.Orientation.Y}, Z {_biggestGrid.PositionAndOrientation.Value.Orientation.Z}, W {_biggestGrid.PositionAndOrientation.Value.Orientation.W}");

                _boxAab.Matrix.SetFrom(biggestGridMatrix);




                var corners = new Vector3D[8];
                foreach (var grid in _grids)
                {
                    BoundingBoxD box = grid.CalculateBoundingBox();
                    box.Matrix.SetFrom(grid.PositionAndOrientation.Value.GetMatrix());
                    _boxAab.Include(ref box);
                }


                _boxD = new MyOrientedBoundingBoxD(_boxAab, biggestGridMatrix);
            }



            var sphere = BoundingSphereD.CreateFromBoundingBox(_boxAab);
            _sphereD = new BoundingSphereD(_boxD.Center, sphere.Radius);
        }


        private bool OriginalSpotClear(bool foundPreviousAttempt)
        {
            var entities = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInOBB(ref _boxD, entities, MyEntityQueryType.Both);


            //We will use this to determine if its in console or not
            bool hasSteamID = (_response._context.Player != null);

            StringBuilder builder = new StringBuilder();
            GpsSender sender = new GpsSender();
            Color warningColor = new Color(255, 108, 0);
            int totalInterferences = 0;

            foreach (var entity in entities)
            {
                if (entity is MyCubeGrid grid)
                {
                    BoundingBox box = grid.PositionComp.LocalAABB;
                    var Matrix = grid.WorldMatrix;

                    //Double check for more percise control
                    MyOrientedBoundingBoxD gridBox = new MyOrientedBoundingBoxD(box, Matrix);
                    if (!gridBox.Intersects(ref _boxD))
                        continue;

                    string stringdesc = $"Grid {grid.DisplayName} interference";
                    builder.AppendLine(stringdesc);

                    //Log.Warn($"grid {grid.DisplayName} is inside the orientated bounding box!");
                    if (hasSteamID && !foundPreviousAttempt)
                    {
                        //sender.SendLinkedGPS(grid.PositionComp.GetPosition(), entity, stringdesc, _response._context.Player.IdentityId, 1, warningColor, "This grid interferes with spawn area!");
                        displaySpawnArea(entity);
                    }

                    totalInterferences++;


                }
                else if (entity is MyVoxelBase voxel && !_biggestGrid.IsStatic)
                {
                    string stringdesc = $"Voxel {voxel.DisplayName} interference";
                    builder.AppendLine(stringdesc);

                    if (hasSteamID && !foundPreviousAttempt)
                    {
                        //sender.SendLinkedGPS(voxel.PositionComp.GetPosition(), entity, stringdesc, _response._context.Player.IdentityId, 1, warningColor, "Beware this voxel interferes with spawn area!");
                        //displaySpawnArea(entity);
                    }
                        


                    //totalInterferences++;
                }
            }


            //If there is nothing interfering we are good to spawn
            if (totalInterferences == 0)
                return true;

            builder.AppendLine($"Total interferences: {totalInterferences}");

            if (!foundPreviousAttempt && hasSteamID)
                _response.Respond($"{totalInterferences} spawn interferences. Run command again in next 15s to spawn grid near you.");
            else if (!foundPreviousAttempt && !hasSteamID)
                _response.Respond(builder.ToString());

            return false;
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
            //confirm that the
            var grid = (MyCubeGrid)entity;
            _spawned.Add(grid);

            if (_spawned.Count < _maxCount)
                return;

            foreach (var g in _spawned)
                MyAPIGateway.Entities.AddEntity(g, true);

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



        private static int displayfor = 15;
        public static void update()
        {
            for (int i = recentCommands.Count - 1; i >= 0; i--)
            {
                if (recentCommands[i].timer > displayfor)
                {
                    recentCommands.RemoveAt(i);
                    continue;
                }

                recentCommands[i].timer++;
            }
        }





        public void displaySpawnArea(MyOrientedBoundingBoxD spawnArea)
        {
            
            //If response is null, its running as a console command
            if (_response._context == null)
                return;

 
            Color color = new Color(0, 255, 00, 10);
            shapeDisplay.drawobjectMessage.addOBB(_boxAab, _translation, spawnArea.Orientation.Forward, spawnArea.Orientation.Up, color, MySimpleObjectRasterizer.Wireframe, 1f, 0.005f);
            Log.Warn("Display spawn area!");
            //ModCommunication.SendMessageTo(sphere, _playerSteamID);
        }

        public void displaySpawnArea(MyEntity entity)
        {
            Color color = new Color(255, 0, 0, 10);
            shapeDisplay.drawobjectMessage.addOBBLinkedEntity(entity.EntityId, color, MySimpleObjectRasterizer.Wireframe, 1f, 0.005f);
            
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
        public readonly CommandContext _context;
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