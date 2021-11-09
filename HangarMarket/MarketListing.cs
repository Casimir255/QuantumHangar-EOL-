using ProtoBuf;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;

namespace QuantumHangar.HangarMarket
{


    //This is the file we keep for a public market listing

    [ProtoContract]
    public class MarketListing
    {

        [ProtoMember(1)] public string Name = "Grid";
        [ProtoMember(2)] public string Description = "Description";
        [ProtoMember(3)] public string Seller = "Sold by Server";
        [ProtoMember(4)] public long Price = 0;
        [ProtoMember(5)] public double MarketValue = 0;
        [ProtoMember(6)] public ulong SteamID = 0;
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


        public MarketListing() { }
        public MarketListing(GridStamp Stamp)
        {
            Name = Stamp.GridName;
            
            

            GridMass = Stamp.GridMass;
            SmallGrids = Stamp.SmallGrids;
            LargeGrids = Stamp.LargeGrids;
            StaticGrids = Stamp.StaticGrids;
            NumberofBlocks = Stamp.NumberofBlocks;
            MaxPowerOutput = Stamp.MaxPowerOutput;
            GridBuiltPercent = Stamp.GridBuiltPercent;
            JumpDistance = Stamp.JumpDistance;
            NumberOfGrids = Stamp.NumberOfGrids;
            PCU = Stamp.GridPCU;
            MarketValue = Stamp.MarketValue;
        }

        public void SetUserInputs(string Description, long Price)
        {
            this.Description = Description;
            this.Price = Price;
        }



        public void SetPlayerData(ulong SteamID, long IdentityID)
        {
            this.SteamID = SteamID;
            this.IdentityID = IdentityID;
        }


    }
}
