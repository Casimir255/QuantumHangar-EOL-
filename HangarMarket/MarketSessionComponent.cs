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
        private bool _initilized = false;
        public const ushort NetworkId = 2934;

        public static List<MarketListing> RecievedMarket = new List<MarketListing>();


        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Utilities.IsDedicated) //only client pls
                return;


            if (MyAPIGateway.Session == null)
                return;




            if (!_initilized)
            {
                _initilized = true;
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NetworkId, MessageRecieved);

                //Tell server to send new offers
                RequestOffers();
            }

        }

        protected override void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NetworkId, MessageRecieved);
        }



        private void MessageRecieved(ushort arg1, byte[] arg2, ulong arg3, bool arg4)
        {
            Utils.Log("Server Message Recieved!");


            try
            {
                Message recievedMessage = MyAPIGateway.Utilities.SerializeFromBinary<Message>(arg2);

                if (recievedMessage == null)
                    return;


                switch (recievedMessage.Type)
                {
                    case MessageType.MarketOffersUpdate:
                        ModCore.MergeNewCollection(recievedMessage.MarketOffers);
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

        public static void SendGridPreviewRequest(GridDefinition def)
        {
            try
            {
                Message message = new Message(MessageType.GridDefintionPreview);
                message.Definition = def;

                MyAPIGateway.Multiplayer.SendMessageToServer(NetworkId, MyAPIGateway.Utilities.SerializeToBinary(message));
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
                Message message = new Message(MessageType.MarketOffersUpdate);


                MyAPIGateway.Multiplayer.SendMessageToServer(NetworkId, MyAPIGateway.Utilities.SerializeToBinary(message));
            }
            catch (Exception ex)
            {
                Utils.Log($"Exception occured on server message deserialization! {ex.ToString()}");
            }
        }

        public static void PurchaseGrid(BuyGridRequest request)
        {
            try
            {
                Message message = new Message(MessageType.BuySelectedGrid);
                message.BuyRequest = request;

                MyAPIGateway.Multiplayer.SendMessageToServer(NetworkId, MyAPIGateway.Utilities.SerializeToBinary(message));

            }catch(Exception ex)
            {
                Utils.Log($"Exception occured on server message deserialization! {ex.ToString()}");
            }

        }






        private MarketListing FillTest(string name, int pcu)
        {
            MarketListing b = new MarketListing();
            b.Name = name;
            b.Pcu = pcu;

            return b;
        }





    }

}






