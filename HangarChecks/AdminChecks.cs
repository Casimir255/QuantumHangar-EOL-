using NLog;
using QuantumHangar.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
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
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private readonly bool InConsole = false;

        public AdminChecks(CommandContext Context)
        {
            Chat = new Chat(Context);

            InConsole = TryGetAdminPosition(Context.Player);

        }

        private bool TryGetAdminPosition(IMyPlayer Admin)
        {
            if (Admin == null)
                return true;


            AdminPlayerPosition = Admin.GetPosition();
            AdminUserCharacter = (MyCharacter)Admin.Character;

            return false;
        }


        public void SaveGrid(string NameOrIdentity = "")
        {


            GridResult Result = new GridResult(true);


  
            //Gets grids player is looking at
            if (!Result.GetGrids(Chat, AdminUserCharacter, NameOrIdentity))
                return;

            GridStamp stamp = Result.GenerateGridStamp();
            

            PlayerHangar PlayersHanger = new PlayerHangar(Result.OwnerSteamID, Chat);


            GridUtilities.FormatGridName(PlayersHanger, stamp);
            if (PlayersHanger.SaveGridsToFile(Result, stamp.GridName))
            {

                PlayersHanger.SaveGridStamp(stamp, Result.BiggestOwner, true);
                Chat.Respond("Save Complete!");
            }
            else
            {
                Chat.Respond("Saved Failed!");
                return;
            }

        }

        public void LoadGrid(string NameOrSteamID, int ID, bool FromSavePos = true)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, Chat, out ulong PlayerSteamID))
                return;

            PlayerHangar PlayersHanger = new PlayerHangar(PlayerSteamID, Chat);

            if (!PlayersHanger.LoadGrid(ID, out IEnumerable<MyObjectBuilder_CubeGrid> Grids, out GridStamp Stamp))
                return;


            if (FromSavePos == false && InConsole == true)
                FromSavePos = true;


            ParallelSpawner Spawner = new ParallelSpawner(Grids, Chat, !FromSavePos);
            if (Spawner.Start(FromSavePos, AdminPlayerPosition))
            {
                Chat.Respond("Spawning Complete!");
                PlayersHanger.RemoveGridStamp(Stamp);
            }
            else
            {
                Chat.Respond("An error occured while spawning the grid!");
            }


        }

        public void ListGrids(string NameOrSteamID)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, Chat, out ulong PlayerSteamID))
                return;

            PlayerHangar PlayersHanger = new PlayerHangar(PlayerSteamID, Chat, true);
            PlayersHanger.ListAllGrids();
        }

        public void SyncHangar(string NameOrSteamID)
        {
            if (!AdminTryGetPlayerSteamID(NameOrSteamID, Chat, out ulong PlayerSteamID))
                return;

            PlayerHangar PlayersHanger = new PlayerHangar(PlayerSteamID, Chat, true);
            PlayersHanger.UpdateHangar();


        }


        public static bool AdminTryGetPlayerSteamID(string NameOrSteamID, Chat chat, out ulong PSteamID)
        {
            ulong? SteamID;
            if (UInt64.TryParse(NameOrSteamID, out ulong PlayerSteamID))
            {
                MyIdentity Identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(PlayerSteamID, 0));

                if (Identity == null)
                {
                    chat.Respond(NameOrSteamID + " doesnt exsist as an Identity!");
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
                    chat.Respond("Player " + NameOrSteamID + " dosnt exist on the server!");
                    PSteamID = 0;
                    return false;
                }
            }

            if (!SteamID.HasValue)
            {
                chat.Respond(NameOrSteamID +" doest exist! Check logs for more details!");
                PSteamID = 0;
                return false;
            }

            PSteamID = SteamID.Value;
            return true;
        }


    }
}
