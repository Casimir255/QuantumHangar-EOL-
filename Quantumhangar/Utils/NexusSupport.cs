using NLog;
using ProtoBuf;
using QuantumHangar.HangarChecks;
using Sandbox.Engine.Multiplayer;
using Sandbox.ModAPI;
using System;
using System.Linq;
using Torch.API.Plugins;
using VRage.Game;
using VRageMath;
using static QuantumHangar.Utils.CharacterUtilities;

namespace QuantumHangar.Utils
{
    public static class NexusSupport
    {
        private const ushort QuantumHangarNexusModID = 0x24fc;

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public static NexusAPI API { get; } = new NexusAPI(QuantumHangarNexusModID);

        private static GpsSender gpsSender = new GpsSender();

        private static int ThisServerID = -1;
        public static bool RunningNexus { get; private set; } = false;
        private static bool RequireTransfer = true;



        public static void Init()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(QuantumHangarNexusModID, ReceivePacket);
            ThisServerID = NexusAPI.GetThisServer().ServerID;
            Log.Info("QuantumHangar -> Nexus integration has been initilized with serverID " + ThisServerID);


            if (!NexusAPI.IsRunningNexus())
                return;

            Log.Error("Running Nexus!");

            RunningNexus = true;
            NexusAPI.Server ThisServer = NexusAPI.GetAllServers().FirstOrDefault(x => x.ServerID == ThisServerID);

            if(ThisServer.ServerType >= 1)
            {
                RequireTransfer = true;
                Log.Info("QuantumHangar -> This server is Non-Sectored!");
            }

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
            //Dont contiue if we arent running nexus, or we dont require transfer due to non-Sectored instances
            if (!RunningNexus || !RequireTransfer)
                return false;

            var target = NexusAPI.GetServerIDFromPosition(spawnPos);
            if (target == ThisServerID) 
                return false;

            if (!NexusAPI.IsServerOnline(target))
            {
                Chat?.Respond("Sorry, this grid belongs to another server that is currently offline. Please try again later.");
                return true;
            }

            Chat?.Respond("Sending hangar load command to the corresponding server, please wait...");
            NexusHangarMessage msg = new NexusHangarMessage
            {
                Type = NexusHangarMessageType.LoadGrid,
                SteamID = SteamID,
                LoadGridID = ID,
                LoadNearPlayer = loadNearPlayer,
                IdentityID = IdentityID,
                PlayerPosition = PlayerPosition,
                ServerID = ThisServerID
            };

            API.SendMessageToServer(target, MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(msg));
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
                        Target = msg.ChatIdentityID
                    };
                    MyMultiplayerBase.SendScriptedChatMessage(ref chat);
                    return;

                case NexusHangarMessageType.SendGPS:
                    gpsSender.SendGps(msg.Position, msg.Name, msg.EntityID);
                    return;

                case NexusHangarMessageType.LoadGrid:
                    var chatOverNexus = new Chat((text, color, sender) =>
                    {
                        NexusHangarMessage m = new NexusHangarMessage
                        {
                            Type = NexusHangarMessageType.Chat,
                            IdentityID = msg.IdentityID,
                            Response = text,
                            Color = color,
                            Sender = sender,
                        };
                        API.SendMessageToServer(msg.ServerID, MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(m));
                    });

                    var gpsOverNexus = new GpsSender((position, name, entityID) =>
                    {
                        NexusHangarMessage m = new NexusHangarMessage
                        {
                            Type = NexusHangarMessageType.SendGPS,
                            Name = name,
                            Position = position,
                            EntityID = entityID,
                        };
                        API.SendMessageToServer(msg.ServerID, MyAPIGateway.Utilities.SerializeToBinary<NexusHangarMessage>(m));
                    });

                    PlayerChecks User = new PlayerChecks(chatOverNexus, gpsOverNexus, msg.SteamID, msg.IdentityID, msg.PlayerPosition);
                    User.LoadGrid(msg.LoadGridID, msg.LoadNearPlayer);
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
        SendGPS
    }

    [ProtoContract]
    public class NexusHangarMessage
    {
        [ProtoMember(1)] public NexusHangarMessageType Type;

        // Request to load grid: Type == LoadGrid
        [ProtoMember(2)] public long IdentityID;
        [ProtoMember(3)] public int LoadGridID;
        [ProtoMember(4)] public bool LoadNearPlayer;
        [ProtoMember(5)] public ulong SteamID;
        [ProtoMember(6)] public Vector3D PlayerPosition;
        [ProtoMember(7)] public int ServerID;

        // Relay chat response back: Type == Chat
        [ProtoMember(8)] public long ChatIdentityID;
        [ProtoMember(9)] public string Response;
        [ProtoMember(10)] public Color Color;
        [ProtoMember(11)] public string Sender;

        // Relay send GPS back: Type == SendGPS
        [ProtoMember(12)] public string Name;
        [ProtoMember(13)] public Vector3D Position;
        [ProtoMember(14)] public long EntityID;
    }
}
