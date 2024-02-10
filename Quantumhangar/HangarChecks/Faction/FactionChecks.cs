using NLog;
using QuantumHangar.HangarMarket;
using QuantumHangar.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.GameSystems;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Torch.Commands;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using static QuantumHangar.Utils.CharacterUtilities;

namespace QuantumHangar.HangarChecks
{
    //This is when a normal player runs hangar commands
    public class FactionChecks
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Chat _chat;
        private readonly GpsSender _gpsSender;
        public readonly ulong SteamId;
        private readonly long _identityId;
        private readonly Vector3D _playerPosition;
        private readonly MyCharacter _userCharacter;

        private FactionHanger FactionsHanger { get; set; }

        public static Settings Config => Hangar.Config;


        // PlayerChecks as initiated by another server to call LoadGrid.
        // We don't have a command context nor a player character object to work with,
        // but we receive all required data in the Nexus message.
        public FactionChecks(Chat chat, GpsSender gpsSender, ulong steamId, long identityId, Vector3D playerPosition)
        {
            _chat = chat;
            _gpsSender = gpsSender;
            SteamId = steamId;
            _identityId = identityId;
            _playerPosition = playerPosition;
            // UserCharacter can remain null, it is only used by SaveGrid.
        }

        public FactionChecks(CommandContext context)
        {
            _chat = new Chat(context);
            _gpsSender = new GpsSender();
            SteamId = context.Player.SteamUserId;
            _identityId = context.Player.Identity.IdentityId;
            _playerPosition = context.Player.GetPosition();
            _userCharacter = (MyCharacter)context.Player.Character;
        }

        private bool PerformMainChecks(bool isSaving)
        {
            if (!Config.PluginEnabled)
            {
                _chat?.Respond("Plugin is not enabled!");
                return false;
            }
            var faction = MySession.Static.Factions.GetPlayerFaction(_identityId);
            if (faction == null)
            {
                _chat?.Respond("Players without a faction cannot use faction hangar!");
                return false;
            }

            if (FactionHanger.IsServerSaving(_chat))
            {
                _chat?.Respond("Server is saving or is paused!");
                return false;
            }

            if (!CheckZoneRestrictions(isSaving))
            {
                _chat?.Respond("You are not in the right zone!");
                return false;
            }

            if (!CheckGravity())
            {
                _chat?.Respond("Unable to perform this action in gravity!");
                return false;
            }

            FactionsHanger = new FactionHanger(SteamId, _chat);
            if (!faction.IsFounder(_identityId) && !faction.IsLeader(_identityId) &&
                !FactionsHanger.IsWhitelisted(SteamId))
            {
                _chat?.Respond("Only leaders, founders or players whitelisted with !fh whitelist");
                return false;
            }
            if (FactionsHanger.CheckPlayerTimeStamp()) return true;
            _chat?.Respond("Command cooldown is still in affect!");
            return false;


            /*
            if (CheckEnemyDistance(LoadingAtSave, PlayerPosition))
                return false;
            */

        }

        public async void ChangeWebhook(string webhook)
        {
            var faction = MySession.Static.Factions.GetPlayerFaction(_identityId);
            if (faction == null)
            {
                _chat?.Respond("Players without a faction cannot use faction hangar!");
                return;
            }
            if (!faction.IsFounder(_identityId))
            {
                _chat?.Respond("Only the founder can edit the webhook");
                return;
            }
            FactionsHanger = new FactionHanger(SteamId, _chat);
            FactionsHanger.ChangeWebhook(webhook);
            _chat?.Respond("Webhook changed");
        }

        public async void ChangeWhitelist(string targetNameOrSteamId)
        {
            var faction = MySession.Static.Factions.GetPlayerFaction(_identityId);
            if (faction == null)
            {
                _chat?.Respond("Players without a faction cannot use faction hangar!");
                return;
            }
            if (!faction.IsFounder(_identityId) && !faction.IsLeader(_identityId))
            {
                _chat?.Respond("Only the founder or leader can edit the whitelist");
                return;
            }
            if (CharacterUtilities.TryGetPlayerSteamId(targetNameOrSteamId, _chat, out ulong steamId))
            {
                FactionsHanger = new FactionHanger(SteamId, _chat);

                var result = FactionsHanger.ChangeWhitelist(steamId);
                if (result)
                {
                    _chat?.Respond("Player added to whitelist.");
                    return;
                }
                _chat?.Respond("Player removed from whitelist.");
            }
            else
            {
                _chat?.Respond("Couldn't find that player.");
            }
        }

        public async void SaveGrid()
        {
            if (!PerformMainChecks(true))
                return;

            if (!FactionsHanger.CheckHangarLimits())
                return;


            var result = new GridResult();
            //Gets grids player is looking at
            if (!result.GetGrids(_chat, _userCharacter, null, FactionsHanger.FactionId))
                return;

            if (IsAnyGridInsideSafeZone(result))
                return;


            //Calculates incoming grids data
            var gridData = result.GenerateGridStamp();


            //PlayersHanger.CheckGridLimits(GridData);

            //Checks for single and all slot block and grid limits
            if (!FactionsHanger.ExtensiveLimitChecker(gridData))
                return;


            if (!CheckEnemyDistance(Config.LoadType, _playerPosition))
                return;


            if (!RequireSaveCurrency(result))
                return;
            
            //cooldown
            FactionsHanger.SelectedFactionFile.LoadFile(FactionsHanger.FactionFolderPath, (ulong)FactionsHanger.FactionId);
            if (!FactionsHanger.CheckPlayerTimeStamp())
            {
                _chat?.Respond("Command cooldown is still in affect!");
                return;
            }
            
            var st = new TimeStamp
            {
                OldTime = DateTime.Now
            };
            FactionsHanger.SelectedFactionFile.Timer = st;
            FactionsHanger.SelectedFactionFile.SaveFile();


            FactionsHanger.SelectedFactionFile.FormatGridName(gridData);

            var val = await FactionsHanger.SaveGridsToFile(result, gridData.GridName, _identityId);
            if (val)
            {
                FactionsHanger.SaveGridStamp(gridData);
                _chat?.Respond("Save Complete!");
                FactionsHanger.SendWebHookMessage($"{_userCharacter?.DisplayNameText ?? "Name not found"} {SteamId} saved grid {gridData.GridName}");
            }
            else
            {
                _chat?.Respond("Saved Failed!");
            }
        }

        private bool RequireSaveCurrency(GridResult result)
        {
            //MyObjectBuilder_Definitions(MyParticlesManager)
            if (!Config.RequireCurrency)
            {
                return true;
            }
            else
            {
                long saveCost = 0;
                switch (Config.HangarSaveCostType)
                {
                    case CostType.BlockCount:

                        foreach (var grid in result.Grids)
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                    //If grid is station
                                    saveCost += Convert.ToInt64(grid.BlocksCount * Config.CustomStaticGridCurrency);
                                else
                                    //If grid is large grid
                                    saveCost += Convert.ToInt64(grid.BlocksCount * Config.CustomLargeGridCurrency);
                            }
                            else
                            {
                                //if its a small grid
                                saveCost += Convert.ToInt64(grid.BlocksCount * Config.CustomSmallGridCurrency);
                            }

                        //Multiply by 
                        break;


                    case CostType.Fixed:

                        saveCost = Convert.ToInt64(Config.CustomStaticGridCurrency);

                        break;


                    case CostType.PerGrid:

                        foreach (var grid in result.Grids)
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                    //If grid is station
                                    saveCost += Convert.ToInt64(Config.CustomStaticGridCurrency);
                                else
                                    //If grid is large grid
                                    saveCost += Convert.ToInt64(Config.CustomLargeGridCurrency);
                            }
                            else
                            {
                                //if its a small grid
                                saveCost += Convert.ToInt64(Config.CustomSmallGridCurrency);
                            }

                        break;
                }

                TryGetPlayerBalance(out var balance);


                if (balance >= saveCost)
                {
                    //Check command status!
                    var command = result.BiggestGrid.DisplayName;
                    var confirmationCooldownMap = Hangar.ConfirmationsMap;
                    if (confirmationCooldownMap.TryGetValue(_identityId, out var confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Confirmed command! Update player balance!
                            confirmationCooldownMap.Remove(_identityId);
                            _chat?.Respond("Confirmed! Saving grid!");

                            ChangeBalance(-1 * saveCost);
                            return true;
                        }
                        _chat?.Respond("Saving this grid in your hangar will cost " + saveCost +
                                      " SC. Run this command again within 30 secs to continue!");
                        confirmationCooldown.StartCooldown(command);
                        return false;
                    }
                    _chat?.Respond("Saving this grid in your hangar will cost " + saveCost +
                                  " SC. Run this command again within 30 secs to continue!");
                    confirmationCooldown = new CurrentCooldown();
                    confirmationCooldown.StartCooldown(command);
                    confirmationCooldownMap.Add(_identityId, confirmationCooldown);
                    return false;
                }
                else
                {
                    var required = saveCost - balance;
                    _chat?.Respond("You need an additional " + required + " SC to perform this action!");
                    return false;
                }
            }
        }

        private bool RequireLoadCurrency(GridStamp grid)
        {
            //MyObjectBuilder_Definitions(MyParticlesManager)
            if (!Config.RequireLoadCurrency)
            {
                return true;
            }
            else
            {
                long loadCost = 0;
                switch (Config.HangarLoadCostType)
                {
                    case CostType.BlockCount:
                        //If grid is station
                        loadCost = Convert.ToInt64(grid.NumberOfBlocks * Config.LoadStaticGridCurrency);
                        break;


                    case CostType.Fixed:

                        loadCost = Convert.ToInt64(Config.LoadStaticGridCurrency);

                        break;


                    case CostType.PerGrid:

                        loadCost += Convert.ToInt64(grid.StaticGrids * Config.LoadStaticGridCurrency);
                        loadCost += Convert.ToInt64(grid.LargeGrids * Config.LoadLargeGridCurrency);
                        loadCost += Convert.ToInt64(grid.SmallGrids * Config.LoadSmallGridCurrency);


                        break;
                }

                TryGetPlayerBalance(out var balance);


                if (balance >= loadCost)
                {
                    //Check command status!
                    var command = grid.GridName;
                    var confirmationCooldownMap = Hangar.ConfirmationsMap;
                    if (confirmationCooldownMap.TryGetValue(_identityId, out var confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Confirmed command! Update player balance!
                            confirmationCooldownMap.Remove(_identityId);
                            _chat?.Respond("Confirmed! Loading grid!");

                            ChangeBalance(-1 * loadCost);
                            return true;
                        }
                        else
                        {
                            _chat?.Respond("Loading this grid will cost " + loadCost +
                                          " SC. Run this command again within 30 secs to continue!");
                            confirmationCooldown.StartCooldown(command);
                            return false;
                        }
                    }
                    else
                    {
                        _chat?.Respond("Loading this grid will cost " + loadCost +
                                      " SC. Run this command again within 30 secs to continue!");
                        confirmationCooldown = new CurrentCooldown();
                        confirmationCooldown.StartCooldown(command);
                        confirmationCooldownMap.Add(_identityId, confirmationCooldown);
                        return false;
                    }
                }
                else
                {
                    var required = loadCost - balance;
                    _chat?.Respond("You need an additional " + required + " SC to perform this action!");
                    return false;
                }
            }
        }
        public void ListWhitelist()
        {
            var faction = MySession.Static.Factions.GetPlayerFaction(_identityId);
            if (faction == null)
            {
                _chat?.Respond("Only members of a faction can use faction hangar.");
                return;
            }
            FactionsHanger = new FactionHanger(SteamId, _chat);
            FactionsHanger.ListAllWhitelisted();
        }
        public void ListGrids()
        {
            var faction = MySession.Static.Factions.GetPlayerFaction(_identityId);
            if (faction == null)
            {
                _chat?.Respond("Only members of a faction can use faction hangar.");
                return;
            }
            FactionsHanger = new FactionHanger(SteamId, _chat);
            FactionsHanger.ListAllGrids();
        }

        public void DetailedInfo(string input)
        {
            FactionsHanger = new FactionHanger(SteamId, _chat);

            if (!FactionsHanger.ParseInput(input, out var id))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }

            FactionsHanger.DetailedReport(id);
        }

        public async void LoadGrid(string input, bool loadNearPlayer)
        {
            if (!PerformMainChecks(false))
                return;


            if (!FactionsHanger.ParseInput(input, out var id))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }


            if (!FactionsHanger.TryGetGridStamp(id, out var stamp))
                return;


            //Check to see if the grid is for sale. We need to let the player know if it is
            if (!CheckGridForSale(stamp, id))
                return;


            if (!FactionsHanger.LoadGrid(stamp, out var grids, _identityId))
            {
                Log.Error($"Loading grid {id} failed for {_identityId}!");
                _chat.Respond("Loading grid failed! Report this to staff and check logs for more info!");
                return;
            }

            var file = File.Open(Path.Combine(FactionsHanger.FactionFolderPath, stamp.GridName + ".sbc"), FileMode.Open);

            var myObjectBuilderCubeGrids = grids as MyObjectBuilder_CubeGrid[] ?? grids.ToArray();
            if (!FactionsHanger.CheckLimits(stamp, myObjectBuilderCubeGrids, _identityId))
            {
                file.Close();
                return;
            }
                

            if (!CheckEnemyDistance(Config.LoadType, stamp.GridSavePosition) && !Config.AllowLoadNearEnemy)
            {
                file.Close();
                return;
            }

            if (!RequireLoadCurrency(stamp))
            {
                file.Close();
                return;
            }
            
            //cooldown
            FactionsHanger.SelectedFactionFile.LoadFile(FactionsHanger.FactionFolderPath, (ulong)FactionsHanger.FactionId);
            if (!FactionsHanger.CheckPlayerTimeStamp())
            {
                _chat?.Respond("Command cooldown is still in affect!");
                
                file.Close();
                return;
                
            }
            
            var st = new TimeStamp
            {
                OldTime = DateTime.Now
            };
            FactionsHanger.SelectedFactionFile.Timer = st;
            FactionsHanger.SelectedFactionFile.SaveFile();

            PluginDependencies.BackupGrid(myObjectBuilderCubeGrids.ToList(), _identityId);
            var spawnPos = DetermineSpawnPosition(stamp.GridSavePosition, _playerPosition, out var keepOriginalPosition,
                loadNearPlayer);

            if (!CheckDistanceToLoadPoint(spawnPos))
            {
                file.Close();
                return;
            }

            if (PluginDependencies.NexusInstalled && Config.NexusApi &&
                NexusSupport.RelayLoadIfNecessary(spawnPos, id, loadNearPlayer, _chat, SteamId, _identityId,
                    _playerPosition))
            {
                file.Close();
                return;
            }

            var spawner = new ParallelSpawner(myObjectBuilderCubeGrids, _chat, SteamId, SpawnedGridsSuccessful);
            spawner.setBounds(stamp.BoundingBox, stamp.Box, stamp.MatrixTranslation);

            Log.Info("Attempting Grid Spawning @" + spawnPos.ToString());
            if (spawner.Start(spawnPos, keepOriginalPosition))
            {
                _chat?.Respond("Spawning Complete!");
                file.Close();
                FactionsHanger.RemoveGridStamp(id);
                FactionsHanger.SendWebHookMessage($"{_userCharacter?.DisplayNameText ?? "Name not found"} {SteamId} loaded grid {stamp.GridName}");
            }
            else
            {
                file.Close();
                //_chat?.Respond("An error occured while spawning the grid!");
            }
        }

        public void SellGrid(int id, long price, string description)
        {
            FactionsHanger = new FactionHanger(SteamId, _chat);

            if (!FactionsHanger.TryGetGridStamp(id, out var stamp))
                return;

            //Check to see if grid is already for sale
            if (stamp.IsGridForSale())
            {
                _chat.Respond("This grid is already for sale!");
                return;
            }


            if (!FactionsHanger.SellSelectedGrid(stamp, price, description))
                return;

            _chat.Respond("Grid has been succesfully listed!");
        }

        public void RemoveGrid(string input)
        {
            FactionsHanger = new FactionHanger(SteamId, _chat);

            if (!FactionsHanger.ParseInput(input, out var id))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }

            if (FactionsHanger.RemoveGridStamp(id))
                _chat.Respond("Successfully removed grid!");
        }

        private void SpawnedGridsSuccessful(HashSet<MyCubeGrid> grids)
        {
            grids.BiggestGrid(out var biggestGrid);

            if (biggestGrid != null && _identityId != 0)
                _gpsSender.SendGps(biggestGrid.PositionComp.GetPosition(), biggestGrid.DisplayName, _identityId);
        }

        private bool CheckZoneRestrictions(bool isSave)
        {
            if (Config.ZoneRestrictions.Count == 0) return true;
            //Get save point
            var closestPoint = -1;
            double distance = -1;

            for (var i = 0; i < Config.ZoneRestrictions.Count(); i++)
            {
                var zoneCenter = new Vector3D(Config.ZoneRestrictions[i].X, Config.ZoneRestrictions[i].Y,
                    Config.ZoneRestrictions[i].Z);

                var playerDistance = Vector3D.Distance(zoneCenter, _playerPosition);

                if (playerDistance <= Config.ZoneRestrictions[i].Radius)
                {
                    //if player is within range

                    if (isSave && !Config.ZoneRestrictions[i].AllowSaving)
                    {
                        _chat?.Respond("You are not permitted to save grids in this zone");
                        return false;
                    }

                    if (isSave || Config.ZoneRestrictions[i].AllowLoading) return true;
                    _chat?.Respond("You are not permitted to load grids in this zone");
                    return false;

                }


                if (isSave && Config.ZoneRestrictions[i].AllowSaving)
                    if (closestPoint == -1 || playerDistance <= distance)
                    {
                        closestPoint = i;
                        distance = playerDistance;
                    }


                if (isSave || !Config.ZoneRestrictions[i].AllowLoading) continue;
                if (closestPoint != -1 && !(playerDistance <= distance)) continue;
                closestPoint = i;
                distance = playerDistance;
            }

            Vector3D closestZone;
            try
            {
                closestZone = new Vector3D(Config.ZoneRestrictions[closestPoint].X,
                    Config.ZoneRestrictions[closestPoint].Y, Config.ZoneRestrictions[closestPoint].Z);
            }
            catch (Exception)
            {
                _chat?.Respond("No areas found!");
                //Log.Warn(e, "No suitable zones found! (Possible Error)");
                return false;
            }


            if (isSave)
            {
                _gpsSender.SendGps(closestZone,
                    Config.ZoneRestrictions[closestPoint].Name + " (within " +
                    Config.ZoneRestrictions[closestPoint].Radius + "m)", _identityId);
                _chat?.Respond("Nearest save area has been added to your HUD");
                return false;
            }
            _gpsSender.SendGps(closestZone,
                Config.ZoneRestrictions[closestPoint].Name + " (within " +
                Config.ZoneRestrictions[closestPoint].Radius + "m)", _identityId);
            //Chat chat = new Chat(Context);
            _chat?.Respond("Nearest load area has been added to your HUD");
            return false;

        }

        private bool CheckGravity()
        {
            if (!Config.AllowInGravity)
            {
                if (Vector3D.IsZero(MyGravityProviderSystem.CalculateNaturalGravityInPoint(_playerPosition)))
                    return true;
                _chat?.Respond("Saving & Loading in gravity has been disabled!");
                return false;
            }

            if (Config.MaxGravityAmount == 0) return true;
            var gravity = MyGravityProviderSystem.CalculateNaturalGravityInPoint(_playerPosition).Length() / 9.81f;
            if (!(gravity > Config.MaxGravityAmount)) return true;
            //Log.Warn("Players gravity amount: " + Gravity);
            _chat?.Respond("You are not permitted to Save/load in this gravity amount. Max amount: " +
                          Config.MaxGravityAmount + "g");
            return false;
        }

        private bool CheckEnemyDistance(LoadType loadingAtSavePoint, Vector3D position = new Vector3D())
        {
            if (loadingAtSavePoint == LoadType.ForceLoadMearPlayer) position = _playerPosition;

            var playersFaction = MySession.Static.Factions.GetPlayerFaction(_identityId);
            var enemyFoundFlag = false;


            if (Config.DistanceCheck > 0)
                //Check enemy location! If under limit return!
                foreach (var p in MySession.Static.Players.GetOnlinePlayers())
                {
                    if (p.Character == null || p.Character.MarkedForClose)
                        continue;

                    Vector3D pos;
                    if (p.Character.IsUsing is MyCryoChamber || p.Character.IsUsing is MyCockpit)
                        pos = (p.Character.IsUsing as MyCockpit).PositionComp.GetPosition();
                    else
                        pos = p.GetPosition();


                    var playerId = p.Identity.IdentityId;
                    if (playerId == 0L) continue;
                    if (playerId == _identityId)
                        continue;

                    var targetPlayerFaction = MySession.Static.Factions.GetPlayerFaction(playerId);
                    if (playersFaction != null && targetPlayerFaction != null)
                    {
                        if (playersFaction.FactionId == targetPlayerFaction.FactionId)
                            continue;

                        //Neutrals count as allies not friends for some reason
                        var relation = MySession.Static.Factions
                            .GetRelationBetweenFactions(playersFaction.FactionId, targetPlayerFaction.FactionId).Item1;
                        if (relation == MyRelationsBetweenFactions.Neutral ||
                            relation == MyRelationsBetweenFactions.Friends)
                            continue;
                    }


                    if (Vector3D.Distance(position, pos) == 0) continue;

                    if (!(Vector3D.Distance(position, pos) <= Config.DistanceCheck)) continue;
                    _chat?.Respond("Unable to load grid! Enemy within " + Config.DistanceCheck + "m!");
                    _gpsSender.SendGps(position, "Failed Hangar Load! (Enemy nearby)", _identityId);
                    enemyFoundFlag = true;
                    break;
                }


            if (!(Config.GridDistanceCheck > 0) || Config.GridCheckMinBlock <= 0 || enemyFoundFlag != false)
                return !enemyFoundFlag;
            {
                var spawnSphere = new BoundingSphereD(position, Config.GridDistanceCheck);

                var entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref spawnSphere, entities);


                //This is looping through all grids in the specified range. If we find an enemy, we need to break and return/deny spawning
                foreach (var grid in entities.OfType<MyCubeGrid>())
                {
                    if (grid == null || grid.MarkedForClose)
                        continue;

                    if (grid.BigOwners.Count <= 0 || grid.CubeBlocks.Count < Config.GridCheckMinBlock)
                        continue;

                    if (grid.BigOwners.Contains(_identityId))
                        continue;


                    //if the player isnt big owner, we need to scan for faction mates
                    var foundAlly = true;
                    foreach (var targetPlayerFaction in grid.BigOwners.Select(owner => MySession.Static.Factions.GetPlayerFaction(owner)))
                    {
                        if (playersFaction != null && targetPlayerFaction != null)
                        {
                            if (playersFaction.FactionId == targetPlayerFaction.FactionId)
                                continue;

                            var relation = MySession.Static.Factions
                                .GetRelationBetweenFactions(playersFaction.FactionId, targetPlayerFaction.FactionId)
                                .Item1;
                            if (relation != MyRelationsBetweenFactions.Enemies) continue;
                            foundAlly = false;
                            break;
                        }
                        else
                        {
                            foundAlly = false;
                            break;
                        }
                    }

                    if (foundAlly) continue;
                    //Stop loop
                    _chat?.Respond("Unable to load grid! Enemy within " + Config.GridDistanceCheck + "m!");
                    _gpsSender.SendGps(position, "Failed Hangar Load! (Enemy nearby)", _identityId);
                    enemyFoundFlag = true;
                    break;
                }
            }

            return !enemyFoundFlag;
        }

        private static Vector3D DetermineSpawnPosition(Vector3D gridPosition, Vector3D characterPosition,
            out bool keepOriginalPosition, bool playersSpawnNearPlayer = false)
        {
            switch (Config.LoadType)
            {
                //If the ship is loading from where it saved, we want to ignore aligning to gravity. (Needs to attempt to spawn in original position)
                case LoadType.ForceLoadNearOriginalPosition when gridPosition == Vector3D.Zero:
                    Log.Info("Grid position is empty!");
                    keepOriginalPosition = false;
                    return characterPosition;
                case LoadType.ForceLoadNearOriginalPosition:
                    Log.Info("Loading from grid save position!");
                    keepOriginalPosition = true;
                    return gridPosition;
                case LoadType.ForceLoadMearPlayer when characterPosition == Vector3D.Zero:
                    keepOriginalPosition = true;
                    return gridPosition;
                case LoadType.ForceLoadMearPlayer:
                    keepOriginalPosition = false;
                    return characterPosition;
            }

            if (playersSpawnNearPlayer)
            {
                keepOriginalPosition = false;
                return characterPosition;
            }
            keepOriginalPosition = true;
            return gridPosition;
        }

        private void TryGetPlayerBalance(out long balance)
        {
            try
            {
                balance = MyBankingSystem.GetBalance(_identityId);
            }
            catch (Exception)
            {
                balance = 0;
            }

        }

        private void ChangeBalance(long amount)
        {
            MyBankingSystem.ChangeBalance(_identityId, amount);
        }

        private bool CheckDistanceToLoadPoint(Vector3D loadPoint)
        {
            if (!Config.RequireLoadRadius)
                return true;


            var distance = Vector3D.Distance(_playerPosition, loadPoint);
            if (distance < Config.LoadRadius)
                return true;

            _gpsSender.SendGps(loadPoint, "Load Point", _identityId);
            _chat.Respond("Cannot load! You are " + Math.Round(distance, 0) +
                         "m away from the load point! Check your GPS points!");
            return false;
        }

        private bool IsAnyGridInsideSafeZone(GridResult result)
        {
            if (!MySessionComponentSafeZones.SafeZones.Any(zone => zone.Entities.Contains(result.BiggestGrid.EntityId)))
                return false;
            _chat?.Respond("Cannot save a grid in safezone!");
            return true;

        }


        private static Dictionary<ulong, int> _playerConfirmations = new Dictionary<ulong, int>();

        private bool CheckGridForSale(GridStamp stamp, int id)
        {
            while (true)
            {
                if (!stamp.GridForSale)
                {
                    //if grid is not for sale, remove any confirmations
                    _playerConfirmations.Remove(SteamId);
                    return true;
                }
                else
                {
                    if (!_playerConfirmations.TryGetValue(SteamId, out var selection))
                    {
                        //Prompt user
                        _chat.Respond(Config.RestockAmount != 0
                            ? $"This grid is for sale! Run this command again to pay {Config.RestockAmount}sc to remove it from the market and load it in!"
                            : "This grid is for sale! Run this command again to confirm removal of sell offer and load it in!");


                        _playerConfirmations.Add(SteamId, id);

                        return false;
                    }
                    if (selection != id)
                    {
                        //If this grid is for sale and doesnt match our first selection need to remove it from the list and call this function again.
                        _playerConfirmations.Remove(SteamId);
                        continue;
                    }

                    //Remove market offer
                    HangarMarketController.RemoveMarketListing(SteamId, stamp.GridName);
                    _playerConfirmations.Remove(SteamId);
                    return true;
                }
            }
        }
    }
}