using NLog;
using ProtoBuf;
using QuantumHangar.HangarChecks;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantumHangar.HangarMarket
{
    public class ClientCommunication
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        public const ushort NETWORK_ID = 2934;

        public ClientCommunication()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(NETWORK_ID, ClientMessageRecieved);
        }

        private void ClientMessageRecieved(ushort arg1, byte[] arg2, ulong arg3, bool arg4)
        {

            try
            {
                Message RecievedMessage = MyAPIGateway.Utilities.SerializeFromBinary<Message>(arg2);
                switch (RecievedMessage.Type)
                {
                    case Message.MessageType.MarketOffersUpdate:
                        ReplyAllOffers(arg3);
                        break;


                    case Message.MessageType.GridDefintionPreview:
                        SetGridPreview(RecievedMessage.Definition);
                        break;


                    case Message.MessageType.BuySelectedGrid:
                        PruchaseGrid(RecievedMessage.BuyRequest, arg3);
                        break;


                    default:
                        Log.Warn($"Unkown message type! Is this mod up to date?");
                        return;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occured on server message deserialization! {ex.ToString()}");
                return;
            }
        }


        private void ReplyAllOffers(ulong SteamID)
        {
            try
            {
                Message Message = new Message(Message.MessageType.MarketOffersUpdate);
                Message.MarketOffers = HangarMarketController.MarketOffers.Values.ToList();
                Message.MarketOffers.AddRange(Hangar.Config.PublicMarketOffers);


                MyAPIGateway.Multiplayer.SendMessageTo(NETWORK_ID, MyAPIGateway.Utilities.SerializeToBinary(Message), SteamID);
                Log.Warn("Sending all market offers back to client!");
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occured on server message serialization! {ex}");
            }
        }

        public void UpdateAllOffers()
        {
            try
            {
                Message Message = new Message(Message.MessageType.MarketOffersUpdate);
                Message.MarketOffers = HangarMarketController.MarketOffers.Values.ToList();
                Message.MarketOffers.AddRange(Hangar.Config.PublicMarketOffers);


                if (Message.MarketOffers == null)
                {
                    Message.MarketOffers = new List<MarketListing>();
                    //Log.Fatal("Marketoffers is null");
                }

                MyAPIGateway.Multiplayer.SendMessageToOthers(NETWORK_ID, MyAPIGateway.Utilities.SerializeToBinary(Message));
                Log.Warn("Sending all market offers back to client!");
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occured on server message serialization! {ex}");
            }
        }


        private void SetGridPreview(GridDefinition Definition)
        {
            //Need to have a cooldown on this shit
            Log.Warn($"Client requested to set grid preview of {Definition.GridName}!");


            //Get grid.
            HangarMarketController.SetGridPreview(Definition.ProjectorEntityID, Definition.OwnerSteamID, Definition.GridName);
        }

        private void PruchaseGrid(BuyGridRequest Offer, ulong BuyerSteamID)
        {
            HangarMarketController.PurchaseGridOffer(BuyerSteamID, Offer.OwnerSteamID, Offer.GridName);
        }


        public void close()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(NETWORK_ID, ClientMessageRecieved);
        }

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
        public enum MessageType
        {
            MarketOffersUpdate,
            GridDefintionPreview,
            BuySelectedGrid
        }


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



}
