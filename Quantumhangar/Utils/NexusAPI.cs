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
    public class NexusAPI
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

        public NexusAPI(ushort SocketID)
        {
            CrossServerModId = SocketID;
        }

        public static bool IsRunningNexus()
        {
            return false;
        }

        public static bool IsPlayerOnline(long IdentityID)
        {
            return false;
        }

        private static List<object[]> GetSectorsObject()
        {
            var APISectors = new List<object[]>();
            return APISectors;
        }

        private static List<object[]> GetAllOnlinePlayersObject()
        {
            var OnlinePlayers = new List<object[]>();
            return OnlinePlayers;
        }

        private static List<object[]> GetAllServersObject()
        {
            var Servers = new List<object[]>();
            return Servers;

        }
        private static List<object[]> GetAllOnlineServersObject()
        {
            var Servers = new List<object[]>();
            return Servers;

        }

        private static object[] GetThisServerObject()
        {
            var OnlinePlayers = new object[6];
            return OnlinePlayers;
        }


        public static Server GetThisServer()
        {
            var obj = GetThisServerObject();
            return new Server((string)obj[0], (int)obj[1], (short)obj[2], (int)obj[3], (int)obj[4], (List<ulong>)obj[5]);
        }

        public static List<Sector> GetSectors()
        {
            var Objects = GetSectorsObject();

            return Objects.Select(obj => new Sector((string)obj[0], (string)obj[1], (int)obj[2], (bool)obj[3], (Vector3D)obj[4], (double)obj[5], (int)obj[6])).ToList();
        }


        public static int GetServerIDFromPosition(Vector3D Position)
        {
            return 0;
        }


        public static List<Player> GetAllOnlinePlayers()
        {
            var Objects = GetAllOnlinePlayersObject();
            return Objects.Select(obj => new Player((string)obj[0], (ulong)obj[1], (long)obj[2], (int)obj[3])).ToList();
        }


        public static List<Server> GetAllServers()
        {
            var Objects = GetAllServersObject();
            return Objects.Select(obj => new Server((string)obj[0], (int)obj[1], (int)obj[2], (string)obj[3])).ToList();
        }
        public static List<Server> GetAllOnlineServers()
        {
            var Objects = GetAllOnlineServersObject();
            return Objects.Select(obj => new Server((string)obj[0], (int)obj[1], (int)obj[2], (float)obj[3], (int)obj[4], (List<ulong>)obj[5])).ToList();
        }



        public static bool IsServerOnline(int ServerID)
        {
            return false;
        }
        public static void BackupGrid(List<MyObjectBuilder_CubeGrid> GridObjectBuilders, long OnwerIdentity)
        {
            
        }
        public static void SendChatMessageToDiscord(ulong ChannelID, string Author, string Message) { }
        public static void SendEmbedMessageToDiscord(ulong ChannelID, string EmbedTitle, string EmbedMsg, string EmbedFooter, string EmbedColor = null) { }

        public void SendMessageToServer(int ServerID, byte[] Message)
        {
            
        }

        public void SendMessageToAllServers(byte[] Message)
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

            public Sector(string Name, string IpAddress, int Port, bool IsGeneralSpace, Vector3D Center, double Radius, int ServerID)
            {
                this.Name = Name;
                this.IpAddress = IpAddress;
                this.Port = Port;
                this.IsGeneralSpace = IsGeneralSpace;
                this.Center = Center;
                this.Radius = Radius;
                this.ServerId = ServerID;
            }

        }

        public class Player
        {

            public readonly string PlayerName;

            public readonly ulong SteamId;

            public readonly long IdentityId;

            public readonly int OnServer;

            public Player(string PlayerName, ulong SteamId, long IdentityId, int OnServer)
            {
                this.PlayerName = PlayerName;
                this.SteamId = SteamId;
                this.IdentityId = IdentityId;
                this.OnServer = OnServer;
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


            public Server(string Name, int ServerId, int ServerType, string Ip)
            {
                this.Name = Name;
                this.ServerId = ServerId;
                this.ServerType = ServerType;
                this.ServerIp = Ip;
            }


            //Online Server
            public Server(string Name, int ServerId, int MaxPlayers, float SimSpeed, int TotalGrids, List<ulong> ReservedPlayers)
            {
                this.Name = Name;
                this.ServerId = ServerId;
                this.MaxPlayers = MaxPlayers;
                this.ServerSs = SimSpeed;
                this.TotalGrids = TotalGrids;
                this.ReservedPlayers = ReservedPlayers;
            }

        }


        [ProtoContract]
        public class CrossServerMessage
        {

            [ProtoMember(1)] public readonly int ToServerId;
            [ProtoMember(2)] public readonly int FromServerId;
            [ProtoMember(3)] public readonly ushort UniqueMessageId;
            [ProtoMember(4)] public readonly byte[] Message;

            public CrossServerMessage(ushort UniqueMessageId, int ToServerId, int FromServerId, byte[] Message)
            {
                this.UniqueMessageId = UniqueMessageId;
                this.ToServerId = ToServerId;
                this.FromServerId = FromServerId;
                this.Message = Message;
            }

            public CrossServerMessage() { }
        }
    }
}
