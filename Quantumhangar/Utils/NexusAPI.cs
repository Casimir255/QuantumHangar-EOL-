using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRageMath;

namespace QuantumHangar.Utils
{
    public class NexusApi
    {
        public ushort CrossServerModId;

        /*  For recieving custom messages you have to register a message handler with a different unique ID then what you use server to client. (It should be the same as this class)
         *  
         *  NexusAPI(5432){
         *  CrossServerModID = 5432
         *  }
         *  
         *  
         *  Register this somewhere in your comms code. (This will only be raised when it recieves a message from another server)
         *  MyAPIGateway.Multiplayer.RegisterMessageHandler(5432, MessageHandler);
         */

        public NexusApi(ushort socketId)
        {
            CrossServerModId = socketId;
        }

        public static bool IsRunningNexus()
        {
            return false;
        }

        public static bool IsPlayerOnline(long identityId)
        {
            return false;
        }

        private static List<object[]> GetSectorsObject()
        {
            var apiSectors = new List<object[]>();
            return apiSectors;
        }

        private static List<object[]> GetAllOnlinePlayersObject()
        {
            var onlinePlayers = new List<object[]>();
            return onlinePlayers;
        }

        private static List<object[]> GetAllServersObject()
        {
            var servers = new List<object[]>();
            return servers;

        }
        private static List<object[]> GetAllOnlineServersObject()
        {
            var servers = new List<object[]>();
            return servers;

        }

        private static object[] GetThisServerObject()
        {
            var onlinePlayers = new object[6];
            return onlinePlayers;
        }


        public static Server GetThisServer()
        {
            var obj = GetThisServerObject();
            return new Server((string)obj[0], (int)obj[1], (short)obj[2], (int)obj[3], (int)obj[4], (List<ulong>)obj[5]);
        }

        public static List<Sector> GetSectors()
        {
            var objects = GetSectorsObject();

            return objects.Select(obj => new Sector((string)obj[0], (string)obj[1], (int)obj[2], (bool)obj[3], (Vector3D)obj[4], (double)obj[5], (int)obj[6])).ToList();
        }


        public static int GetServerIdFromPosition(Vector3D position)
        {
            return 0;
        }


        public static List<Player> GetAllOnlinePlayers()
        {
            var objects = GetAllOnlinePlayersObject();
            return objects.Select(obj => new Player((string)obj[0], (ulong)obj[1], (long)obj[2], (int)obj[3])).ToList();
        }


        public static List<Server> GetAllServers()
        {
            var objects = GetAllServersObject();
            return objects.Select(obj => new Server((string)obj[0], (int)obj[1], (int)obj[2], (string)obj[3])).ToList();
        }
        public static List<Server> GetAllOnlineServers()
        {
            var objects = GetAllOnlineServersObject();
            return objects.Select(obj => new Server((string)obj[0], (int)obj[1], (int)obj[2], (float)obj[3], (int)obj[4], (List<ulong>)obj[5])).ToList();
        }



        public static bool IsServerOnline(int serverId)
        {
            return false;
        }
        public static void BackupGrid(List<MyObjectBuilder_CubeGrid> gridObjectBuilders, long onwerIdentity)
        {
            
        }
        public static void SendChatMessageToDiscord(ulong channelId, string author, string message) { }
        public static void SendEmbedMessageToDiscord(ulong channelId, string embedTitle, string embedMsg, string embedFooter, string embedColor = null) { }

        public void SendMessageToServer(int serverId, byte[] message)
        {
            
        }

        public void SendMessageToAllServers(byte[] message)
        {
            
        }

        public class Sector
        {
            public readonly string Name;

            public readonly string IpAddress;

            public readonly int Port;

            public readonly bool IsGeneralSpace;

            public readonly Vector3D Center;

            public readonly double Radius;

            public readonly int ServerId;

            public Sector(string name, string ipAddress, int port, bool isGeneralSpace, Vector3D center, double radius, int serverId)
            {
                this.Name = name;
                this.IpAddress = ipAddress;
                this.Port = port;
                this.IsGeneralSpace = isGeneralSpace;
                this.Center = center;
                this.Radius = radius;
                this.ServerId = serverId;
            }

        }

        public class Player
        {

            public readonly string PlayerName;

            public readonly ulong SteamId;

            public readonly long IdentityId;

            public readonly int OnServer;

            public Player(string playerName, ulong steamId, long identityId, int onServer)
            {
                this.PlayerName = playerName;
                this.SteamId = steamId;
                this.IdentityId = identityId;
                this.OnServer = onServer;
            }
        }

        public class Server
        {
            public readonly string Name;
            public readonly int ServerId;
            public readonly int ServerType;
            public readonly string ServerIp;

            public readonly int MaxPlayers;
            public readonly float ServerSs;
            public readonly int TotalGrids;
            public readonly List<ulong> ReservedPlayers;

            /*  Possible Server Types
             * 
             *  0 - SyncedSectored
             *  1 - SyncedNon-Sectored
             *  2 - Non-Synced & Non-Sectored
             * 
             */


            public Server(string name, int serverId, int serverType, string ip)
            {
                this.Name = name;
                this.ServerId = serverId;
                this.ServerType = serverType;
                this.ServerIp = ip;
            }


            //Online Server
            public Server(string name, int serverId, int maxPlayers, float simSpeed, int totalGrids, List<ulong> reservedPlayers)
            {
                this.Name = name;
                this.ServerId = serverId;
                this.MaxPlayers = maxPlayers;
                this.ServerSs = simSpeed;
                this.TotalGrids = totalGrids;
                this.ReservedPlayers = reservedPlayers;
            }

        }


        [ProtoContract]
        public class CrossServerMessage
        {

            [ProtoMember(1)] public readonly int ToServerId;
            [ProtoMember(2)] public readonly int FromServerId;
            [ProtoMember(3)] public readonly ushort UniqueMessageId;
            [ProtoMember(4)] public readonly byte[] Message;

            public CrossServerMessage(ushort uniqueMessageId, int toServerId, int fromServerId, byte[] message)
            {
                this.UniqueMessageId = uniqueMessageId;
                this.ToServerId = toServerId;
                this.FromServerId = fromServerId;
                this.Message = message;
            }

            public CrossServerMessage() { }
        }
    }
}
