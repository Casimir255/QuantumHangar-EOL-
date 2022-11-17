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
using System.Linq;
using Torch.Commands;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using static QuantumHangar.Utils.CharacterUtilities;

namespace QuantumHangar.HangarChecks
{
    //This is when a normal player runs hangar commands
    public class PlayerChecks
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly Chat _chat;
        private readonly GpsSender _gpsSender;
        public readonly ulong SteamId;
        private readonly long _identityId;
        private readonly Vector3D _playerPosition;
        private readonly MyCharacter _userCharacter;

        private PlayerHangar PlayersHanger { get; set; }

        public static Settings Config => Hangar.Config;


        // PlayerChecks as initiated by another server to call LoadGrid.
        // We don't have a command context nor a player character object to work with,
        // but we receive all required data in the Nexus message.
        public PlayerChecks(Chat chat, GpsSender gpsSender, ulong steamID, long identityID, Vector3D playerPosition)
        {
            _chat = chat;
            _gpsSender = gpsSender;
            SteamId = steamID;
            _identityId = identityID;
            _playerPosition = playerPosition;
            // UserCharacter can remain null, it is only used by SaveGrid.
        }

        public PlayerChecks(CommandContext Context)
        {
            _chat = new Chat(Context);
            _gpsSender = new GpsSender();
            SteamId = Context.Player.SteamUserId;
            _identityId = Context.Player.Identity.IdentityId;
            _playerPosition = Context.Player.GetPosition();
            _userCharacter = (MyCharacter)Context.Player.Character;
        }

        private bool PerformMainChecks(bool IsSaving)
        {
            if (!Config.PluginEnabled)
            {
                _chat?.Respond("Plugin is not enabled!");
                return false;
            }


            if (PlayerHangar.IsServerSaving(_chat))
            {
                _chat?.Respond("Server is saving or is paused!");
                return false;
            }


            if (!CheckZoneRestrictions(IsSaving))
            {
                _chat?.Respond("You are not in the right zone!");
                return false;
            }


            if (!CheckGravity())
            {
                _chat?.Respond("Unable to perform this action in gravity!");
                return false;
            }

            PlayersHanger = new PlayerHangar(SteamId, _chat);
            if (PlayersHanger.CheckPlayerTimeStamp()) return true;
            _chat?.Respond("Command cooldown is still in affect!");
            return false;


            /*
            if (CheckEnemyDistance(LoadingAtSave, PlayerPosition))
                return false;
            */

        }

        public async void SaveGrid()
        {
            if (!PerformMainChecks(true))
                return;


            if (!PlayersHanger.CheckHangarLimits())
                return;


            var Result = new GridResult();

            //Gets grids player is looking at
            if (!Result.GetGrids(_chat, _userCharacter))
                return;


            if (IsAnyGridInsideSafeZone(Result))
                return;


            //Calculates incoming grids data
            var GridData = Result.GenerateGridStamp();


            //PlayersHanger.CheckGridLimits(GridData);

            //Checks for single and all slot block and grid limits
            if (!PlayersHanger.ExtensiveLimitChecker(GridData))
                return;


            if (!CheckEnemyDistance(Config.LoadType, _playerPosition))
                return;


            if (!RequireSaveCurrency(Result))
                return;


            PlayersHanger.SelectedPlayerFile.FormatGridName(GridData);

            var val = await PlayersHanger.SaveGridsToFile(Result, GridData.GridName);
            if (val)
            {
                PlayersHanger.SaveGridStamp(GridData);
                _chat?.Respond("Save Complete!");
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
                long SaveCost = 0;
                switch (Config.HangarSaveCostType)
                {
                    case CostType.BlockCount:

                        foreach (var grid in result.Grids)
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                    //If grid is station
                                    SaveCost += Convert.ToInt64(grid.BlocksCount * Config.CustomStaticGridCurrency);
                                else
                                    //If grid is large grid
                                    SaveCost += Convert.ToInt64(grid.BlocksCount * Config.CustomLargeGridCurrency);
                            }
                            else
                            {
                                //if its a small grid
                                SaveCost += Convert.ToInt64(grid.BlocksCount * Config.CustomSmallGridCurrency);
                            }

                        //Multiply by 
                        break;


                    case CostType.Fixed:

                        SaveCost = Convert.ToInt64(Config.CustomStaticGridCurrency);

                        break;


                    case CostType.PerGrid:

                        foreach (var grid in result.Grids)
                            if (grid.GridSizeEnum == MyCubeSize.Large)
                            {
                                if (grid.IsStatic)
                                    //If grid is station
                                    SaveCost += Convert.ToInt64(Config.CustomStaticGridCurrency);
                                else
                                    //If grid is large grid
                                    SaveCost += Convert.ToInt64(Config.CustomLargeGridCurrency);
                            }
                            else
                            {
                                //if its a small grid
                                SaveCost += Convert.ToInt64(Config.CustomSmallGridCurrency);
                            }

                        break;
                }

                TryGetPlayerBalance(out var Balance);


                if (Balance >= SaveCost)
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

                            ChangeBalance(-1 * SaveCost);
                            return true;
                        }
                        _chat?.Respond("Saving this grid in your hangar will cost " + SaveCost +
                                      " SC. Run this command again within 30 secs to continue!");
                        confirmationCooldown.StartCooldown(command);
                        return false;
                    }
                    _chat?.Respond("Saving this grid in your hangar will cost " + SaveCost +
                                  " SC. Run this command again within 30 secs to continue!");
                    confirmationCooldown = new CurrentCooldown();
                    confirmationCooldown.StartCooldown(command);
                    confirmationCooldownMap.Add(_identityId, confirmationCooldown);
                    return false;
                }
                else
                {
                    var required = SaveCost - Balance;
                    _chat?.Respond("You need an additional " + required + " SC to perform this action!");
                    return false;
                }
            }
        }

        private bool RequireLoadCurrency(GridStamp Grid)
        {
            //MyObjectBuilder_Definitions(MyParticlesManager)
            if (!Config.RequireCurrency)
            {
                return true;
            }
            else
            {
                long LoadCost = 0;
                switch (Config.HangarLoadCostType)
                {
                    case CostType.BlockCount:
                        //If grid is station
                        LoadCost = Convert.ToInt64(Grid.NumberofBlocks * Config.LoadStaticGridCurrency);
                        break;


                    case CostType.Fixed:

                        LoadCost = Convert.ToInt64(Config.LoadStaticGridCurrency);

                        break;


                    case CostType.PerGrid:

                        LoadCost += Convert.ToInt64(Grid.StaticGrids * Config.LoadStaticGridCurrency);
                        LoadCost += Convert.ToInt64(Grid.LargeGrids * Config.LoadLargeGridCurrency);
                        LoadCost += Convert.ToInt64(Grid.SmallGrids * Config.LoadSmallGridCurrency);


                        break;
                }

                TryGetPlayerBalance(out var Balance);


                if (Balance >= LoadCost)
                {
                    //Check command status!
                    var command = Grid.GridName;
                    var confirmationCooldownMap = Hangar.ConfirmationsMap;
                    if (confirmationCooldownMap.TryGetValue(_identityId, out var confirmationCooldown))
                    {
                        if (!confirmationCooldown.CheckCommandStatus(command))
                        {
                            //Confirmed command! Update player balance!
                            confirmationCooldownMap.Remove(_identityId);
                            _chat?.Respond("Confirmed! Loading grid!");

                            ChangeBalance(-1 * LoadCost);
                            return true;
                        }
                        else
                        {
                            _chat?.Respond("Loading this grid will cost " + LoadCost +
                                          " SC. Run this command again within 30 secs to continue!");
                            confirmationCooldown.StartCooldown(command);
                            return false;
                        }
                    }
                    else
                    {
                        _chat?.Respond("Loading this grid will cost " + LoadCost +
                                      " SC. Run this command again within 30 secs to continue!");
                        confirmationCooldown = new CurrentCooldown();
                        confirmationCooldown.StartCooldown(command);
                        confirmationCooldownMap.Add(_identityId, confirmationCooldown);
                        return false;
                    }
                }
                else
                {
                    var required = LoadCost - Balance;
                    _chat?.Respond("You need an additional " + required + " SC to perform this action!");
                    return false;
                }
            }
        }

        public void ListGrids()
        {
            PlayersHanger = new PlayerHangar(SteamId, _chat);
            PlayersHanger.ListAllGrids();
        }

        public void DetailedInfo(string input)
        {
            PlayersHanger = new PlayerHangar(SteamId, _chat);

            if (!PlayersHanger.ParseInput(input, out var ID))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }

            PlayersHanger.DetailedReport(ID);
        }

        public void LoadGrid(string input, bool LoadNearPlayer)
        {
            if (!PerformMainChecks(false))
                return;


            if (!PlayersHanger.ParseInput(input, out var ID))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }


            if (!PlayersHanger.TryGetGridStamp(ID, out var Stamp))
                return;


            //Check to see if the grid is for sale. We need to let the player know if it is
            if (!CheckGridForSale(Stamp, ID))
                return;


            if (!PlayersHanger.LoadGrid(Stamp, out var Grids))
            {
                Log.Error($"Loading grid {ID} failed for {_identityId}!");
                _chat.Respond("Loading grid failed! Report this to staff and check logs for more info!");
                return;
            }

            if (!PlayersHanger.CheckLimits(Stamp, Grids))
                return;

            if (!CheckEnemyDistance(Config.LoadType, Stamp.GridSavePosition) && !Config.AllowLoadNearEnemy)
                return;

            if (!RequireLoadCurrency(Stamp))
                return;

            PluginDependencies.BackupGrid(Grids.ToList(), _identityId);
            var SpawnPos = DetermineSpawnPosition(Stamp.GridSavePosition, _playerPosition, out var KeepOriginalPosition,
                LoadNearPlayer);

            if (!CheckDistanceToLoadPoint(SpawnPos))
                return;

            if (PluginDependencies.NexusInstalled && Config.NexusApi &&
                NexusSupport.RelayLoadIfNecessary(SpawnPos, ID, LoadNearPlayer, _chat, SteamId, _identityId,
                    _playerPosition))
                return;

            var Spawner = new ParallelSpawner(Grids, _chat, SpawnedGridsSuccessful);
            Log.Info("Attempting Grid Spawning @" + SpawnPos.ToString());
            if (Spawner.Start(SpawnPos, KeepOriginalPosition))
            {
                _chat?.Respond("Spawning Complete!");
                PlayersHanger.RemoveGridStamp(ID);
            }
            else
            {
                _chat?.Respond("An error occured while spawning the grid!");
            }
        }

        public void SellGrid(int ID, long Price, string Description)
        {
            PlayersHanger = new PlayerHangar(SteamId, _chat);

            if (!PlayersHanger.TryGetGridStamp(ID, out var Stamp))
                return;

            //Check to see if grid is already for sale
            if (Stamp.IsGridForSale())
            {
                _chat.Respond("This grid is already for sale!");
                return;
            }


            if (!PlayersHanger.SellSelectedGrid(Stamp, Price, Description))
                return;

            _chat.Respond("Grid has been succesfully listed!");
        }

        public void RemoveGrid(string input)
        {
            if (!PlayersHanger.ParseInput(input, out var ID))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }

            PlayersHanger = new PlayerHangar(SteamId, _chat);
            if (PlayersHanger.RemoveGridStamp(ID))
                _chat.Respond("Successfully removed grid!");
        }

        private void SpawnedGridsSuccessful(HashSet<MyCubeGrid> Grids)
        {
            Grids.BiggestGrid(out var BiggestGrid);

            if (BiggestGrid != null && _identityId != 0)
                _gpsSender.SendGps(BiggestGrid.PositionComp.GetPosition(), BiggestGrid.DisplayName, _identityId);
        }

        private bool CheckZoneRestrictions(bool IsSave)
        {
            if (Config.ZoneRestrictions.Count == 0) return true;
            //Get save point
            var ClosestPoint = -1;
            double Distance = -1;

            for (var i = 0; i < Config.ZoneRestrictions.Count(); i++)
            {
                var ZoneCenter = new Vector3D(Config.ZoneRestrictions[i].X, Config.ZoneRestrictions[i].Y,
                    Config.ZoneRestrictions[i].Z);

                var PlayerDistance = Vector3D.Distance(ZoneCenter, _playerPosition);

                if (PlayerDistance <= Config.ZoneRestrictions[i].Radius)
                {
                    //if player is within range

                    if (IsSave && !Config.ZoneRestrictions[i].AllowSaving)
                    {
                        _chat?.Respond("You are not permitted to save grids in this zone");
                        return false;
                    }

                    if (IsSave || Config.ZoneRestrictions[i].AllowLoading) return true;
                    _chat?.Respond("You are not permitted to load grids in this zone");
                    return false;

                }


                if (IsSave && Config.ZoneRestrictions[i].AllowSaving)
                    if (ClosestPoint == -1 || PlayerDistance <= Distance)
                    {
                        ClosestPoint = i;
                        Distance = PlayerDistance;
                    }


                if (IsSave || !Config.ZoneRestrictions[i].AllowLoading) continue;
                if (ClosestPoint != -1 && !(PlayerDistance <= Distance)) continue;
                ClosestPoint = i;
                Distance = PlayerDistance;
            }

            Vector3D ClosestZone;
            try
            {
                ClosestZone = new Vector3D(Config.ZoneRestrictions[ClosestPoint].X,
                    Config.ZoneRestrictions[ClosestPoint].Y, Config.ZoneRestrictions[ClosestPoint].Z);
            }
            catch (Exception)
            {
                _chat?.Respond("No areas found!");
                //Log.Warn(e, "No suitable zones found! (Possible Error)");
                return false;
            }


            if (IsSave)
            {
                _gpsSender.SendGps(ClosestZone,
                    Config.ZoneRestrictions[ClosestPoint].Name + " (within " +
                    Config.ZoneRestrictions[ClosestPoint].Radius + "m)", _identityId);
                _chat?.Respond("Nearest save area has been added to your HUD");
                return false;
            }
            _gpsSender.SendGps(ClosestZone,
                Config.ZoneRestrictions[ClosestPoint].Name + " (within " +
                Config.ZoneRestrictions[ClosestPoint].Radius + "m)", _identityId);
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
            var Gravity = MyGravityProviderSystem.CalculateNaturalGravityInPoint(_playerPosition).Length() / 9.81f;
            if (!(Gravity > Config.MaxGravityAmount)) return true;
            //Log.Warn("Players gravity amount: " + Gravity);
            _chat?.Respond("You are not permitted to Save/load in this gravity amount. Max amount: " +
                          Config.MaxGravityAmount + "g");
            return false;
        }

        private bool CheckEnemyDistance(LoadType LoadingAtSavePoint, Vector3D Position = new Vector3D())
        {
            if (LoadingAtSavePoint == LoadType.ForceLoadMearPlayer) Position = _playerPosition;

            var PlayersFaction = MySession.Static.Factions.GetPlayerFaction(_identityId);
            var EnemyFoundFlag = false;


            if (Config.DistanceCheck > 0)
                //Check enemy location! If under limit return!
                foreach (var P in MySession.Static.Players.GetOnlinePlayers())
                {
                    if (P.Character == null || P.Character.MarkedForClose)
                        continue;

                    Vector3D Pos;
                    if (P.Character.IsUsing is MyCryoChamber || P.Character.IsUsing is MyCockpit)
                        Pos = (P.Character.IsUsing as MyCockpit).PositionComp.GetPosition();
                    else
                        Pos = P.GetPosition();


                    var PlayerID = P.Identity.IdentityId;
                    if (PlayerID == 0L) continue;
                    if (PlayerID == _identityId)
                        continue;

                    var TargetPlayerFaction = MySession.Static.Factions.GetPlayerFaction(PlayerID);
                    if (PlayersFaction != null && TargetPlayerFaction != null)
                    {
                        if (PlayersFaction.FactionId == TargetPlayerFaction.FactionId)
                            continue;

                        //Neutrals count as allies not friends for some reason
                        var Relation = MySession.Static.Factions
                            .GetRelationBetweenFactions(PlayersFaction.FactionId, TargetPlayerFaction.FactionId).Item1;
                        if (Relation == MyRelationsBetweenFactions.Neutral ||
                            Relation == MyRelationsBetweenFactions.Friends)
                            continue;
                    }


                    if (Vector3D.Distance(Position, Pos) == 0) continue;

                    if (!(Vector3D.Distance(Position, Pos) <= Config.DistanceCheck)) continue;
                    _chat?.Respond("Unable to load grid! Enemy within " + Config.DistanceCheck + "m!");
                    _gpsSender.SendGps(Position, "Failed Hangar Load! (Enemy nearby)", _identityId);
                    EnemyFoundFlag = true;
                    break;
                }


            if (!(Config.GridDistanceCheck > 0) || Config.GridCheckMinBlock <= 0 || EnemyFoundFlag != false)
                return !EnemyFoundFlag;
            {
                var SpawnSphere = new BoundingSphereD(Position, Config.GridDistanceCheck);

                var entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref SpawnSphere, entities);


                //This is looping through all grids in the specified range. If we find an enemy, we need to break and return/deny spawning
                foreach (var Grid in entities.OfType<MyCubeGrid>())
                {
                    if (Grid == null || Grid.MarkedForClose)
                        continue;

                    if (Grid.BigOwners.Count <= 0 || Grid.CubeBlocks.Count < Config.GridCheckMinBlock)
                        continue;

                    if (Grid.BigOwners.Contains(_identityId))
                        continue;


                    //if the player isnt big owner, we need to scan for faction mates
                    var FoundAlly = true;
                    foreach (var TargetPlayerFaction in Grid.BigOwners.Select(Owner => MySession.Static.Factions.GetPlayerFaction(Owner)))
                    {
                        if (PlayersFaction != null && TargetPlayerFaction != null)
                        {
                            if (PlayersFaction.FactionId == TargetPlayerFaction.FactionId)
                                continue;

                            var Relation = MySession.Static.Factions
                                .GetRelationBetweenFactions(PlayersFaction.FactionId, TargetPlayerFaction.FactionId)
                                .Item1;
                            if (Relation != MyRelationsBetweenFactions.Enemies) continue;
                            FoundAlly = false;
                            break;
                        }
                        else
                        {
                            FoundAlly = false;
                            break;
                        }
                    }

                    if (FoundAlly) continue;
                    //Stop loop
                    _chat?.Respond("Unable to load grid! Enemy within " + Config.GridDistanceCheck + "m!");
                    _gpsSender.SendGps(Position, "Failed Hangar Load! (Enemy nearby)", _identityId);
                    EnemyFoundFlag = true;
                    break;
                }
            }

            return !EnemyFoundFlag;
        }

        private static Vector3D DetermineSpawnPosition(Vector3D GridPosition, Vector3D CharacterPosition,
            out bool KeepOriginalPosition, bool PlayersSpawnNearPlayer = false)
        {
            switch (Config.LoadType)
            {
                //If the ship is loading from where it saved, we want to ignore aligning to gravity. (Needs to attempt to spawn in original position)
                case LoadType.ForceLoadNearOriginalPosition when GridPosition == Vector3D.Zero:
                    Log.Info("Grid position is empty!");
                    KeepOriginalPosition = false;
                    return CharacterPosition;
                case LoadType.ForceLoadNearOriginalPosition:
                    Log.Info("Loading from grid save position!");
                    KeepOriginalPosition = true;
                    return GridPosition;
                case LoadType.ForceLoadMearPlayer when CharacterPosition == Vector3D.Zero:
                    KeepOriginalPosition = true;
                    return GridPosition;
                case LoadType.ForceLoadMearPlayer:
                    KeepOriginalPosition = false;
                    return CharacterPosition;
            }

            if (PlayersSpawnNearPlayer)
            {
                KeepOriginalPosition = false;
                return CharacterPosition;
            }
            KeepOriginalPosition = true;
            return GridPosition;
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

        private void ChangeBalance(long Amount)
        {
            MyBankingSystem.ChangeBalance(_identityId, Amount);
        }

        private bool CheckDistanceToLoadPoint(Vector3D LoadPoint)
        {
            if (!Config.RequireLoadRadius)
                return true;


            var Distance = Vector3D.Distance(_playerPosition, LoadPoint);
            if (Distance < Config.LoadRadius)
                return true;

            _gpsSender.SendGps(LoadPoint, "Load Point", _identityId);
            _chat.Respond("Cannot load! You are " + Math.Round(Distance, 0) +
                         "m away from the load point! Check your GPS points!");
            return false;
        }

        private bool IsAnyGridInsideSafeZone(GridResult Result)
        {
            if (!MySessionComponentSafeZones.SafeZones.Any(Zone => Zone.Entities.Contains(Result.BiggestGrid.EntityId)))
                return false;
            _chat?.Respond("Cannot save a grid in safezone!");
            return true;

        }


        private static Dictionary<ulong, int> PlayerConfirmations = new Dictionary<ulong, int>();

        private bool CheckGridForSale(GridStamp Stamp, int ID)
        {
            while (true)
            {
                if (!Stamp.GridForSale)
                {
                    //if grid is not for sale, remove any confirmations
                    PlayerConfirmations.Remove(SteamId);
                    return true;
                }
                else
                {
                    if (!PlayerConfirmations.TryGetValue(SteamId, out var Selection))
                    {
                        //Prompt user
                        _chat.Respond(Config.RestockAmount != 0
                            ? $"This grid is for sale! Run this command again to pay {Config.RestockAmount}sc to remove it from the market and load it in!"
                            : "This grid is for sale! Run this command again to confirm removal of sell offer and load it in!");


                        PlayerConfirmations.Add(SteamId, ID);

                        return false;
                    }
                    if (Selection != ID)
                    {
                        //If this grid is for sale and doesnt match our first selection need to remove it from the list and call this function again.
                        PlayerConfirmations.Remove(SteamId);
                        continue;
                    }

                    //Remove market offer
                    HangarMarketController.RemoveMarketListing(SteamId, Stamp.GridName);
                    PlayerConfirmations.Remove(SteamId);
                    return true;
                }
            }
        }
    }
}