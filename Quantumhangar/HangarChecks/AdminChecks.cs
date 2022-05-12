using NLog;
using QuantumHangar.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Commands;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace QuantumHangar.HangarChecks
{
    public class AdminChecks
    {
        public static Settings Config { get { return Hangar.Config; } }
        private readonly Chat Chat;

        private readonly ulong TargetSteamID;
        private readonly long TargetIdentityID;

        private Vector3D AdminPlayerPosition;
        private MyCharacter AdminUserCharacter;
        private static readonly Logger Log = LogManager.GetLogger("Hangar." + nameof(AdminChecks));
        private readonly bool InConsole = false;

        private CommandContext Ctx;

        public AdminChecks(CommandContext Context)
        {

            InConsole = TryGetAdminPosition(Context.Player);
            Chat = new Chat(Context, InConsole);
            Ctx = Context;
        }

        private bool TryGetAdminPosition(IMyPlayer Admin)
        {
            if (Admin == null)
                return true;


            AdminPlayerPosition = Admin.GetPosition();
            AdminUserCharacter = (MyCharacter)Admin.Character;

            return false;
        }


        public async void SaveGrid(string NameOrIdentity = "")
        {


            GridResult Result = new GridResult(true);



            //Gets grids player is looking at
            if (!Result.GetGrids(Chat, AdminUserCharacter, NameOrIdentity))
                return;


            if (Result.OwnerSteamID == 0)
            {
                Chat?.Respond("Unable to get major grid owner!");
                return;
            }


            GridStamp stamp = Result.GenerateGridStamp();
            PlayerHangar PlayersHanger = new PlayerHangar(Result.OwnerSteamID, Chat, true);

            string Name = Result.OwnerSteamID.ToString();
            if (MySession.Static.Players.TryGetIdentityFromSteamID(Result.OwnerSteamID, out MyIdentity identity))
                Name = identity.DisplayName;

            PlayersHanger.SelectedPlayerFile.FormatGridName(stamp);

            bool val = await PlayersHanger.SaveGridsToFile(Result, stamp.GridName);
            if (val)
            {
                PlayersHanger.SaveGridStamp(stamp, true);
                Chat?.Respond($"{stamp.GridName} was saved to {Name}'s hangar!");
            }
            else
            {
                Chat?.Respond($"{stamp.GridName} failed to send to {Name}'s hangar!");
                return;
            }

        }

        public void LoadGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, out ulong PlayerSteamID))
                return;

            PlayerHangar PlayersHanger = new PlayerHangar(PlayerSteamID, Chat, true);
            if (!PlayersHanger.TryGetGridStamp(ID, out GridStamp Stamp))
                return;


            if (!PlayersHanger.LoadGrid(Stamp, out IEnumerable<MyObjectBuilder_CubeGrid> Grids))
            {
                Log.Error($"Loading grid {ID} failed for {NameOrSteamID}!");
                Chat.Respond("Loading grid failed! Report this to staff and check logs for more info!");
                return;
            }


            Vector3D LoadPos = Stamp.GridSavePosition;
            if (FromSavePos == false && InConsole == true)
                FromSavePos = true;

            if (!FromSavePos)
                LoadPos = AdminPlayerPosition;



            ParallelSpawner Spawner = new ParallelSpawner(Grids, Chat);
            if (Spawner.Start(LoadPos, FromSavePos))
            {
                Chat?.Respond($"Spawning Completed! \n Location: {LoadPos}");
                PlayersHanger.RemoveGridStamp(ID);
            }
            else
            {
                Chat?.Respond("An error occured while spawning the grid!");
            }


        }

        public void ListGrids(string NameOrSteamID)
        {


            if (!AdminTryGetPlayerSteamID(NameOrSteamID, out ulong PlayerSteamID))
                return;


            PlayerHangar PlayersHanger = new PlayerHangar(PlayerSteamID, Chat, true);
            PlayersHanger.ListAllGrids();
        }

        public void SyncHangar(string NameOrSteamID)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, out ulong PlayerSteamID))
                return;

            PlayerHangar PlayersHanger = new PlayerHangar(PlayerSteamID, Chat, true);
            PlayersHanger.UpdateHangar();
        }

        public void SyncAll()
        {

            //Get All hangar folders
            foreach (var folder in Directory.GetDirectories(Hangar.MainPlayerDirectory))
            {
                string PlayerID = Path.GetFileName(folder);

                ulong ID = UInt64.Parse(PlayerID);


                if (ID == 0)
                    continue;


                PlayerHangar PlayersHanger = new PlayerHangar(ID, Chat, true);
                PlayersHanger.UpdateHangar();
            }




        }

        public void RemoveGrid(string NameOrSteamID, int Index)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, out ulong PlayerSteamID))
                return;

            PlayerHangar PlayersHanger = new PlayerHangar(PlayerSteamID, Chat, true);
            if (PlayersHanger.RemoveGridStamp(Index))
                Chat.Respond("Successfully removed grid!");

        }

        public bool AdminTryGetPlayerSteamID(string NameOrSteamID, out ulong PSteamID)
        {
            ulong? SteamID;
            if (UInt64.TryParse(NameOrSteamID, out ulong PlayerSteamID))
            {
                MyIdentity Identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(PlayerSteamID, 0));

                if (Identity == null)
                {
                    Chat?.Respond(NameOrSteamID + " doesnt exsist as an Identity!");
                    PSteamID = 0;
                    return false;
                }

                PSteamID = PlayerSteamID;
                return true;
            }
            else
            {
                try
                {
                    MyIdentity MPlayer;
                    MPlayer = MySession.Static.Players.GetAllIdentities().FirstOrDefault(x => x.DisplayName.Equals(NameOrSteamID));
                    SteamID = MySession.Static.Players.TryGetSteamId(MPlayer.IdentityId);
                }
                catch (Exception e)
                {
                    //Hangar.Debug("Player "+ NameOrID + " dosnt exist on the server!", e, Hangar.ErrorType.Warn);
                    Chat?.Respond("Player " + NameOrSteamID + " dosnt exist on the server!");
                    PSteamID = 0;
                    return false;
                }
            }

            if (!SteamID.HasValue)
            {
                Chat?.Respond(NameOrSteamID + " doest exist! Check logs for more details!");
                PSteamID = 0;
                return false;
            }

            PSteamID = SteamID.Value;
            return true;
        }


    }
}
