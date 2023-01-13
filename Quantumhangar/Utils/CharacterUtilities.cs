using NLog;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using VRage.Game.Entity;
using VRageMath;

namespace QuantumHangar.Utils
{
    public static class CharacterUtilities
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();


        public static bool TryGetPlayerBalance(ulong steamId, out long balance)
        {
            try
            {
                //PlayerId Player = new MyPlayer.PlayerId(steamID);
                //Hangar.Debug("SteamID: " + steamID);
                var identityId = MySession.Static.Players.TryGetIdentityId(steamId);
                //Hangar.Debug("IdentityID: " + IdentityID);
                balance = MyBankingSystem.GetBalance(identityId);
                return true;
            }
            catch (Exception)
            {
                //Hangar.Debug("Unkown keen player error!", e, Hangar.ErrorType.Fatal);
                balance = 0;
                return false;
            }
        }

        public static bool TryGetIdentityFromSteamId(this MyPlayerCollection collection, ulong steamId,
            out MyIdentity player)
        {
            player = collection.TryGetPlayerIdentity(new MyPlayer.PlayerId(steamId, 0));

            return player != null;
        }

        public static bool TryGetPlayerSteamId(string nameOrSteamId, Chat chat, out ulong pSteamId)
        {
            ulong? steamId = null;
            if (ulong.TryParse(nameOrSteamId, out var playerSteamId))
            {
                var identity = MySession.Static.Players.TryGetPlayerIdentity(new MyPlayer.PlayerId(playerSteamId, 0));

                if (identity == null)
                {
                    chat?.Respond(nameOrSteamId + " doesn't exist as an Identity! Dafuq?");
                    pSteamId = 0;
                    return false;
                }

                pSteamId = playerSteamId;
                return true;
            }

            try
            {
                var myPlayer = MySession.Static.Players.GetAllIdentities()
                    .FirstOrDefault(x => x.DisplayName.Equals(nameOrSteamId));
                if (myPlayer != null) steamId = MySession.Static.Players.TryGetSteamId(myPlayer.IdentityId);
            }
            catch (Exception)
            {
                //Hangar.Debug("Player "+ NameOrID + " doesn't exist on the server!", e, Hangar.ErrorType.Warn);
                chat?.Respond("Player " + nameOrSteamId + " doesn't exist on the server!");
                pSteamId = 0;
                return false;
            }

            if (!steamId.HasValue)
            {
                chat?.Respond("Invalid player format! Check logs for more details!");
                //Hangar.Debug("Player " + NameOrSteamID + " doesn't exist on the server!");
                pSteamId = 0;
                return false;
            }

            pSteamId = steamId.Value;
            return true;
        }

        public static readonly string MScanPattern = "GPS:([^:]{0,32}):([\\d\\.-]*):([\\d\\.-]*):([\\d\\.-]*):";

        public static Vector3D GetGps(string text)
        {
            var num = 0;
            foreach (Match item in Regex.Matches(text, MScanPattern))
            {
                var value = item.Groups[1].Value;
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
            public GpsSender()
            {
            }

            private readonly Action<Vector3D, string, long, int, Color, string> _send;

            // Delegate how to send the GPS, e.g. via a Nexus message.
            public GpsSender(Action<Vector3D, string, long, int, Color, string> sender)
            {
                _send = sender;
            }

            // SendGps adds a yellow GPS point in the player GPS list that expires after 5 minutes.
            public void SendGps(Vector3D position, string name, long entityId, int time = 5, Color color = default(Color), string desc = "Hangar location for loading grid at or around this position")
            {
                if (_send != null)
                {
                    _send(position, name, entityId, time, color, desc);
                    return;
                }

                var myGps = new MyGps
                {
                    ShowOnHud = true,
                    Coords = position,
                    Name = name,
                    Description = desc,
                    AlwaysVisible = true

                };

                var gps = myGps;
                gps.DiscardAt = TimeSpan.FromMinutes(MySession.Static.ElapsedPlayTime.TotalMinutes + time);

                gps.GPSColor = Color.Yellow;
                MySession.Static.Gpss.SendAddGpsRequest(entityId, ref gps, 0L, true);
            }

            public void SendLinkedGPS(Vector3D position, MyEntity ent,  string name, long entityId, int time = 5, Color color = default(Color), string desc = "Hangar location for loading grid at or around this position")
            {
                if (_send != null)
                {
                    _send(position, name, entityId, time, color, desc);
                    return;
                }

                var myGps = new MyGps
                {
                    ShowOnHud = true,
                    Coords = position,
                    Name = name,
                    Description = desc,
                    AlwaysVisible = true
                };

                var gps = myGps;
                gps.DiscardAt = TimeSpan.FromMinutes(MySession.Static.ElapsedPlayTime.TotalMinutes + time);
                gps.SetEntity(ent);
                gps.GPSColor = Color.Yellow;
                MySession.Static.Gpss.SendAddGpsRequest(entityId, ref gps, 0L, true);
            }
        }
    }
}