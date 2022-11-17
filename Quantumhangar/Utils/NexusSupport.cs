using NLog;
using ProtoBuf;
using QuantumHangar.HangarChecks;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI;
using System;
using System.Linq;
using VRage.Game;
using VRageMath;
using static QuantumHangar.Utils.CharacterUtilities;

namespace QuantumHangar.Utils
{
    public static class NexusSupport
    {
        private const ushort QuantumHangarNexusModId = 0x24fc;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly GpsSender GpsSender = new GpsSender();
        private static int _thisServerId = -1;
        private static bool _requireTransfer = true;
        
        public static NexusAPI Api { get; } = new NexusAPI(QuantumHangarNexusModId);
        public static bool RunningNexus { get; private set; }

        public static void Init()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(QuantumHangarNexusModId, ReceivePacket);
            _thisServerId = NexusAPI.GetThisServer().ServerId;
            Log.Info("QuantumHangar -> Nexus integration has been initilized with serverID " + _thisServerId);


            if (!NexusAPI.IsRunningNexus())
                return;

            Log.Error("Running Nexus!");

            RunningNexus = true;
            var ThisServer = NexusAPI.GetAllServers().FirstOrDefault(x => x.ServerId == _thisServerId);

            if (ThisServer.ServerType >= 1)
            {
                _requireTransfer = true;
                Log.Info("QuantumHangar -> This server is Non-Sectored!");
            }
        }

        public static void Dispose()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(QuantumHangarNexusModId, ReceivePacket);
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
        public static bool RelayLoadIfNecessary(Vector3D spawnPos, int ID, bool loadNearPlayer, Chat Chat,
            ulong SteamID, long IdentityID, Vector3D PlayerPosition)
        {
            //Don't continue if we aren't running nexus, or we don't require transfer due to non-Sectored instances
            if (!RunningNexus || !_requireTransfer)
                return false;

            var target = NexusAPI.GetServerIDFromPosition(spawnPos);
            if (target == _thisServerId)
                return false;

            if (!NexusAPI.IsServerOnline(target))
            {
                Chat?.Respond(
                    "Sorry, this grid belongs to another server that is currently offline. Please try again later.");
                return true;
            }

            Chat?.Respond("Sending hangar load command to the corresponding server, please wait...");
            var msg = new NexusHangarMessage
            {
                Type = NexusHangarMessageType.LoadGrid,
                SteamId = SteamID,
                LoadGridId = ID,
                LoadNearPlayer = loadNearPlayer,
                IdentityId = IdentityID,
                PlayerPosition = PlayerPosition,
                ServerId = _thisServerId
            };

            Api.SendMessageToServer(target, MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(msg));
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

            switch (msg.Type)
            {
                case NexusHangarMessageType.Chat:
                    var chat = new ScriptedChatMsg()
                    {
                        Author = msg.Sender,
                        Text = msg.Response,
                        Font = MyFontEnum.White,
                        Color = msg.Color,
                        Target = msg.ChatIdentityId
                    };
                    MyMultiplayerBase.SendScriptedChatMessage(ref chat);
                    return;

                case NexusHangarMessageType.SendGps:
                    GpsSender.SendGps(msg.Position, msg.Name, msg.EntityId);
                    return;

                case NexusHangarMessageType.LoadGrid:
                    var chatOverNexus = new Chat((text, color, sender) =>
                    {
                        var m = new NexusHangarMessage
                        {
                            Type = NexusHangarMessageType.Chat,
                            IdentityId = msg.IdentityId,
                            Response = text,
                            Color = color,
                            Sender = sender
                        };
                        Api.SendMessageToServer(msg.ServerId,
                            MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(m));
                    });

                    var gpsOverNexus = new GpsSender((position, name, entityID) =>
                    {
                        var m = new NexusHangarMessage
                        {
                            Type = NexusHangarMessageType.SendGps,
                            Name = name,
                            Position = position,
                            EntityId = entityID
                        };
                        Api.SendMessageToServer(msg.ServerId,
                            MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(m));
                    });

                    var User = new PlayerChecks(chatOverNexus, gpsOverNexus, msg.SteamId, msg.IdentityId,
                        msg.PlayerPosition);


                    User.LoadGrid(msg.LoadGridId.ToString(), msg.LoadNearPlayer);
                    return;
            }

            Log.Error("Invalid Nexus cross-server message for Quantum Hangar (unrecognized type: " + msg.Type + ")");
        }
    }

    public enum NexusHangarMessageType
    {
        Unset,
        LoadGrid,
        Chat,
        SendGps
    }

    [ProtoContract]
    public class NexusHangarMessage
    {
        [ProtoMember(1)] public NexusHangarMessageType Type;

        // Request to load grid: Type == LoadGrid
        [ProtoMember(2)] public long IdentityId;
        [ProtoMember(3)] public int LoadGridId;
        [ProtoMember(4)] public bool LoadNearPlayer;
        [ProtoMember(5)] public ulong SteamId;
        [ProtoMember(6)] public Vector3D PlayerPosition;
        [ProtoMember(7)] public int ServerId;

        // Relay chat response back: Type == Chat
        [ProtoMember(8)] public long ChatIdentityId;
        [ProtoMember(9)] public string Response;
        [ProtoMember(10)] public Color Color;
        [ProtoMember(11)] public string Sender;

        // Relay send GPS back: Type == SendGPS
        [ProtoMember(12)] public string Name;
        [ProtoMember(13)] public Vector3D Position;
        [ProtoMember(14)] public long EntityId;
    }
}