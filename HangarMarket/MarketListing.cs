using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantumHangar.HangarMarket
{


    //This is the file we keep for a public market listing
    public class MarketListing
    {

        [ProtoMember(1)] public string Name;
        [ProtoMember(2)] public string Description;
        [ProtoMember(3)] public string Seller = "Sold by Server";
        [ProtoMember(4)] public long Price;
        [ProtoMember(5)] public double MarketValue;
        [ProtoMember(6)] public ulong Steamid;
        [ProtoMember(7)] public string SellerFaction = "N/A";




        //Grid specfic details
        [ProtoMember(8)] public float GridMass = 0;
        [ProtoMember(9)] public int SmallGrids = 0;
        [ProtoMember(10)] public int LargeGrids = 0;
        [ProtoMember(11)] public int StaticGrids = 0;
        [ProtoMember(12)] public int NumberofBlocks = 0;
        [ProtoMember(13)] public float MaxPowerOutput = 0;
        [ProtoMember(14)] public float GridBuiltPercent = 0;
        [ProtoMember(15)] public long JumpDistance = 0;
        [ProtoMember(16)] public int NumberOfGrids = 0;
        [ProtoMember(17)] public int PCU = 0;



        //Server blocklimits Block
        [ProtoMember(18)] public Dictionary<string, int> BlockTypeCount = new Dictionary<string, int>();


        //Grid Stored Materials
        [ProtoMember(19)] public Dictionary<string, double> StoredMaterials = new Dictionary<string, double>();
        [ProtoMember(19)] public string GridFileName; //Point to the objectbuilder in the player's file
        [ProtoMember(25)] public byte[] GridDefinition;



        [ProtoMember(30)] public bool ServerOffer = false;




    }
}
