using ProtoBuf;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace QuantumHangar.HangarMarket
{


    //This is the file we keep for a public market listing

    [ProtoContract]
    public class MarketListing
    {

        [ProtoMember(1)] public string Name { get; set; } = "Grid_Name";
        [ProtoMember(2)] public string Description { get; set; } = "";
        [ProtoMember(3)] public string Seller { get; set; } = "Sold by Server";
        [ProtoMember(4)] public long Price { get; set; } = 1000;

        [ProtoMember(5)] public double MarketValue = 0;
        [ProtoMember(6)] public ulong SteamId = 0;
        [ProtoMember(7)] public long IdentityId;
        [ProtoMember(8)] public string SellerFaction { get; set; } = "N/A";




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
        [ProtoMember(19)] public int Pcu = 0;



        //Server blocklimits Block
        [XmlIgnoreAttribute]
        [ProtoMember(20)] 
        public Dictionary<string, int> BlockTypeCount = new Dictionary<string, int>();


        //Grid Stored Materials
        [XmlIgnoreAttribute]
        [ProtoMember(30)] 
        public Dictionary<string, double> StoredMaterials = new Dictionary<string, double>();


        [ProtoMember(31)] public string GridFileName; //Point to the objectbuilder in the player's file
        [ProtoMember(32)] public byte[] GridDefinition;




        //Non Serialized data
        [ProtoIgnore] public bool ServerOffer = false;

        [ProtoIgnore] public List<KeyValuePair<ulong, int>> PlayerPurchases = new List<KeyValuePair<ulong, int>>();
        [ProtoIgnore] public string FileSbcPath { get; set; } = "";
        [ProtoIgnore] public int TotalAmount { get; set; } = 0;
        [ProtoIgnore] public int TotalPerPlayer { get; set; } = 0;
        [ProtoIgnore] public int TotalBuys { get; set; } = 0;
        [ProtoIgnore] public bool ForSale { get; set; } = true;



        public MarketListing() { }
        public MarketListing(GridStamp stamp)
        {
            Name = stamp.GridName;
            
            

            GridMass = stamp.GridMass;
            SmallGrids = stamp.SmallGrids;
            LargeGrids = stamp.LargeGrids;
            StaticGrids = stamp.StaticGrids;
            NumberofBlocks = stamp.NumberOfBlocks;
            MaxPowerOutput = stamp.MaxPowerOutput;
            GridBuiltPercent = stamp.GridBuiltPercent;
            JumpDistance = stamp.JumpDistance;
            NumberOfGrids = stamp.NumberOfGrids;
            Pcu = stamp.GridPcu;
            MarketValue = stamp.MarketValue;
        }

        public void SetUserInputs(string description, long price)
        {
            this.Description = description;
            this.Price = price;
        }



        public void SetPlayerData(ulong steamId, long identityId)
        {
            this.SteamId = steamId;
            this.IdentityId = identityId;
        }


    }
}
