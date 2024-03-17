using NLog;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.IO;
using System.Linq;
using Torch.Commands;
using VRage.Game.ModAPI;
using VRageMath;

namespace QuantumHangar.HangarChecks
{
    public class AdminChecks
    {
        public static Settings Config => Hangar.Config;
        private readonly Chat _chat;

        private readonly ulong _targetSteamId;
        private readonly long _targetIdentityId;

        private Vector3D _adminPlayerPosition;
        private MyCharacter _adminUserCharacter;
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(AdminChecks));
        private readonly bool _inConsole;

        private CommandContext _ctx;
        public bool SetForceSpawnNearPlayer(string NameOrSteamID, int Index)
        {
            AdminChecks.Log.Info(nameof(SetForceSpawnNearPlayer));
            ulong PSteamID;
            if (!this.AdminTryGetPlayerSteamID(NameOrSteamID, out PSteamID))
            {
                AdminChecks.Log.Info("SetForceSpawnNearPlayer: AdminTryGetPlayerSteamID failed");
                return false;
            }
            if (!new PlayerHangar(PSteamID, this._chat, true).ForceSpawnNearPlayer(Index))
            {
                AdminChecks.Log.Info("SetForceSpawnNearPlayer: ForceSpawnNearPlayer failed");
                return false;
            }
            this._chat?.Respond("Grid " + Index.ToString() + " reset.");
            return true;
        }

        public bool AdminTryGetPlayerSteamID(string NameOrSteamID, out ulong PSteamID)
        {
            ulong result;
            if (ulong.TryParse(NameOrSteamID, out result))
            {
                if (MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(result, 0)) == null)
                {
                    this._chat?.Respond(NameOrSteamID + " doesnt exsist as an Identity!");
                    PSteamID = 0UL;
                    return false;
                }
                PSteamID = result;
                return true;
            }
            ulong? nullable;
            try
            {
                nullable = new ulong?(MySession.Static.Players.TryGetSteamId(MySession.Static.Players.GetAllIdentities().FirstOrDefault<MyIdentity>((Func<MyIdentity, bool>)(x => x.DisplayName.Equals(NameOrSteamID))).IdentityId));
            }
            catch (Exception ex)
            {
                this._chat?.Respond("Player " + NameOrSteamID + " dosnt exist on the server!");
                PSteamID = 0UL;
                return false;
            }

            if (nullable == null)
            {
                this._chat?.Respond(NameOrSteamID + " doest exist! Check logs for more details!");
                PSteamID = 0UL;
                return false;
            }
            PSteamID = nullable.Value;
            return true;
        }


        public AdminChecks(CommandContext context)
        {

            _inConsole = TryGetAdminPosition(context.Player);
            _chat = new Chat(context, _inConsole);
            _ctx = context;
        }

        private bool TryGetAdminPosition(IMyPlayer admin)
        {
            if (admin == null)
                return true;


            _adminPlayerPosition = admin.GetPosition();
            _adminUserCharacter = (MyCharacter)admin.Character;

            return false;
        }


        public async void SaveGrid(string nameOrIdentity = "")
        {
            var result = new GridResult(true);

            //Gets grids player is looking at
            if (!result.GetGrids(_chat, _adminUserCharacter, nameOrIdentity))
                return;


            if (result.OwnerSteamId == 0)
            {
                _chat?.Respond("Unable to get major grid owner!");
                return;
            }
            var stamp = result.GenerateGridStamp();
            var playersHanger = new PlayerHangar(result.OwnerSteamId, _chat, true);
            var name = result.OwnerSteamId.ToString();
            if (MySession.Static.Players.TryGetPlayerBySteamId(result.OwnerSteamId, out MyPlayer player))
                name = player.DisplayName;

            playersHanger.SelectedPlayerFile.FormatGridName(stamp);

            var val = await playersHanger.SaveGridsToFile(result, stamp.GridName);
            if (val)
            {
                playersHanger.SaveGridStamp(stamp, true);
                _chat?.Respond($"{stamp.GridName} was saved to {name}'s hangar!");
            }
            else
            {
                _chat?.Respond($"{stamp.GridName} failed to send to {name}'s hangar!");
            }

        }

        public void LoadGrid(string nameOrSteamId, int id, bool fromSavePos = true)
        {
            if (!AdminTryGetPlayerSteamId(nameOrSteamId, out var playerSteamId))
                return;

            var playersHanger = new PlayerHangar(playerSteamId, _chat, true);
            if (!playersHanger.TryGetGridStamp(id, out var stamp))
                return;


            if (!playersHanger.LoadGrid(stamp, out var grids))
            {
                Log.Error($"Loading grid {id} failed for {nameOrSteamId}!");
                _chat.Respond("Loading grid failed! Report this to staff and check logs for more info!");
                return;
            }


            var loadPos = stamp.GridSavePosition;
            if (fromSavePos == false && _inConsole == true)
                fromSavePos = true;

            if (!fromSavePos)
                loadPos = _adminPlayerPosition;



            var spawner = new ParallelSpawner(grids, _chat, playerSteamId);
            spawner.setBounds(stamp.BoundingBox, stamp.Box, stamp.MatrixTranslation);
            if (spawner.Start(loadPos, fromSavePos))
            {
                _chat?.Respond($"Spawning Completed! \n Location: {loadPos}");
                playersHanger.RemoveGridStamp(id);
            }
            else
            {
                _chat?.Respond("An error occurred while spawning the grid!");
            }


        }

        public void ListGrids(string nameOrSteamId)
        {
            if (!AdminTryGetPlayerSteamId(nameOrSteamId, out var playerSteamId))
                return;

            var playersHanger = new PlayerHangar(playerSteamId, _chat, true);
            playersHanger.ListAllGrids();
        }

        public void SyncHangar(string nameOrSteamId)
        {
            if (!AdminTryGetPlayerSteamId(nameOrSteamId, out var playerSteamId))
                return;

            var playersHanger = new PlayerHangar(playerSteamId, _chat, true);
            playersHanger.UpdateHangar();
        }

        public void SyncAll()
        {

            //Get All hangar folders
            foreach (var folder in Directory.GetDirectories(Hangar.MainPlayerDirectory))
            {
                var playerId = Path.GetFileName(folder);
                var id = ulong.Parse(playerId);

                if (id == 0)
                    continue;

                var playersHanger = new PlayerHangar(id, _chat, true);
                playersHanger.UpdateHangar();
            }




        }

        public void RemoveGrid(string nameOrSteamId, int index)
        {
            if (!AdminTryGetPlayerSteamId(nameOrSteamId, out ulong playerSteamId))
                return;

            var playersHanger = new PlayerHangar(playerSteamId, _chat, true);
            if (playersHanger.RemoveGridStamp(index))
                _chat.Respond("Successfully removed grid!");

        }

        public bool AdminTryGetPlayerSteamId(string nameOrSteamId, out ulong pSteamId)
        {
            ulong? steamId;
            if (ulong.TryParse(nameOrSteamId, out var playerSteamId))
            {
                var identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(playerSteamId, 0));

                if (identity == null)
                {
                    _chat?.Respond(nameOrSteamId + " doesn't exist as an Identity!");
                    pSteamId = 0;
                    return false;
                }

                pSteamId = playerSteamId;
                return true;
            }
            try
            {
                var mPlayer = MySession.Static.Players.GetAllIdentities().FirstOrDefault(x => x.DisplayName.Equals(nameOrSteamId));
                steamId = MySession.Static.Players.TryGetSteamId(mPlayer.IdentityId);
            }
            catch (Exception)
            {
                //Hangar.Debug("Player "+ NameOrID + " doesn't exist on the server!", e, Hangar.ErrorType.Warn);
                _chat?.Respond("Player " + nameOrSteamId + " doesn't exist on the server!");
                pSteamId = 0;
                return false;
            }

            pSteamId = steamId.Value;
            return true;
        }


    }
}
