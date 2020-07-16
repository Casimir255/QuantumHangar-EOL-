using NLog;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;
using System.Xml;
using SimpleTCP;
using System.IO;
using Newtonsoft.Json;
using VRage.Game.ModAPI;
using QuantumHangar.Utilities;

namespace QuantumHangar
{
    public class Comms
    {

        private static bool HandlersInitilized = false;
        private static ushort NetworkID = 2934;
        //private static Main _main;

        public GridMarket _Market;


        public Comms(GridMarket Market)
        {
            _Market = Market;
        }

        public void RegisterHandlers()
        {
            if (!HandlersInitilized)
            {
                QuantumHangar.Hangar.Debug("Registering Event Handlers");
                MyAPIGateway.Multiplayer.RegisterMessageHandler(NetworkID, MessageHandler);

                HandlersInitilized = true;
            }
        }

        public void UnregisterHandlers()
        {
            try
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(NetworkID, MessageHandler);
            }
            catch (Exception a)
            {
                QuantumHangar.Hangar.Debug("Cannot remove event Handlers! Are they already removed?", a, QuantumHangar.Hangar.ErrorType.Fatal);
            }
        }
        private void MessageHandler(byte[] bytes)
        {

            try
            {
                //var type = (MessageType)bytes[0];
                //MyLog.Default.WriteLineAndConsole(bytes.ToString());


                Message RecievedMessage = MyAPIGateway.Utilities.SerializeFromBinary<Message>(bytes);



                if (RecievedMessage.Type == MessageType.RequestAllItems)
                {
                    SendListToModOnInitilize();
                }
                else if (RecievedMessage.Type == MessageType.SendDefinition)
                {
                    // Main.SendDefinition(RecievedMessage.GridDefinitions.name);
                }
                else if (RecievedMessage.Type == MessageType.PurchasedGrid)
                {
                    CrossServerMessage message = new CrossServerMessage();
                    message.GridDefinition.Add(RecievedMessage.GridDefinitions);
                    message.Type = CrossServer.MessageType.PurchasedGrid;

                    _Market.MarketServers.Update(message);
                    //Main.PurchaseGrid(RecievedMessage.GridDefinitions, Main);
                }
                else
                {
                    //Do nothing/Send nothing

                }



                //Array.Copy(bytes, 1, data, 0, data.Length);
            }
            catch (Exception ex)
            {
                QuantumHangar.Hangar.Debug($"Unable to decrypt data from client!", ex, QuantumHangar.Hangar.ErrorType.Warn);

            }
        }

        public static void SendMessageToMod(Message message)
        {

            byte[] barr = MyAPIGateway.Utilities.SerializeToBinary(message);
            MyAPIGateway.Multiplayer.SendMessageToOthers(NetworkID, barr);
        }


        public static void SendListToModOnInitilize()
        {
            //We send the server offers and the global player offers together.
            //This lets us keep admin offers per server. (Kinda cool)
            Message Newmessage = new Message();
            List<MarketList> AllItems = new List<MarketList>();
            AllItems.AddRange(GridMarket.GridList);
            AllItems.AddRange(GridMarket.PublicOfferseGridList);

            Newmessage.MarketBoxItmes = AllItems;
            Newmessage.GridDefinitions = null;
            Newmessage.Type = MessageType.RequestAllItems;

            SendMessageToMod(Newmessage);

        }


        public static void AddSingleItem(MarketList item)
        {

            Hangar.Debug("Sending List to Client!");
            Message Newmessage = new Message();
            Newmessage.MarketBoxItmes.Add(item);
            Newmessage.GridDefinitions = null;
            Newmessage.Type = MessageType.AddOne;

            SendMessageToMod(Newmessage);


        }

        public static void RemoveSingleItem(MarketList item)
        {

            //Debug("Sending List to Client!");
            Message Newmessage = new Message();
            Newmessage.MarketBoxItmes.Add(item);
            Newmessage.GridDefinitions = null;
            Newmessage.Type = MessageType.RemoveOne;

            SendMessageToMod(Newmessage);
        }

    }

    public class CrossServer
    {
        private static readonly Logger Log = LogManager.GetLogger("QuantumHangar");
        public static SimpleTcpServer Server;
        public static SimpleTcpClient Client;
        public static bool RecievedInitialRequest = false;
        private int _Port;
        
        private GridMarket _Market;

        public CrossServer(int MarketPort, GridMarket Market)
        {
            _Port = MarketPort;
            _Market = Market;
        }
        public enum MessageType
        {
            //For the grids
            RequestAll,
            AddItem,
            RemoveItem,

            //Check to see if the seller is online on any server
            PlayerOnline,
            PlayerAccountUpdated, //Return to server that the seller has been found and can be removed from the dictionary. (if true)
            PurchasedGrid
            //If false, broadcast to all clients to update the playerJoinerWatcher

            //When player joins return PlayerAccount updated
        }

        public bool CreateServers()
        {
            //Need to attempt server 
            try
            {

                Server = new SimpleTcpServer().Start(_Port);
                Hangar.Debug("Initilized Server on port: " + _Port);

                _Market.IsHostServer = true;
                Server.DataReceived += Server_DataReceived;

                //Server.DelimiterDataReceived += (sender, msg) => {
                //Console.WriteLine("From client: "+msg.MessageString);
                //CrossServerMessage RecievedData = new CrossServerMessage();
                //RecievedData.GridDefinition = Main.GridDefinition;
                //RecievedData.List = Main.GridList;
                //RecievedData.BalanceUpdate = Main.PlayerAccountUpdate;
                //msg.Reply(JsonConvert.SerializeObject(RecievedData));
                //};
            }
            catch (System.InvalidOperationException)
            {
                //Server already created, create client
                Client = new SimpleTcpClient().Connect("127.0.0.1", _Port);
                Hangar.Debug("Initilized Client on port: " + _Port);

                _Market.IsHostServer = false;
                Client.DelimiterDataReceived += Client_DelimiterDataReceived;
            }
            catch (Exception a)
            {
                Hangar.Debug("Hangar CrossServer Network Error!", a, Hangar.ErrorType.Fatal);
                //Some weird shit
                return false;
            }
            return true;
        }

        private void Server_DataReceived(object sender, SimpleTCP.Message e)
        {
            //if we recieve data to server, this means we need to update clients, and save file to disk
            CrossServerMessage RecievedData = JsonConvert.DeserializeObject<CrossServerMessage>(e.MessageString);
            Hangar.Debug("Client Data Recieved! " + RecievedData.Type.ToString());



            if (RecievedData.Type == MessageType.AddItem)
            {
                Server.BroadcastLine(e.MessageString);

                //Now that we added our items... we need to save file
                //Main.GridDefinition.Add(RecievedData.GridDefinition[0]);
                GridMarket.GridList.Add(RecievedData.List[0]);


                //Send update to clients on this game server!
                Comms.AddSingleItem(RecievedData.List[0]);

                //Save data to file (This is server!)
                MarketData Data = new MarketData();
                Data.List = GridMarket.GridList;
                FileSaver.Save(Path.Combine(Hangar.Dir, "Market.json"), Data);
            }
            else if (RecievedData.Type == MessageType.RemoveItem)
            {
                Server.BroadcastLine(e.MessageString);

                //Just goahead and check to see if the list contains etc.
                if (GridMarket.GridList.Any(x => x.Name == RecievedData.List[0].Name))
                {
                    Hangar.Debug("Removing: " + RecievedData.List[0].Name + " from market!");
                    GridMarket.GridList.RemoveAll(x => x.Name == RecievedData.List[0].Name);
                    //Main.GridDefinition.RemoveAll(x => x.name == RecievedData.List[0].Name);

                    //Send update to clients on this game server!
                    Comms.RemoveSingleItem(RecievedData.List[0]);
                }

                //Save data to file (This is server!)
                MarketData Data = new MarketData();
                Data.List = GridMarket.GridList;
                FileSaver.Save(Path.Combine(Hangar.Dir, "Market.json"), Data);
            }
            else if (RecievedData.Type == MessageType.PlayerAccountUpdated)
            {
                Server.BroadcastLine(e.MessageString);


                //Store values
                foreach (PlayerAccount account in RecievedData.BalanceUpdate)
                {
                    Utils.TryUpdatePlayerBalance(account);

                    if (!GridMarket.PlayerAccounts.ContainsKey(account.SteamID))
                    {
                        if (!account.AccountAdjustment)
                        {
                            GridMarket.PlayerAccounts.Add(account.SteamID, account.AccountBalance);
                        }
                    }
                    else
                    {
                        if (!account.AccountAdjustment)
                        {
                            GridMarket.PlayerAccounts[account.SteamID] = account.AccountBalance;
                        }
                        else
                        {
                            //Add this to the general list
                            GridMarket.PlayerAccounts[account.SteamID] = account.AccountBalance + GridMarket.PlayerAccounts[account.SteamID];
                        }

                    }
                }

                Accounts accounts = new Accounts();
                accounts.PlayerAccounts = GridMarket.PlayerAccounts;

                try
                {
                    FileSaver.Save(Path.Combine(Hangar.Dir, "PlayerAccounts.json"), accounts);
                    //File.WriteAllText(Path.Combine(Main.Dir, "PlayerAccounts.json"), JsonConvert.SerializeObject(accounts));
                 
                }
                catch (Exception a)
                {
                    Hangar.Debug("IO Exception!", a, Hangar.ErrorType.Warn);
                }

               

                /*
                //Need to check to see if the player is online!
                //var first = RecievedData.BalanceUpdate.First();
                //ulong key = first.Key;
                //long val = first.Value;



                List<IMyPlayer> Seller = new List<IMyPlayer>();
                //MyAPIGateway.Players.GetPlayers(Seller, x => x.SteamUserId == Item.Steamid);
                MyAPIGateway.Players.GetPlayers(Seller, x => x.SteamUserId == key);

                if (Seller.Count == 1)
                {
                    //Player is online! Attempt to change player balance!
                    Seller[0].RequestChangeBalance(val);

                    //Send message to server that play is online!
                    CrossServerMessage PlayerOnlineMessage = new CrossServerMessage();
                    PlayerOnlineMessage.Type = CrossServer.MessageType.PlayerOnline;
                    //PlayerOnlineMessage.BalanceUpdate.Add(key, val);

                    Update(PlayerOnlineMessage);
                }
                else
                {
                    //Player is offline. Check to see if this server already has the player! (Remove and add new!)
                    if (Main.PlayerAccountUpdate.ContainsKey(key))
                    {
                        //Remove old Key
                        Main.PlayerAccountUpdate.Remove(key);
                    }

                    Main.PlayerAccountUpdate.Add(key, val);

                    Server.BroadcastLine(e.MessageString);
                }
                */
            }
            else if (RecievedData.Type == MessageType.RequestAll)
            {

                CrossServerMessage AllData = new CrossServerMessage();
                //AllData.GridDefinition = Main.GridDefinition;
                AllData.List = GridMarket.GridList;
                //AllData.BalanceUpdate = Main.PlayerAccounts;
                AllData.Type = MessageType.RequestAll;

                string AllDataString = JsonConvert.SerializeObject(AllData);

                Server.BroadcastLine(AllDataString);

            }





            //File.WriteAllText(Path.Combine(Main.Dir, "Market.json"), FileData);


            //throw new NotImplementedException();

        }

        //Client data recieved
        private void Client_DelimiterDataReceived(object sender, SimpleTCP.Message e)
        {

            //if we recieve data as client we need to update the market list from the one that the server sent
            try
            {
                //e.MessageString.TrimEnd(e.MessageString[e.MessageString.Length-1]);
                CrossServerMessage RecievedData = JsonConvert.DeserializeObject<CrossServerMessage>(e.MessageString);
                Hangar.Debug("Server Data Recieved! " + RecievedData.Type.ToString());

                if (RecievedData.Type == MessageType.AddItem)
                {
                    if (!GridMarket.GridList.Contains(RecievedData.List[0]))
                    {
                        //If its not already in the list, add it.
                        GridMarket.GridList.Add(RecievedData.List[0]);
                        //Main.GridDefinition.Add(RecievedData.GridDefinition[0]);

                        //Send update to clients on this game server!
                        Comms.AddSingleItem(RecievedData.List[0]);
                    }
                }
                else if (RecievedData.Type == MessageType.RemoveItem)
                {

                    if (GridMarket.GridList.Any(x => x.Name == RecievedData.List[0].Name))
                    {
                        Hangar.Debug("Removing: " + RecievedData.List[0].Name + " from market!");
                        GridMarket.GridList.RemoveAll(x => x.Name == RecievedData.List[0].Name);
                        //Main.GridDefinition.RemoveAll(x => x.name == RecievedData.List[0].Name);

                        //Send update to clients on this game server!
                        Comms.RemoveSingleItem(RecievedData.List[0]);
                    }
                }
                else if (RecievedData.Type == MessageType.PlayerAccountUpdated)
                {
                    foreach (PlayerAccount account in RecievedData.BalanceUpdate)
                    {
                        Utils.TryUpdatePlayerBalance(account);

                        if (!GridMarket.PlayerAccounts.ContainsKey(account.SteamID))
                        {
                            if (!account.AccountAdjustment)
                            {
                                GridMarket.PlayerAccounts.Add(account.SteamID, account.AccountBalance);
                            }
                        }
                        else
                        {
                            if (!account.AccountAdjustment)
                            {
                                GridMarket.PlayerAccounts[account.SteamID] = account.AccountBalance;
                            }
                            else
                            {
                                //Add this to the general list
                                GridMarket.PlayerAccounts[account.SteamID] = account.AccountBalance + GridMarket.PlayerAccounts[account.SteamID];
                            }

                        }
                    }


                }
                else if (RecievedData.Type == MessageType.PlayerOnline)
                {
                    //Player was online somewhere!
                    //var first = RecievedData.BalanceUpdate.First();
                    //ulong key = first.Key;

                    //Main.PlayerAccountUpdate.Remove(key);
                }
                else if (RecievedData.Type == MessageType.RequestAll && !RecievedInitialRequest)
                {
                    //Update everything! (New Server started!)
                    //Main.GridDefinition = RecievedData.GridDefinition;
                    GridMarket.GridList = RecievedData.List;
                    //Main.PlayerAccountUpdate = RecievedData.BalanceUpdate;
                    RecievedInitialRequest = true;
                }
                else if (RecievedData.Type == MessageType.PurchasedGrid)
                {
                    var t = Task.Run(() => _Market.PurchaseGrid(RecievedData.GridDefinition[0]));
                }

                MarketData Data = new MarketData();
                //Data.GridDefinition = Main.GridDefinition;
                Data.List = GridMarket.GridList;

                //Save
                //FileSaver.Save(Path.Combine(Main.Dir, "Market.json"), Data);
                //File.WriteAllText(Path.Combine(Main.Dir, "Market.json"), JsonConvert.SerializeObject(Data));

            }
            catch (Exception c)
            {
                Hangar.Debug("Client DeserializeObject Error! ", c, Hangar.ErrorType.Fatal);
            }
            //throw new NotImplementedException();

        }


        //Force update
        public bool Update(CrossServerMessage Message)
        {
            if (_Market.IsHostServer)
            {

                string MarketServerData = JsonConvert.SerializeObject(Message);
                //Write to file and broadcast to all clients!
                Hangar.Debug("Sending new market data to clients! " + Message.Type.ToString());
                if (Message.Type == MessageType.AddItem)
                {
                    //Main.Debug("Point1");
                    Server.BroadcastLine(MarketServerData);
                    //Main.Debug("Point2");
                    //Update server.
                    GridMarket.GridList.Add(Message.List[0]);
                    //Main.GridDefinition.Add(Message.GridDefinition[0]);


                    //Send update to clients on this game server!
                    Comms.AddSingleItem(Message.List[0]);
                    //Main.Debug("Point3");

                    //Save data to file (This is server!)
                    MarketData Data = new MarketData();
                    Data.List = GridMarket.GridList;
                    FileSaver.Save(Path.Combine(Hangar.Dir, "Market.json"), Data);
                }
                else if (Message.Type == MessageType.RemoveItem)
                {
                    Server.BroadcastLine(MarketServerData);

                    //Update server.
                    GridMarket.GridList.Remove(Message.List[0]);
                    //Main.GridDefinition.RemoveAll(Message.GridDefinition[0]);
                    //Main.GridDefinition.RemoveAll(x => x.name == Message.List[0].Name);

                    //Send update to clients on this game server!
                    Comms.RemoveSingleItem(Message.List[0]);


                    //Save data to file (This is server!)
                    MarketData Data = new MarketData();
                    Data.List = GridMarket.GridList;
                    FileSaver.Save(Path.Combine(Hangar.Dir, "Market.json"), Data);
                }
                else if (Message.Type == MessageType.PlayerAccountUpdated)
                {
                    //Do nothing. (Send data to server and wait for reply)

                    Server.BroadcastLine(MarketServerData);


                    foreach (PlayerAccount account in Message.BalanceUpdate)
                    {
                        Utils.TryUpdatePlayerBalance(account);

                        if (!GridMarket.PlayerAccounts.ContainsKey(account.SteamID))
                        {
                            if (!account.AccountAdjustment)
                            {
                                GridMarket.PlayerAccounts.Add(account.SteamID, account.AccountBalance);
                            }
                        }
                        else
                        {
                            if (!account.AccountAdjustment)
                            {
                                GridMarket.PlayerAccounts[account.SteamID] = account.AccountBalance;
                            }
                            else
                            {
                                //Add this to the general list
                                GridMarket.PlayerAccounts[account.SteamID] = account.AccountBalance + GridMarket.PlayerAccounts[account.SteamID];
                            }

                        }
                    }

                    Accounts accounts = new Accounts();
                    accounts.PlayerAccounts = GridMarket.PlayerAccounts;

                    FileSaver.Save(Path.Combine(Hangar.Dir, "PlayerAccounts.json"), accounts);

                }
                else if (Message.Type == MessageType.PlayerOnline)
                {
                    //Server found the player online! (By Joining)
                    //var first = Message.BalanceUpdate.First();
                    //ulong key = first.Key;


                    // Main.PlayerAccountUpdate.Remove(key);

                    //Broadcast to all clients!
                    Server.BroadcastLine(MarketServerData);
                }
                else if (Message.Type == MessageType.PurchasedGrid)
                {
                    var t = Task.Run(() => _Market.PurchaseGrid(Message.GridDefinition[0]));
                }
                //File.WriteAllText(Path.Combine(Main.Dir, "Market.json"), JsonConvert.SerializeObject(Data));

            }
            else
            {
                try
                {
                    string MarketClientData = JsonConvert.SerializeObject(Message);
                    //Send to server to get reply
                    Client.Write(MarketClientData);

                    if (Message.Type == MessageType.AddItem)
                    {
                        //Send update to clients on this game server!
                        Comms.AddSingleItem(Message.List[0]);
                    }
                    else if (Message.Type == MessageType.RemoveItem)
                    {
                        //Send update to clients on this game server!
                        Comms.RemoveSingleItem(Message.List[0]);
                    }
                }
                catch (System.IO.IOException e)
                {
                    //Server no longer responding? Perhaps it shutdown? Or restarted?
                    //This means THIS server needs to be the new server

                    //Or this is an old client needing to be re-connected to server. This means we need to redo/Reconnect everything!
                    Hangar.Debug("Server closed! Trying to Update!", e, Hangar.ErrorType.Warn);
                    //Remove client DataRecieved Event!
                    Client.DelimiterDataReceived -= Client_DelimiterDataReceived;

                    //Re run create servers and client class!
                    CreateServers();

                    //Re run the update
                    Update(Message);


                }
                catch (Exception g)
                {
                    Hangar.Debug("CrossServer Market Network Failed Fatally!", g, Hangar.ErrorType.Fatal);

                }
            }


            return true;
        }

        public void Dispose()
        {
            //Dispose of events. This is called at server close


            if (_Market.IsHostServer)
            {
                Server.DataReceived -= Server_DataReceived;
            }
            else
            {
                Client.DelimiterDataReceived -= Client_DelimiterDataReceived;
            }

        }
    }

}
