using NLog;
using QuantumHangar.HangarChecks;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VRageMath;

namespace QuantumHangar.Utils
{
    public static class CharacterUtilities
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    
        public static bool TryGetPlayerBalance(ulong steamID, out long balance)
        {
            try
            {
                //PlayerId Player = new MyPlayer.PlayerId(steamID);
                //Hangar.Debug("SteamID: " + steamID);
                long IdentityID = MySession.Static.Players.TryGetIdentityId(steamID);
                //Hangar.Debug("IdentityID: " + IdentityID);
                balance = MyBankingSystem.GetBalance(IdentityID);
                return true;
            }
            catch (Exception e)
            {
                //Hangar.Debug("Unkown keen player error!", e, Hangar.ErrorType.Fatal);
                balance = 0;
                return false;
            }


        }

        public static bool TryGetIdentityFromSteamID(this MyPlayerCollection Collection,  ulong SteamID, out MyIdentity Player)
        {

            Player = Collection.TryGetPlayerIdentity(new MyPlayer.PlayerId(SteamID, 0));

            if (Player == null)
                return false;

            return true;
        }

        public static bool TryGetPlayerSteamID(string NameOrSteamID, Chat Chat, out ulong PSteamID)
        {
            ulong? SteamID;
            if (UInt64.TryParse(NameOrSteamID, out ulong PlayerSteamID))
            {
                MyIdentity Identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(PlayerSteamID, 0));

                if (Identity == null)
                {
                    Chat?.Respond(NameOrSteamID + " doesnt exsist as an Identity! Dafuq?");
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
                Chat?.Respond("Invalid player format! Check logs for more details!");
                //Hangar.Debug("Player " + NameOrSteamID + " dosnt exist on the server!");
                PSteamID = 0;
                return false;
            }

            PSteamID = SteamID.Value;
            return true;
        }

        public static readonly string m_ScanPattern = "GPS:([^:]{0,32}):([\\d\\.-]*):([\\d\\.-]*):([\\d\\.-]*):";
        public static Vector3D GetGps(string text)
        {
            int num = 0;
            foreach (Match item in Regex.Matches(text, m_ScanPattern))
            {
                string value = item.Groups[1].Value;
                double value2;
                double value3;
                double value4;
                try
                {
                    value2 = double.Parse(item.Groups[2].Value, CultureInfo.InvariantCulture);
                    value2 = Math.Round(value2, 2);
                    value3 = double.Parse(item.Groups[3].Value, CultureInfo.InvariantCulture);
                    value3 = Math.Round(value3, 2);
                    value4 = double.Parse(item.Groups[4].Value, CultureInfo.InvariantCulture);
                    value4 = Math.Round(value4, 2);
                }
                catch (SystemException)
                {
                    continue;
                }

                return new Vector3D(value2, value3, value4);

            }

            return Vector3D.Zero;
        }


        // GpsSender is a class to send GPS coordinates to the player.
        public class GpsSender
        {

            // Normal object to send GPS directly, i.e. in the current game.
            public GpsSender() { }

            private Action<Vector3D, string, long> _send;

            // Delegate how to send the GPS, e.g. via a Nexus message.
            public GpsSender(Action<Vector3D, string, long> sender)
            {
                _send = sender;
            }

            // SendGps adds a yellow GPS point in the player GPS list that expires after 5 minutes.
            public void SendGps(Vector3D Position, string name, long EntityID)
            {
                if (_send != null)
                {
                    _send(Position, name, EntityID);
                    return;
                }
                MyGps myGps = new MyGps();
                myGps.ShowOnHud = true;
                myGps.Coords = Position;
                myGps.Name = name;
                myGps.Description = "Hangar location for loading grid at or around this position";
                myGps.AlwaysVisible = true;

                MyGps gps = myGps;
                gps.DiscardAt = TimeSpan.FromMinutes(MySession.Static.ElapsedPlayTime.TotalMinutes + 5);
                gps.GPSColor = Color.Yellow;
                MySession.Static.Gpss.SendAddGpsRequest(EntityID, ref gps, 0L, true);
            }
        }
    }
}
