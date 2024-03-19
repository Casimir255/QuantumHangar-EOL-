using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using QuantumHangar.HangarMarket;
using QuantumHangar.Serialization;
using QuantumHangar.Utils;
using Sandbox.Definitions;
using Sandbox.Game.World;
using Torch.Mod;
using Torch.Mod.Messages;
using VRage.Game;
using VRage.Game.ModAPI;

namespace QuantumHangar.HangarChecks
{
    public class AllianceHanger : IDisposable
    {
        private static Logger Log;
        private readonly Chat _chat;
        public readonly AllianceInfo SelectedAllianceFile;

        private bool _isAdminCalling;
        private readonly ulong _steamId;
        //private readonly MyPlayer _player;
        private readonly Guid _allianceId;

        public string AllianceFolderPath { get; }


        private static Settings Config => Hangar.Config;

        private FileLock Lock { get; set; }
        ~AllianceHanger()
        {
            Dispose();
        }
        public AllianceHanger(ulong steamId, Chat respond, Guid allianceId, bool isAdminCalling = false)
        {
            try
            {
                MySession.Static.Players.TryGetIdentityFromSteamId(steamId, out var _identity);
                _allianceId = allianceId;
                Log = LogManager.GetLogger($"Hangar.{_allianceId}");
             
                
                this._steamId = steamId;

                this._isAdminCalling = isAdminCalling;
                _chat = respond;
                SelectedAllianceFile = new AllianceInfo();


                AllianceFolderPath = Path.Combine(Hangar.MainAllianceDirectory, _allianceId.ToString());
                if (SelectedAllianceFile.LoadFile(Hangar.MainAllianceDirectory, _allianceId))
                {
                    Lock = new FileLock(AllianceFolderPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }

        public static bool TransferGrid(Guid allianceIdFrom, Guid allianceIdTo, string gridName)
        {
            try
            {
                Log.Error("Starting Grid Transfer!");

                var fromInfo = new AllianceInfo();
                fromInfo.LoadFile(Hangar.MainPlayerDirectory, allianceIdFrom);

                if (!fromInfo.GetGrid(gridName, out var stamp, out var error))
                {
                    Log.Error("Failed to get grid! " + error);
                    return false;
                }


                var gridPath = stamp.GetGridPath(fromInfo.AllianceFolderpath);
                var fileName = Path.GetFileName(gridPath);

                fromInfo.Grids.Remove(stamp);
                fromInfo.SaveFile();

                var toInfo = new AllianceInfo();
                toInfo.LoadFile(Hangar.MainPlayerDirectory, allianceIdTo);
                toInfo.FormatGridName(stamp);


                //Call gridstamp transferred as it will force load near player, and transfer on load
                stamp.Transferred();

                toInfo.Grids.Add(stamp);

                //Make sure to create directory
                Directory.CreateDirectory(toInfo.AllianceFolderpath);
                File.Move(gridPath, Path.Combine(toInfo.AllianceFolderpath, stamp.GridName + ".sbc"));

                toInfo.SaveFile();
                Log.Error("Moved Grid!");
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }

            return true;
        }

        public static bool TransferGrid(AllianceInfo to, GridStamp stamp)
        {
            try
            {
                var gridName = stamp.GridName;
                to.FormatGridName(stamp);

                //Make sure to create the directory!
                Directory.CreateDirectory(to.AllianceFolderpath);

                File.Copy(stamp.OriginalGridPath, Path.Combine(to.AllianceFolderpath, stamp.GridName + ".sbc"));
                stamp.Transferred();

                to.ServerOfferPurchased(gridName);

                to.Grids.Add(stamp);
                to.SaveFile();
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }

            return true;
        }

        /* Private Methods */
        private bool RemoveStamp(int id)
        {
            //Log.Warn($"HitA: {ID}");

            //Input valid for ID - X
            if (!SelectedAllianceFile.IsInputValid(id, out string error))
            {
                _chat.Respond(error);
                return false;
            }

            //Log.Warn("HitB");
            if (!_isAdminCalling)
            {
                var stamp = new TimeStamp
                {
                    OldTime = DateTime.Now
                };
                SelectedAllianceFile.Timer = stamp;
            }

            //Log.Warn("HitC");
            try
            {
                var a = Path.Combine(AllianceFolderPath, SelectedAllianceFile.Grids[id - 1].GridName + ".sbc");
                Log.Info(a);

                File.Delete(a);
                SelectedAllianceFile.Grids.RemoveAt(id - 1);
                SelectedAllianceFile.SaveFile();


                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }

        private bool CheckGridLimits(GridStamp grid, MyIdentity playerIdentity)
        {
            //Backwards compatibale
            if (Config.OnLoadTransfer)
                return true;

            if (grid.ShipPcu.Count == 0)
            {
                var blockLimits = playerIdentity.BlockLimits;

                var a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                {
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Log.Debug("PCU Server limits is 0!");
                    return true;
                }

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                var currentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                var maxPcu = blockLimits.PCU + currentPcu;

                var pcu = maxPcu - currentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                //Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                //Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (maxPcu - currentPcu > grid.GridPcu) return true;
                var need = grid.GridPcu - (maxPcu - currentPcu);
                _chat.Respond("PCU limit reached! You need an additional " + need + " pcu to perform this action!");
                return false;
            }


            foreach (var player in grid.ShipPcu)
            {
                var identity = MySession.Static.Players.TryGetIdentity(player.Key);
                if (identity == null)
                {
                    continue;
                }

                var blockLimits = identity.BlockLimits;
                var a = MySession.Static.GlobalBlockLimits;

                if (a.PCU <= 0)
                {
                    //PCU Limits on server is 0
                    //Skip PCU Checks
                    //Hangar.Debug("PCU Server limits is 0!");
                    continue;
                }

                //Main.Debug("PCU Limit from Server:"+a.PCU);
                //Main.Debug("PCU Limit from Player: " + blockLimits.PCU);
                //Main.Debug("PCU Built from Player: " + blockLimits.PCUBuilt);

                var currentPcu = blockLimits.PCUBuilt;
                //Hangar.Debug("Current PCU: " + CurrentPcu);

                var maxPcu = blockLimits.PCU + currentPcu;

                var pcu = maxPcu - currentPcu;
                //Main.Debug("MaxPcu: " + pcu);
                //Hangar.Debug("Grid PCU: " + Grid.GridPCU);


                //Hangar.Debug("Current player PCU:" + CurrentPcu);

                //Find the difference
                if (maxPcu - currentPcu > player.Value) continue;
                var need = player.Value - (maxPcu - currentPcu);
                _chat.Respond("PCU limit reached! " + identity.DisplayName + " needs an additional " + need +
                             " PCU to load this grid!");
                return false;
            }

            return true;
        }

        private bool BlockLimitChecker(IEnumerable<MyObjectBuilder_CubeGrid> shipBlueprints, long playerIdentityId)
        {
            var biggestGrid = 0;
            var blocksToBuild = 0;
            //failedBlockType = null;
            //Need dictionary for each player AND their blocks they own. (Factions could own stuff on the same grid)
            var blocksAndOwnerForLimits = new Dictionary<long, Dictionary<string, int>>();


            //Total PCU and Blocks
            var finalBlocksCount = 0;
            var finalBlocksPcu = 0;


            //Go ahead and check if the block limits is enabled server side! If it isn't... return true!
            if (!Config.EnableBlackListBlocks)
            {
                return true;
            }

            //If we are using built in server block limits..
            if (Config.SBlockLimits)
            {
                //& the server blocklimits is not enabled... Return true
                if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.NONE)
                {
                    return true;
                }


                //Cycle each grid in the ship blueprints
                foreach (var cubeGrid in shipBlueprints)
                {
                    //Main.Debug("CubeBlocks count: " + CubeGrid.GetType());
                    if (biggestGrid < cubeGrid.CubeBlocks.Count())
                    {
                        biggestGrid = cubeGrid.CubeBlocks.Count();
                    }

                    blocksToBuild += cubeGrid.CubeBlocks.Count();

                    foreach (var block in cubeGrid.CubeBlocks)
                    {
                        var defId = new MyDefinitionId(block.TypeId, block.SubtypeId);

                        if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(defId,
                                out var myCubeBlockDefinition)) continue;
                        //Check for BlockPair or SubType?
                        var blockName = "";
                        //Server Block Limits
                        blockName = Config.SBlockLimits
                            ? myCubeBlockDefinition.BlockPairName
                            : /*Custom Block SubType Limits */ myCubeBlockDefinition.Id.SubtypeName;

                        var blockOwner2 = 0L;
                        blockOwner2 = block.BuiltBy;

                        //If the player dictionary already has a Key, we need to retrieve it
                        if (blocksAndOwnerForLimits.ContainsKey(blockOwner2))
                        {
                            //if the dictionary already contains the same block type
                            var dictForUser = blocksAndOwnerForLimits[blockOwner2];
                            if (dictForUser.ContainsKey(blockName))
                            {
                                dictForUser[blockName]++;
                            }
                            else
                            {
                                dictForUser.Add(blockName, 1);
                            }

                            blocksAndOwnerForLimits[blockOwner2] = dictForUser;
                        }
                        else
                        {
                            blocksAndOwnerForLimits.Add(blockOwner2, new Dictionary<string, int>
                            {
                                {
                                    blockName,
                                    1
                                }
                            });
                        }

                        finalBlocksPcu += myCubeBlockDefinition.PCU;
                        //if()
                    }

                    finalBlocksCount += cubeGrid.CubeBlocks.Count;
                }

                if (MySession.Static.MaxGridSize == 0 || biggestGrid <= MySession.Static.MaxGridSize)
                    return PlayerIdentityLoop(blocksAndOwnerForLimits, finalBlocksCount);
                _chat.Respond("Biggest grid is over Max grid size! ");
                return false;

                //Need too loop player identities in dictionary. Do this via seperate function
            }

            //BlockLimiter
            if (!PluginDependencies.BlockLimiterInstalled)
            {
                //BlockLimiter is null!
                _chat.Respond("Blocklimiter Plugin not installed or Loaded!");
                Log.Warn("BLimiter plugin not installed or loaded! May require a server restart!");
                return false;
            }

            var grids = shipBlueprints.ToList();
            var valueReturn = PluginDependencies.CheckGridLimits(grids, playerIdentityId);

            //Convert to value return type
            if (!valueReturn)
            {
                //Main.Debug("Cannont load grid in due to BlockLimiter Configs!");
                return true;
            }

            _chat.Respond("Grid would be over Server-Blocklimiter limits!");
            return false;
        }

        private bool PlayerIdentityLoop(Dictionary<long, Dictionary<string, int>> blocksAndOwnerForLimits,
            int blocksToBuild)
        {
            foreach (var (k, playerBuiltBlocks) in blocksAndOwnerForLimits)
            {
                var myIdentity = MySession.Static.Players.TryGetIdentity(k);
                if (myIdentity == null) continue;
                var blockLimits = myIdentity.BlockLimits;
                if (MySession.Static.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.PER_FACTION &&
                    MySession.Static.Factions.GetPlayerFaction(myIdentity.IdentityId) == null)
                {
                    _chat.Respond("ServerLimits are set PerFaction. You are not in a faction! Contact an Admin!");
                    return false;
                }

                if (blockLimits == null) continue;
                if (MySession.Static.MaxBlocksPerPlayer != 0 &&
                    blockLimits.BlocksBuilt + blocksToBuild > blockLimits.MaxBlocks)
                {
                    _chat.Respond("Cannot load grid! You would be over your Max Blocks!");
                    return false;
                }

                //Double check to see if the list is null
                if (playerBuiltBlocks == null) continue;
                foreach (var serverBlockLimits in MySession.Static.BlockTypeLimits)
                {
                    if (!playerBuiltBlocks.ContainsKey(serverBlockLimits.Key)) continue;
                    var totalNumberOfBlocks = playerBuiltBlocks[serverBlockLimits.Key];

                    if (blockLimits.BlockTypeBuilt.TryGetValue(serverBlockLimits.Key,
                            out MyBlockLimits.MyTypeLimitData limitData))
                    {
                        //Grab their existing block count for the block limit
                        totalNumberOfBlocks += limitData.BlocksBuilt;
                    }

                    //Compare to see if they would be over!
                    var serverLimit = MySession.Static.GetBlockTypeLimit(serverBlockLimits.Key);
                    if (totalNumberOfBlocks <= serverLimit) continue;
                    _chat.Respond("Player " + myIdentity.DisplayName + " would be over their " + serverBlockLimits.Key +
                                 " limits! " + totalNumberOfBlocks + "/" + serverLimit);
                    //Player would be over their block type limits
                    return false;
                }
            }

            return true;
        }

        public bool IsGridForSale(GridStamp grid, bool admin)
        {
            return admin && grid.GridForSale;
        }

        public static bool IsServerSaving(Chat chat)
        {
            if (!MySession.Static.IsSaveInProgress) return false;
            chat?.Respond("Server has a save in progress... Please wait!");
            return true;
        }

        public bool CheckPlayerTimeStamp()
        {
            //Check timestamp before continuing!
            if (SelectedAllianceFile.Timer == null) return true;
            var old = SelectedAllianceFile.Timer;
            var lastUse = old.OldTime;


            //When the player can use the command
            var canUseTime = lastUse + TimeSpan.FromMinutes(Config.WaitTime);
            //Log.Info("TimeSpan: " + Subtracted.TotalMinutes);
            if (canUseTime > DateTime.Now)
            {
                var timeLeft = DateTime.Now - canUseTime;
                //int RemainingTime = (int)Plugin.Config.WaitTime - Convert.ToInt32(Subtracted.TotalMinutes);
                _chat?.Respond($"You have {timeLeft:hh\\:mm\\:ss} until you can perform this action!");
                return false;
            }

            SelectedAllianceFile.Timer = null;
            return true;
        }

        public bool ExtensiveLimitChecker(GridStamp stamp)
        {
            //Begin Single Slot Save!
            if (Config.SingleMaxBlocks != 0)
            {
                if (stamp.NumberOfBlocks > Config.SingleMaxBlocks)
                {
                    var remainder = stamp.NumberOfBlocks - Config.SingleMaxBlocks;
                    _chat?.Respond("Grid is " + remainder + " blocks over the max slot block limit! " +
                                  stamp.NumberOfBlocks + "/" + Config.SingleMaxBlocks);
                    return false;
                }
            }

            if (Config.SingleMaxPcu != 0)
            {
                if (stamp.GridPcu > Config.SingleMaxPcu)
                {
                    var remainder = stamp.GridPcu - Config.SingleMaxPcu;
                    _chat?.Respond("Grid is " + remainder + " PCU over the slot hangar PCU limit! " + stamp.GridPcu +
                                  "/" + Config.SingleMaxPcu);
                    return false;
                }
            }

            if (Config.AllowStaticGrids)
            {
                if (Config.SingleMaxLargeGrids != 0 && stamp.StaticGrids > Config.SingleMaxStaticGrids)
                {
                    var remainder = stamp.StaticGrids - Config.SingleMaxStaticGrids;
                    _chat?.Respond("You are " + remainder + " static grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (stamp.StaticGrids > 0)
                {
                    _chat?.Respond("Saving Static Grids is disabled!");
                    return false;
                }
            }

            if (Config.AllowLargeGrids)
            {
                if (Config.SingleMaxLargeGrids != 0 && stamp.LargeGrids > Config.SingleMaxLargeGrids)
                {
                    var remainder = stamp.LargeGrids - Config.SingleMaxLargeGrids;
                    _chat?.Respond("You are " + remainder + " large grids over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (stamp.LargeGrids > 0)
                {
                    _chat?.Respond("Saving Large Grids is disabled!");
                    return false;
                }
            }

            if (Config.AllowSmallGrids)
            {
                if (Config.SingleMaxSmallGrids != 0 && stamp.SmallGrids > Config.SingleMaxSmallGrids)
                {
                    var remainder = stamp.SmallGrids - Config.SingleMaxLargeGrids;
                    _chat?.Respond("You are " + remainder + " small grid over the hangar slot limit!");
                    return false;
                }
            }
            else
            {
                if (stamp.SmallGrids > 0)
                {
                    _chat?.Respond("Saving Small Grids is disabled!");
                    return false;
                }
            }


            var totalBlocks = 0;
            var totalPcu = 0;
            var staticGrids = 0;
            var largeGrids = 0;
            var smallGrids = 0;

            //Hangar total limit!
            foreach (var grid in SelectedAllianceFile.Grids)
            {
                totalBlocks += grid.NumberOfBlocks;
                totalPcu += grid.GridPcu;

                staticGrids += grid.StaticGrids;
                largeGrids += grid.LargeGrids;
                smallGrids += grid.SmallGrids;
            }

            if (Config.PlayerMaxBlocks != 0 && totalBlocks > Config.PlayerMaxBlocks)
            {
                var remainder = totalBlocks - Config.PlayerMaxBlocks;
                _chat?.Respond("Grid is " + remainder + " blocks over your hangar block limit! " + totalBlocks + "/" +
                              Config.PlayerMaxBlocks);
                return false;
            }

            if (Config.PlayerMaxPCU != 0 && totalPcu > Config.PlayerMaxPCU)
            {
                var remainder = totalPcu - Config.PlayerMaxPCU;
                _chat?.Respond("Grid is " + remainder + " PCU over your hangar PCU limit! " + totalPcu + "/" +
                              Config.PlayerMaxPCU);
                return false;
            }

            if (Config.PlayerMaxStaticGrids != 0 && staticGrids > Config.PlayerMaxStaticGrids)
            {
                var remainder = staticGrids - Config.PlayerMaxStaticGrids;
                _chat?.Respond("You are " + remainder + " static grid over your hangar limit!");
                return false;
            }

            if (Config.PlayerMaxLargeGrids != 0 && largeGrids > Config.PlayerMaxLargeGrids)
            {
                var remainder = largeGrids - Config.PlayerMaxLargeGrids;
                _chat?.Respond("You are " + remainder + " large grid over your hangar limit!");
                return false;
            }

            if (Config.PlayerMaxSmallGrids == 0 || smallGrids <= Config.PlayerMaxSmallGrids) return true;
            {
                var remainder = largeGrids - Config.PlayerMaxSmallGrids;
                _chat?.Respond("You are " + remainder + " small grid over your hangar limit!");
                return false;
            }
        }

        public bool CheckHangarLimits()
        {
            if (SelectedAllianceFile.Grids.Count < SelectedAllianceFile.MaxHangarSlots) return true;
            _chat?.Respond("You have reached your hangar limit!");
            return false;
        }


        public void SaveGridStamp(GridStamp targetStamp, bool admin = false, bool ignoreSave = false)
        {
            if (!admin)
            {
                var stamp = new TimeStamp
                {
                    OldTime = DateTime.Now
                };
                SelectedAllianceFile.Timer = stamp;
            }

            SelectedAllianceFile.Grids.Add(targetStamp);

            if (!ignoreSave)
                SelectedAllianceFile.SaveFile();
        }

        public void SavePlayerFile()
        {
            SelectedAllianceFile.SaveFile();
        }

        public bool RemoveGridStamp(int id)
        {
            return RemoveStamp(id);
        }

        public async Task<bool> SaveGridsToFile(GridResult grids, string fileName, long identity)
        {
            return await GridSerializer.SaveGridsAndClose(grids.Grids, AllianceFolderPath, fileName, identity);
        }

        public void ListAllGrids()
        {
            if (SelectedAllianceFile.Grids.Count == 0)
            {
                _chat.Respond(_isAdminCalling
                    ? "There are no grids in this factions hangar!"
                    : "You have no grids in your hangar!");

                return;
            }

            var sb = new StringBuilder();

            if (_isAdminCalling)
                sb.AppendLine("Factions has " + SelectedAllianceFile.Grids.Count() + "/" +
                              SelectedAllianceFile.MaxHangarSlots + " stored grids:");
            else
                sb.AppendLine("You have " + SelectedAllianceFile.Grids.Count() + "/" + SelectedAllianceFile.MaxHangarSlots +
                              " stored grids:");

            var count = 1;
            foreach (var grid in SelectedAllianceFile.Grids)
            {
                if (grid.GridForSale)
                {
                    sb.AppendLine(" [$" + count + "$] - " + grid.GridName);
                }
                else
                {
                    sb.AppendLine(" [" + count + "] - " + grid.GridName);
                }

                count++;
            }

            _chat.Respond(sb.ToString());
        }

        public void DetailedReport(int id)
        {
            var response = new StringBuilder();
            string prefix;
            if (id == 0)
            {
                prefix = $"HangarSlots: {SelectedAllianceFile.Grids.Count()}/{SelectedAllianceFile.MaxHangarSlots}";
                response.AppendLine("- - Global Limits - -");
                response.AppendLine($"TotalBlocks: {SelectedAllianceFile.TotalBlocks}/{Config.PlayerMaxBlocks}");
                response.AppendLine($"TotalPCU: {SelectedAllianceFile.TotalPcu}/{Config.PlayerMaxPCU}");
                response.AppendLine($"StaticGrids: {SelectedAllianceFile.StaticGrids}/{Config.PlayerMaxStaticGrids}");
                response.AppendLine($"LargeGrids: {SelectedAllianceFile.LargeGrids}/{Config.PlayerMaxLargeGrids}");
                response.AppendLine($"SmallGrids: {SelectedAllianceFile.SmallGrids}/{Config.PlayerMaxSmallGrids}");
                response.AppendLine();
                response.AppendLine("- - Individual Hangar Slots - -");
                for (var i = 0; i < SelectedAllianceFile.Grids.Count; i++)
                {
                    var stamp = SelectedAllianceFile.Grids[i];
                    response.AppendLine($" * * Slot {i + 1} : {stamp.GridName} * *");
                    response.AppendLine($"PCU: {stamp.GridPcu}/{Config.SingleMaxPcu}");
                    response.AppendLine($"Blocks: {stamp.NumberOfBlocks}/{Config.SingleMaxBlocks}");
                    response.AppendLine($"StaticGrids: {stamp.StaticGrids}/{Config.SingleMaxStaticGrids}");
                    response.AppendLine($"LargeGrids: {stamp.LargeGrids}/{Config.SingleMaxLargeGrids}");
                    response.AppendLine($"SmallGrids: {stamp.SmallGrids}/{Config.SingleMaxSmallGrids}");
                    response.AppendLine($"TotalGridCount: {stamp.NumberOfGrids}");
                    response.AppendLine($"Mass: {stamp.GridMass}kg");
                    response.AppendLine($"Built%: {stamp.GridBuiltPercent * 100}%");
                    response.AppendLine($" * * * * * * * * * * * * * * * * ");
                    response.AppendLine();
                }
            }
            else
            {
                if (!SelectedAllianceFile.GetGrid(id, out var stamp, out var error))
                {
                    _chat.Respond(error);
                    return;
                }

                prefix = $"Slot {id} : {stamp.GridName}";
                response.AppendLine($"PCU: {stamp.GridPcu}/{Config.SingleMaxPcu}");
                response.AppendLine($"Blocks: {stamp.NumberOfBlocks}/{Config.SingleMaxBlocks}");
                response.AppendLine($"StaticGrids: {stamp.StaticGrids}/{Config.SingleMaxStaticGrids}");
                response.AppendLine($"LargeGrids: {stamp.LargeGrids}/{Config.SingleMaxLargeGrids}");
                response.AppendLine($"SmallGrids: {stamp.SmallGrids}/{Config.SingleMaxSmallGrids}");
                response.AppendLine($"TotalGridCount: {stamp.NumberOfGrids}");
                response.AppendLine($"Mass: {stamp.GridMass}kg");
                response.AppendLine($"Built%: {stamp.GridBuiltPercent * 100}%");
                response.AppendLine();
            }

            ModCommunication.SendMessageTo(new DialogMessage("Hangar Info", prefix, response.ToString()), _steamId);
        }

        public bool TryGetGridStamp(int id, out GridStamp stamp)
        {
            if (SelectedAllianceFile.GetGrid(id, out stamp, out var error)) return true;
            _chat.Respond(error);
            return false;
        }

        public bool ChangeWebhook(string webhook)
        {
            SelectedAllianceFile.Webhook = webhook;
            SelectedAllianceFile.SaveFile();
            return true;
        }

        public void SendWebHookMessage(string message)
        {
            if (SelectedAllianceFile.Webhook == "default") return;
            try
            {
                var client = new WebClient();
                client.Headers.Add("Content-Type", "application/json");
                //send to ingame and nexus 
                var payloadJson = JsonConvert.SerializeObject(new
                    {
                        username = "Alliance hangar Log",
                        embeds = new[]
                        {
                            new
                            {
                                description = message,
                                title = "Alliance hangar Log",
                            }
                        }
                    }
                );

                var payload = payloadJson;

                var utf8 = Encoding.UTF8.GetBytes(payload);

                client.UploadData(SelectedAllianceFile.Webhook, utf8);
            }
            catch (Exception e)
            {
                Log.Error($"Faction Hangar {_allianceId} {SelectedAllianceFile.Webhook} Discord webhook error, {e}");
            }
        }
        public bool LoadGrid(GridStamp stamp, out IEnumerable<MyObjectBuilder_CubeGrid> grids, long playerId)
        {
            if (!stamp.TryGetGrids(AllianceFolderPath, out grids))
                return false;


            PluginDependencies.BackupGrid(grids.ToList(), playerId);
            GridSerializer.TransferGridOwnership(grids, playerId, stamp.TransferOwnerShipOnLoad);

            return true;
        }

        public void UpdateHangar()
        {
            if (!Directory.Exists(AllianceFolderPath))
            {
                _chat?.Respond("This factions hangar doesn't exist! Skipping sync!");
                return;
            }

            var myFiles = Directory.EnumerateFiles(AllianceFolderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(s => Path.GetExtension(s).TrimStart('.').ToLowerInvariant() == "sbc");

            if (!myFiles.Any())
                return;
            //Scan for new grids


            var newGrids = new List<GridStamp>();
            var addedGrids = 0;
            foreach (var file in myFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                Log.Info(name);
                if (SelectedAllianceFile.AnyGridsMatch(Path.GetFileNameWithoutExtension(name)))
                    continue;

                addedGrids++;
                var stamp = new GridStamp(file);
                newGrids.Add(stamp);
            }

            var removedGrids = 0;
            for (var i = SelectedAllianceFile.Grids.Count - 1; i >= 0; i--)
            {
                if (myFiles.Any(x => Path.GetFileNameWithoutExtension(x) == SelectedAllianceFile.Grids[i].GridName))
                    continue;
                removedGrids++;
                SelectedAllianceFile.Grids.RemoveAt(i);
            }

            SelectedAllianceFile.Grids.AddRange(newGrids);
            SelectedAllianceFile.SaveFile();

            _chat?.Respond(
                $"Removed {removedGrids} grids and added {addedGrids} new grids to hangar for player {_steamId}");
        }

        public void Dispose()
        {
            Lock?.Dispose();
        }

        public bool CheckLimits(GridStamp grid, IEnumerable<MyObjectBuilder_CubeGrid> blueprint, long playerIdentityId)
        {
            return CheckGridLimits(grid, MySession.Static.Players.TryGetIdentity(playerIdentityId)) && BlockLimitChecker(blueprint, playerIdentityId);
        }


        public bool ParseInput(string selection, out int id)
        {
            if (int.TryParse(selection, out id))
                return true;
            for (var i = 0; i < SelectedAllianceFile.Grids.Count; i++)
            {
                var s = SelectedAllianceFile.Grids[i];
                if (s.GridName != selection) continue;
                id = i + 1;
                return true;
            }

            return false;
        }
    }


    [JsonObject(MemberSerialization.OptIn)]
    public class AllianceInfo
    {
        //This is the factions info file. Should contain methods for finding grids/checking timers 

        [JsonProperty] public List<GridStamp> Grids = new List<GridStamp>();
        [JsonProperty] public TimeStamp Timer;
        [JsonProperty] private int? _maxHangarSlots;
        [JsonProperty] private Dictionary<string, int> _serverOfferPurchases = new Dictionary<string, int>();
        [JsonProperty] public string Webhook = "default";

        public int MaxHangarSlots { get; set; }
        public string FilePath { get; set; }
        public ulong SteamId { get; set; }

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public int TotalBlocks { get; private set; }
        public int TotalPcu { get; private set; }
        public int StaticGrids { get; private set; }
        public int LargeGrids { get; private set; }
        public int SmallGrids { get; private set; }

        private Settings Config => Hangar.Config;

        public string AllianceFolderpath;


        public bool LoadFile(string folderPath, Guid allianceId)
        {
            AllianceFolderpath = Path.Combine(folderPath, allianceId.ToString());
            FilePath = Path.Combine(AllianceFolderpath, "AllianceInfo.json");

            GetMaxHangarSlot();

            if (!File.Exists(FilePath))
                return true;


            try
            {
                var scannedFile = JsonConvert.DeserializeObject<AllianceInfo>(File.ReadAllText(FilePath));
                Grids = scannedFile.Grids;
                Timer = scannedFile.Timer;
                _serverOfferPurchases = scannedFile._serverOfferPurchases;
                Webhook = scannedFile.Webhook;
                if (scannedFile._maxHangarSlots.HasValue)
                    MaxHangarSlots = scannedFile._maxHangarSlots.Value;
                PerformDataScan();
            }
            catch (Exception e)
            {
                Log.Warn(e, "For some reason the file is broken");
                return false;
            }


            return true;
        }

        private void PerformDataScan()
        {
            //Accumulate Grid Data
            foreach (var grid in Grids)
            {
                TotalBlocks += grid.NumberOfBlocks;
                TotalPcu += grid.GridPcu;

                StaticGrids += grid.StaticGrids;
                LargeGrids += grid.LargeGrids;
                SmallGrids += grid.SmallGrids;
            }
        }


        public bool CheckTimeStamp(out string response)
        {
            response = null;
            if (Timer == null)
                return true;

            var subtracted = DateTime.Now.Subtract(Timer.OldTime);
            var waitTimeSpawn = new TimeSpan(0, (int)Config.WaitTime, 0);
            var remainder = waitTimeSpawn - subtracted;
            if (!(subtracted.TotalMinutes <= Config.WaitTime)) return true;
            response = string.Format("{0:mm}min & {0:ss}s", remainder);
            return false;
        }

        public bool ReachedHangarLimit(int maxAmount)
        {
            return Grids.Count >= maxAmount;
        }

        public bool AnyGridsMatch(string gridName)
        {
            return Grids.Any(x => x.GridName.Equals(gridName, StringComparison.Ordinal));
        }

        public bool TryFindGridIndex(string gridName, int result)
        {
            var foundIndex = (short?)Grids.FindIndex(x => x.GridName.Equals(gridName, StringComparison.Ordinal));
            return foundIndex != -1;
        }

        public bool GetGrid<T>(T gridNameOrNumber, out GridStamp stamp, out string message)
        {
            message = string.Empty;
            stamp = null;

            switch (gridNameOrNumber)
            {
                case int _:
                    {
                        var target = Convert.ToInt32(gridNameOrNumber);
                        if (!IsInputValid(target, out message))
                            return false;


                        stamp = Grids[target - 1];
                        return true;
                    }
                case string _:
                    {
                        //IsInputValid(SelectedIndex); ;

                        if (!int.TryParse(Convert.ToString(gridNameOrNumber), out var selectedIndex))
                        {
                            stamp = Grids.FirstOrDefault(x => x.GridName == Convert.ToString(gridNameOrNumber));
                            if (stamp != null)
                                return true;
                        }


                        if (!IsInputValid(selectedIndex, out message))
                            return false;


                        stamp = Grids[selectedIndex - 1];
                        return true;
                    }
                default:
                    return false;
            }
        }

        private void GetMaxHangarSlot()
        {
            MaxHangarSlots = _maxHangarSlots.HasValue ? _maxHangarSlots.Value : Config.AllianceHangarAmount;
        }

        public bool IsInputValid(int index, out string message)
        {
            //Is input valid for index 1 - X
            message = string.Empty;

            if (index <= 0)
            {
                message = "Please input a positive non-zero number";
                return false;
            }

            if (index <= Grids.Count) return true;
            message = "This hangar slot is empty! Select a grid that is in your hangar!";
            return false;
        }

        public void FormatGridName(GridStamp result)
        {
            try
            {
                result.GridName = FileSaver.CheckInvalidCharacters(result.GridName);
                // Log.Warn("Running GridName Checks: {" + GridName + "} :" + Test);

                if (!AnyGridsMatch(result.GridName)) return;
                //There is already a grid with that name!
                var nameCheckDone = false;
                var a = 1;
                while (!nameCheckDone)
                {
                    if (AnyGridsMatch(result.GridName + "[" + a + "]"))
                    {
                        a++;
                    }
                    else
                    {
                        //Hangar.Debug("Name check done! " + a);
                        nameCheckDone = true;
                    }
                }

                //Main.Debug("Saving grid name: " + GridName);
                result.GridName = result.GridName + "[" + a + "]";
            }
            catch (Exception e)
            {
                Log.Warn(e);
            }
        }

        public void ServerOfferPurchased(string name)
        {
            var val = name.Trim();
            //Log.Info("SetServerOfferCount: " + Val);

            if (_serverOfferPurchases.ContainsKey(val))
            {
                _serverOfferPurchases[val] = _serverOfferPurchases[val] + 1;
            }
            else
            {
                _serverOfferPurchases.Add(val, 1);
            }
        }

        public int GetServerOfferPurchaseCount(string name)
        {
            var val = name.Trim();
            //Log.Info("GetServerOfferCount: " + Val);
            return _serverOfferPurchases.ContainsKey(val) ? _serverOfferPurchases[val] : 0;
        }

        public async void SaveFile()
        {
            Directory.CreateDirectory(AllianceFolderpath);
            await FileSaver.SaveAsync(FilePath, this);
        }
    }
}
