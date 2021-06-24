using Nexus;
using Nexus.API;
using NLog;
using ProtoBuf;
using QuantumHangar.HangarChecks;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI;
using System;
using Torch.API.Plugins;
using VRage.Game;
using VRageMath;

namespace QuantumHangar.Utils
{
    public class NexusSupport
    {
        private const ushort QuantumHangarNexusModID = 0x24fc;

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static NexusAPI _api = new NexusAPI(QuantumHangarNexusModID);

        public static void Init(ITorchPlugin Plugin)
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(QuantumHangarNexusModID, ReceivePacket);
        }

        public static void Dispose()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(QuantumHangarNexusModID, ReceivePacket);
        }

        // RelayLoadIfNecessary relays the load grid command to another server if necessary.
        //
        // The conditions are:
        // - this server runs with the Nexus plugin and connected to a controller
        // - the spawn position belongs to another server linked with Nexus
        //
        // Relaying the command allows the load to run locally on the target server, and running
        // all the checks and extra work (e.g. digging voxels) that would otherwise not happen,
        // as the grid would simply be transferred to the other sector after being loaded.
        //
        // Returns true if the load grid command was relayed and there is nothing else to do,
        // false otherwise, meaning the load must happen locally.
        //
        // If the target server is offline, it will refuse to load the grid and the player
        // must try again later when the target server is online.
        public static bool RelayLoadIfNecessary(Vector3D spawnPos, int ID, bool loadNearPlayer, Chat Chat, ulong SteamID, long IdentityID, Vector3D PlayerPosition)
        {
            if (!IsRunningNexus()) return false;

            var target = GetServerIDFromPosition(spawnPos);
            if (target == Main.Config.ServerID) return false;

            if (!IsServerOnline(target))
            {
                Chat?.Respond("Sorry, this grid belongs to another server that is currently offline. Please try again later.");
                return true;
            }

            Chat?.Respond("Sending hangar load command to the corresponding server, please wait...");

            NexusHangarMessage msg = new NexusHangarMessage
            {
                SteamID = SteamID,
                LoadGridID = ID,
                LoadNearPlayer = loadNearPlayer,
                IdentityID = IdentityID,
                PlayerPosition = PlayerPosition,
                ServerID = Main.Config.ServerID
            };
            SendMessageToServer(target, MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(msg));
            return true;
        }

        private static void ReceivePacket(ushort HandlerId, byte[] Data, ulong SteamID, bool FromServer)
        {
            // Only consider trusted server messages, i.e. from Nexus itself, not untrusted player messages.
            if (!FromServer) return;

            NexusHangarMessage msg;
            try
            {
                msg = MyAPIGateway.Utilities.SerializeFromBinary<NexusHangarMessage>(Data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Invalid Nexus cross-server message for Quantum Hangar");
                return;
            }

            // The remote server loading the grid wants to respond to the player on our server.
            if (msg.Response != null)
            {
                var chat = new ScriptedChatMsg()
                {
                    Author = msg.Sender,
                    Text = msg.Response,
                    Font = MyFontEnum.White,
                    Color = msg.Color,
                    Target = msg.IdentityID
                };
                MyMultiplayerBase.SendScriptedChatMessage(ref chat);
                return;
            }

            // Load grid locally and relay chat responses back to the other server where the player is.
            var chatOverNexus = new Chat((text, color, sender) =>
            {
                NexusHangarMessage m = new NexusHangarMessage
                {
                    IdentityID = msg.IdentityID,
                    Response = text,
                    Color = color,
                    Sender = sender,
                };
                SendMessageToServer(msg.ServerID, MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(m));
            });

            PlayerChecks User = new PlayerChecks(chatOverNexus, msg.SteamID, msg.IdentityID, msg.PlayerPosition);
            User.LoadGrid(msg.LoadGridID, msg.LoadNearPlayer);
        }

        #region Convenience wrappers around NexusServerSideAPI
        private static bool IsRunningNexus()
        {
            bool result = false;
            NexusServerSideAPI.IsRunningNexus(ref result);
            return result;
        }

        private static int GetServerIDFromPosition(Vector3D position)
        {
            int result = 0;
            NexusServerSideAPI.GetServerIDFromPosition(ref result, position);
            return result;
        }

        private static bool IsServerOnline(int ServerID)
        {
            bool result = false;
            NexusServerSideAPI.IsServerOnline(ref result, ServerID);
            return result;
        }

        private static void SendMessageToServer(int ServerID, byte[] Message)
        {
            NexusServerSideAPI.SendMessageToServer(ref _api, ServerID, Message);
        }
        #endregion
    }

    [ProtoContract]
    public class NexusHangarMessage
    {
        // Common field for both load grid and chat response.
        [ProtoMember(1)] public long IdentityID;

        // Request to load grid.
        [ProtoMember(2)] public int LoadGridID;
        [ProtoMember(3)] public bool LoadNearPlayer;
        [ProtoMember(4)] public ulong SteamID;
        [ProtoMember(5)] public Vector3D PlayerPosition;
        [ProtoMember(6)] public int ServerID;

        // Relay chat response back.
        [ProtoMember(7)] public string Response;
        [ProtoMember(8)] public Color Color;
        [ProtoMember(9)] public string Sender;
    }
}
