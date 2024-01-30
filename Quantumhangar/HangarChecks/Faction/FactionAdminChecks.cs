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
    public class FactionAdminChecks
    {
        public MyFaction Faction;

        public Chat _chat;

        public CommandContext ctx;

        public string tag;

        private FactionHanger FactionsHanger { get; set; }

        public static Settings Config => Hangar.Config;


        // PlayerChecks as initiated by another server to call LoadGrid.
        // We don't have a command context nor a player character object to work with,
        // but we receive all required data in the Nexus message.
        public FactionAdminChecks(string tag, CommandContext ctx)
        {
            this.tag = tag;
            _chat = new Chat(ctx);
            this.ctx = ctx;
        }

        public FactionAdminChecks(CommandContext ctx)
        {
            _chat = new Chat(ctx);
            this.ctx = ctx;
        }

        public bool initHangar(string tag)
        {
            Faction = MySession.Static.Factions.TryGetFactionByTag(tag);
            if (Faction == null)
            {
                return false;
            }
            var id = Faction.Members.First().Key;
            var sid = MySession.Static.Players.TryGetSteamId(id);
            FactionsHanger = new FactionHanger(sid, new Chat(ctx));
            return true;
        }

        private bool PerformMainChecks(bool isSaving)
        {
            if (!Config.PluginEnabled)
            {
                _chat?.Respond("Plugin is not enabled!");
                return false;
            }
            if (Faction == null)
            {
                _chat?.Respond("Faction not found");
                return false;
            }

            if (FactionHanger.IsServerSaving(_chat))
            {
                _chat?.Respond("Server is saving or is paused!");
                return false;
            }

            return true;

        }

        public async void ChangeWebhook(string webhook)
        {
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
            FactionsHanger.ChangeWebhook(webhook);
            _chat?.Respond("Webhook changed");
        }

        public async void ChangeWhitelist(string targetNameOrSteamId)
        {
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
            if (TryGetPlayerSteamId(targetNameOrSteamId, _chat, out ulong steamId))
            {
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
        
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public async void SaveGrid(string name)
        {
            
            var result = new GridResult(true);
            //Gets grids player is looking at
            if (!result.GetGrids(_chat, (MyCharacter)ctx.Player?.Character, name))
                return;
            
            if (result.OwnerSteamId == 0)
            {
                _chat?.Respond("Unable to get major grid owner!");
                return;
            }

            Faction = MySession.Static.Factions.GetPlayerFaction(result.BiggestOwner);
            FactionsHanger = new FactionHanger(result.OwnerSteamId, _chat, true);
            
            if (!PerformMainChecks(true))
                return;

            //Calculates incoming grids data
            var gridData = result.GenerateGridStamp();



            //Checks for single and all slot block and grid limits
            

            FactionsHanger.SelectedFactionFile.FormatGridName(gridData);

            var val = await FactionsHanger.SaveGridsToFile(result, gridData.GridName, Faction.Members.First().Key);
            if (val)
            {
                FactionsHanger.SaveGridStamp(gridData);
                _chat?.Respond("Save Complete!");
                FactionsHanger.SendWebHookMessage($"Admin saved grid {gridData.GridName}");
            }
            else
            {
                _chat?.Respond("Saved Failed!");
            }
        }
        
        public void ListWhitelist()
        {
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
            FactionsHanger.ListAllWhitelisted();
        }
        
        public void ListGrids()
        {
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
            FactionsHanger.ListAllGrids();
        }

        public void DetailedInfo(string input)
        {
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
            if (!FactionsHanger.ParseInput(input, out var id))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }

            FactionsHanger.DetailedReport(id);
        }

        public async void LoadGrid(string input, bool loadNearPlayer)
        {
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
            if (!PerformMainChecks(false))
                return;


            if (!FactionsHanger.ParseInput(input, out var id))
            {
                _chat.Respond($"Grid {input} could not be found!");
                return;
            }


            if (!FactionsHanger.TryGetGridStamp(id, out var stamp))
                return;
            


            if (!FactionsHanger.LoadGrid(stamp, out var grids, Faction.Members.First().Key))
            {
                Log.Error($"Loading grid {id} failed for Admin!");
                _chat.Respond("Loading grid failed! Report this to staff and check logs for more info!");
                return;
            }

            var myObjectBuilderCubeGrids = grids as MyObjectBuilder_CubeGrid[] ?? grids.ToArray();
            

            PluginDependencies.BackupGrid(myObjectBuilderCubeGrids.ToList(), Faction.Members.First().Key);


            var pos = ctx.Player?.Character?.GetPosition();
            
            var spawnPos = DetermineSpawnPosition(stamp.GridSavePosition, pos.GetValueOrDefault(), out var keepOriginalPosition,
                loadNearPlayer);
            

            if (PluginDependencies.NexusInstalled && Config.NexusApi &&
                NexusSupport.RelayLoadIfNecessary(spawnPos, id, loadNearPlayer, _chat, ctx.Player!.SteamUserId, Faction.Members.First().Key,
                    ctx.Player!.GetPosition()))
                return;
            var sid = MySession.Static.Players.TryGetSteamId(Faction.Members.First().Key);
            var spawner = new ParallelSpawner(myObjectBuilderCubeGrids, _chat, sid, SpawnedGridsSuccessful);
            spawner.setBounds(stamp.BoundingBox, stamp.Box, stamp.MatrixTranslation);

            Log.Info("Attempting Grid Spawning @" + spawnPos.ToString());
            if (spawner.Start(spawnPos, keepOriginalPosition))
            {
                _chat?.Respond("Spawning Complete!");
                FactionsHanger.RemoveGridStamp(id);
                FactionsHanger.SendWebHookMessage("Admin loaded grid {stamp.GridName}");
            }
            else
            {
                //_chat?.Respond("An error occured while spawning the grid!");
            }
        }

        public void SellGrid(int id, long price, string description)
        {
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
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
            if (!initHangar(tag))
            {
                _chat?.Respond("that faction does not exist");
                return;
            }
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
            
            if (ctx.Player == null)
                return;
            
            if (biggestGrid != null && ctx.Player?.IdentityId != 0)
                new GpsSender().SendGps(biggestGrid.PositionComp.GetPosition(), biggestGrid.DisplayName,
                        ctx.Player.IdentityId);
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
        
        

        
    }
}