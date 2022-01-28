using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using System.Linq;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.Components;

namespace HangarStoreMod
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class MarketSessionComponent : MySessionComponentBase
    {
        private bool Initilized = false;
        public const ushort NETWORK_ID = 2934;

        public static List<MarketListing> RecievedMarket = new List<MarketListing>();


        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Utilities.IsDedicated) //only client pls
                return;


            if (MyAPIGateway.Session == null)
                return;




            if (!Initilized)
            {
                Initilized = true;
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NETWORK_ID, MessageRecieved);

                //Tell server to send new offers
                RequestOffers();
            }

        }

        protected override void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NETWORK_ID, MessageRecieved);
        }



        private void MessageRecieved(ushort arg1, byte[] arg2, ulong arg3, bool arg4)
        {
            Utils.Log("Server Message Recieved!");


            try
            {
                Message RecievedMessage = MyAPIGateway.Utilities.SerializeFromBinary<Message>(arg2);

                if (RecievedMessage == null)
                    return;


                switch (RecievedMessage.Type)
                {
                    case MessageType.MarketOffersUpdate:
                        ModCore.MergeNewCollection(RecievedMessage.MarketOffers);
                        break;


                    default:
                        Utils.Log($"Unkown message type! Is this mod up to date?");
                        return;
                }
            }
            catch (Exception ex)
            {
                Utils.Log($"Exception occured on server message deserialization! {ex.ToString()}");
                return;
            }





        }

        public static void SendGridPreviewRequest(GridDefinition Def)
        {
            try
            {
                Message Message = new Message(MessageType.GridDefintionPreview);
                Message.Definition = Def;

                MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, MyAPIGateway.Utilities.SerializeToBinary(Message));
            }
            catch (Exception ex)
            {
                Utils.Log($"Exception occured on server message deserialization! {ex.ToString()}");
            }
        }


        public static void RequestOffers()
        {
            try
            {
                Message Message = new Message(MessageType.MarketOffersUpdate);


                MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, MyAPIGateway.Utilities.SerializeToBinary(Message));
            }
            catch (Exception ex)
            {
                Utils.Log($"Exception occured on server message deserialization! {ex.ToString()}");
            }
        }

        public static void PurchaseGrid(BuyGridRequest Request)
        {
            try
            {
                Message Message = new Message(MessageType.BuySelectedGrid);
                Message.BuyRequest = Request;

                MyAPIGateway.Multiplayer.SendMessageToServer(NETWORK_ID, MyAPIGateway.Utilities.SerializeToBinary(Message));

            }catch(Exception ex)
            {
                Utils.Log($"Exception occured on server message deserialization! {ex.ToString()}");
            }

        }






        private MarketListing FillTest(string Name, int PCU)
        {
            MarketListing B = new MarketListing();
            B.Name = Name;
            B.PCU = PCU;

            return B;
        }





    }

}






