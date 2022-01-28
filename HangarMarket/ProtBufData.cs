using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace HangarStoreMod
{
    public enum MessageType
    {
        MarketOffersUpdate,
        GridDefintionPreview,
        BuySelectedGrid
    }


    [ProtoContract]
    public class GridDefinition
    {
        //Items we send to server. (We will have server set the projection)
        [ProtoMember(1)] public string GridName;
        [ProtoMember(3)] public ulong OwnerSteamID;
        [ProtoMember(4)] public long ProjectorEntityID;


        public GridDefinition() { }

    }

    [ProtoContract]
    public class BuyGridRequest
    {
        [ProtoMember(1)] public string GridName;
        [ProtoMember(3)] public ulong OwnerSteamID;
        [ProtoMember(4)] public ulong BuyerSteamID;

        public BuyGridRequest() { }
    }




    [ProtoContract]
    public class Message
    {
        [ProtoMember(10)] public MessageType Type;



        [ProtoMember(20)] public List<MarketListing> MarketOffers;
        [ProtoMember(30)] public GridDefinition Definition;
        [ProtoMember(40)] public BuyGridRequest BuyRequest;


        public Message() { }

        public Message(MessageType Type)
        {
            this.Type = Type;
        }

    }


    [ProtoContract]
    public class MarketListing
    {
        [ProtoMember(1)] public string Name;
        [ProtoMember(2)] public string Description;
        [ProtoMember(3)] public string Seller = "Sold by Server";
        [ProtoMember(4)] public long Price;
        [ProtoMember(5)] public double MarketValue;
        [ProtoMember(6)] public ulong SteamID;
        [ProtoMember(7)] public long IdentityID;
        [ProtoMember(8)] public string SellerFaction = "N/A";




        //Grid specfic details
        [ProtoMember(10)] public float GridMass = 0;
        [ProtoMember(11)] public int SmallGrids = 0;
        [ProtoMember(12)] public int LargeGrids = 0;
        [ProtoMember(13)] public int StaticGrids = 0;
        [ProtoMember(14)] public int NumberofBlocks = 0;
        [ProtoMember(15)] public float MaxPowerOutput = 0;
        [ProtoMember(16)] public float GridBuiltPercent = 0;
        [ProtoMember(17)] public long JumpDistance = 0;
        [ProtoMember(18)] public int NumberOfGrids = 0;
        [ProtoMember(19)] public int PCU = 0;



        //Server blocklimits Block
        [ProtoMember(20)] public Dictionary<string, int> BlockTypeCount = new Dictionary<string, int>();


        //Grid Stored Materials
        [ProtoMember(30)] public Dictionary<string, double> StoredMaterials = new Dictionary<string, double>();
        [ProtoMember(31)] public string GridFileName; //Point to the objectbuilder in the player's file
        [ProtoMember(32)] public byte[] GridDefinition;



        [ProtoMember(33)] public bool ServerOffer = false;


    }
}
