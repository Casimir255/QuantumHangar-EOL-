using NLog;
using QuantumHangar.Utils;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Torch.Commands;
using VRage.Game;
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

        private CommandContext Ctx;

        public AdminChecks(CommandContext Context, ulong TargetSteamId, long TargetIdentityId)
        {

            _inConsole = TryGetAdminPosition(Context.Player);
            _chat = new Chat(Context, _inConsole);
            Ctx = Context;
            _targetSteamId = TargetSteamId;
            _targetIdentityId = TargetIdentityId;
        }

        private bool TryGetAdminPosition(IMyPlayer Admin)
        {
            if (Admin == null)
                return true;


            _adminPlayerPosition = Admin.GetPosition();
            _adminUserCharacter = (MyCharacter)Admin.Character;

            return false;
        }


        public async void SaveGrid(string NameOrIdentity = "")
        {
            var Result = new GridResult(true);
            
            //Gets grids player is looking at
            if (!Result.GetGrids(_chat, _adminUserCharacter, NameOrIdentity))
                return;


            if (Result.OwnerSteamID == 0)
            {
                _chat?.Respond("Unable to get major grid owner!");
                return;
            }
            var stamp = Result.GenerateGridStamp();
            var PlayersHanger = new PlayerHangar(Result.OwnerSteamID, _chat, true);
            var Name = Result.OwnerSteamID.ToString();
            if (MySession.Static.Players.TryGetIdentityFromSteamID(Result.OwnerSteamID, out MyIdentity identity))
                Name = identity.DisplayName;

            PlayersHanger.SelectedPlayerFile.FormatGridName(stamp);

            var val = await PlayersHanger.SaveGridsToFile(Result, stamp.GridName);
            if (val)
            {
                PlayersHanger.SaveGridStamp(stamp, true);
                _chat?.Respond($"{stamp.GridName} was saved to {Name}'s hangar!");
            }
            else
            {
                _chat?.Respond($"{stamp.GridName} failed to send to {Name}'s hangar!");
                return;
            }

        }

        public void LoadGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, out var PlayerSteamID))
                return;

            var PlayersHanger = new PlayerHangar(PlayerSteamID, _chat, true);
            if (!PlayersHanger.TryGetGridStamp(ID, out var Stamp))
                return;


            if (!PlayersHanger.LoadGrid(Stamp, out var Grids))
            {
                Log.Error($"Loading grid {ID} failed for {NameOrSteamID}!");
                _chat.Respond("Loading grid failed! Report this to staff and check logs for more info!");
                return;
            }


            var LoadPos = Stamp.GridSavePosition;
            if (FromSavePos == false && _inConsole == true)
                FromSavePos = true;

            if (!FromSavePos)
                LoadPos = _adminPlayerPosition;



            var Spawner = new ParallelSpawner(Grids, _chat);
            if (Spawner.Start(LoadPos, FromSavePos))
            {
                _chat?.Respond($"Spawning Completed! \n Location: {LoadPos}");
                PlayersHanger.RemoveGridStamp(ID);
            }
            else
            {
                _chat?.Respond("An error occured while spawning the grid!");
            }


        }

        public void ListGrids(string NameOrSteamId)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamId, out var PlayerSteamID))
                return;

            var PlayersHanger = new PlayerHangar(PlayerSteamID, _chat, true);
            PlayersHanger.ListAllGrids();
        }

        public void SyncHangar(string NameOrSteamID)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, out var PlayerSteamID))
                return;

            var PlayersHanger = new PlayerHangar(PlayerSteamID, _chat, true);
            PlayersHanger.UpdateHangar();
        }

        public void SyncAll()
        {

            //Get All hangar folders
            foreach (var folder in Directory.GetDirectories(Hangar.MainPlayerDirectory))
            {
                var PlayerID = Path.GetFileName(folder);
                var ID = ulong.Parse(PlayerID);

                if (ID == 0)
                    continue;

                var PlayersHanger = new PlayerHangar(ID, _chat, true);
                PlayersHanger.UpdateHangar();
            }




        }

        public void RemoveGrid(string NameOrSteamID, int Index)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, out ulong PlayerSteamID))
                return;

            var PlayersHanger = new PlayerHangar(PlayerSteamID, _chat, true);
            if (PlayersHanger.RemoveGridStamp(Index))
                _chat.Respond("Successfully removed grid!");

        }

        public bool AdminTryGetPlayerSteamID(string NameOrSteamID, out ulong PSteamID)
        {
            ulong? SteamID;
            if (ulong.TryParse(NameOrSteamID, out var PlayerSteamID))
            {
                var Identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(PlayerSteamID, 0));

                if (Identity == null)
                {
                    _chat?.Respond(NameOrSteamID + " doesn't exist as an Identity!");
                    PSteamID = 0;
                    return false;
                }

                PSteamID = PlayerSteamID;
                return true;
            }
            try
            {
                var MPlayer = MySession.Static.Players.GetAllIdentities().FirstOrDefault(x => x.DisplayName.Equals(NameOrSteamID));
                SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);
            }
            catch (Exception)
            {
                //Hangar.Debug("Player "+ NameOrID + " doesn't exist on the server!", e, Hangar.ErrorType.Warn);
                _chat?.Respond("Player " + NameOrSteamID + " doesn't exist on the server!");
                PSteamID = 0;
                return false;
            }

            PSteamID = SteamID.Value;
            return true;
        }


    }
}
