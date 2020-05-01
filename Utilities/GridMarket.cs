using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.ObjectBuilders;

namespace QuantumHangar.Utilities
{
    class GridMarket
    {
        //Forces refresh of all admin offered grids and syncs them to the blocks in the server
        public static void UpdatePublicOffers(Settings Config)
        {

            //Update all public offer market items!


            //Need to remove existing ones
            for (int i = 0; i < Main.GridList.Count; i++)
            {
                if (Main.GridList[i].Steamid != 0)
                    continue;

                Main.Debug("Removing public offer: " + Main.GridList[i].Name);
                Main.GridList.RemoveAt(i);
            }

            string PublicOfferPath = System.IO.Path.Combine(Main.Dir, "PublicOffers");

            //Clear list
            Main.PublicOfferseGridList.Clear();

            foreach (PublicOffers offer in Config.PublicOffers)
            {
                if (offer.Forsale)
                {
                    string GridFilePath = System.IO.Path.Combine(PublicOfferPath, offer.Name + ".sbc");


                    MyObjectBuilderSerializer.DeserializeXML(GridFilePath, out MyObjectBuilder_Definitions myObjectBuilder_Definitions);
                    MyObjectBuilder_ShipBlueprintDefinition[] shipBlueprint = null;
                    try
                    {
                        shipBlueprint = myObjectBuilder_Definitions.ShipBlueprints;
                    }
                    catch
                    {
                        Main.Debug("Error on BP: " + offer.Name + "! Most likely you put in the SBC5 file! Dont do that!");
                        continue;
                    }
                    MyObjectBuilder_CubeGrid grid = shipBlueprint[0].CubeGrids[0];
                    byte[] Definition = MyAPIGateway.Utilities.SerializeToBinary(grid);

                    //HangarChecks.GetPublicOfferBPDetails(shipBlueprint, out GridStamp stamp);


                    //Straight up add new ones
                    MarketList NewList = new MarketList();
                    NewList.Steamid = 0;
                    NewList.Name = offer.Name;
                    NewList.Seller = offer.Seller;
                    NewList.SellerFaction = offer.SellerFaction;
                    NewList.Price = offer.Price;
                    NewList.Description = offer.Description;
                    NewList.GridDefinition = Definition;
                    //NewList.GridBuiltPercent = stamp.GridBuiltPercent;
                    //NewList.NumberofBlocks = stamp.NumberofBlocks;
                    //NewList.SmallGrids = stamp.SmallGrids;
                    //NewList.LargeGrids = stamp.LargeGrids;
                    // NewList.BlockTypeCount = stamp.BlockTypeCount;

                    //Need to setTotalBuys
                    Main.PublicOfferseGridList.Add(NewList);

                    Main.Debug("Adding new public offer: " + offer.Name);
                }
            }

            //Update Everything!
            Comms.SendListToModOnInitilize();

            MarketData Data = new MarketData();
            Data.List = Main.PublicOfferseGridList;

            /*
            using (StreamWriter file = File.CreateText(System.IO.Path.Combine(Main.Dir, "Market.json")))
            {
                JsonSerializer serializer = new JsonSerializer();
                serializer.Serialize(file, Data);
            }
            */
            //This will force the market to update the market items
        }
    }
}
