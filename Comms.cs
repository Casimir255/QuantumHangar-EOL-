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

namespace QuantumHangar
{
    public class Comms
    {

        public const ushort NETWORK_ID = 2934;
        private static bool HandlersInitilized = false;
        //private static Main _main;

        public Main Main;

        public void RegisterHandlers()
        {

            if (!HandlersInitilized)
            {
                QuantumHangar.Main.Debug("Registering Event Handlers");
                MyAPIGateway.Multiplayer.RegisterMessageHandler(2934, MessageHandler);
                
                HandlersInitilized = true;
            }
        }

        public void UnregisterHandlers()
        {
            try
            {
                MyAPIGateway.Multiplayer.UnregisterMessageHandler(2935, MessageHandler);
            }
            catch (Exception a)
            {
                QuantumHangar.Main.Debug("Cannot remove event Handlers! Are they already removed?", a, QuantumHangar.Main.ErrorType.Fatal);
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

                    Main.MarketServers.Update(message);
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
                QuantumHangar.Main.Debug($"Unable to decrypt data from client!", ex, QuantumHangar.Main.ErrorType.Warn);

            }
        }

        public static void SendMessageToMod(Message message)
        {

                byte[] barr = MyAPIGateway.Utilities.SerializeToBinary(message);


                MyAPIGateway.Multiplayer.SendMessageToOthers(NETWORK_ID, barr);
           

            //QuantumHangar.Main.Debug(barr.Length.ToString());
        }


        public static void SendListToModOnInitilize()
        {
                //Debug("Sending List to Client!");
                Message Newmessage = new Message();
            List<MarketList> AllItems = new List<MarketList>();
            AllItems.AddRange(Main.GridList);
            AllItems.AddRange(Main.PublicOfferseGridList);

                Newmessage.MarketBoxItmes = AllItems;
                Newmessage.GridDefinitions = null;
                Newmessage.Type = MessageType.RequestAllItems;

                SendMessageToMod(Newmessage);

        }


        public static void AddSingleItem(MarketList item)
        {

                //Debug("Sending List to Client!");
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
        public static short Port = 8910;
        public Main Main;

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
                Server = new SimpleTcpServer().Start(Port);
                Main.Debug("Initilized Server on port: " + Port);

                Main.IsHostServer = true;
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
                Client = new SimpleTcpClient().Connect("127.0.0.1", Port);
                Main.Debug("Initilized Client on port: " + Port);

                Main.IsHostServer = false;
                Client.DelimiterDataReceived += Client_DelimiterDataReceived;
            }
            catch (Exception a)
            {
                Main.Debug("Hangar CrossServer Network Error!", a, Main.ErrorType.Fatal);
                //Some weird shit
                return false;
            }
            return true;
        }

        private void Server_DataReceived(object sender, SimpleTCP.Message e)
        {
            //if we recieve data to server, this means we need to update clients, and save file to disk
            CrossServerMessage RecievedData = JsonConvert.DeserializeObject<CrossServerMessage>(e.MessageString);
            Main.Debug("Client Data Recieved! " + RecievedData.Type.ToString());

           

            if (RecievedData.Type == MessageType.AddItem)
            {
                Server.BroadcastLine(e.MessageString);

                //Now that we added our items... we need to save file
                //Main.GridDefinition.Add(RecievedData.GridDefinition[0]);
                Main.GridList.Add(RecievedData.List[0]);


                //Send update to clients on this game server!
                Comms.AddSingleItem(RecievedData.List[0]);
            }
            else if (RecievedData.Type == MessageType.RemoveItem)
            {
                Server.BroadcastLine(e.MessageString);

                //Just goahead and check to see if the list contains etc.
                if (Main.GridList.Any(x => x.Name == RecievedData.List[0].Name))
                {
                    Main.Debug("Removing: " + RecievedData.List[0].Name + " from market!");
                    Main.GridList.RemoveAll(x => x.Name == RecievedData.List[0].Name);
                    //Main.GridDefinition.RemoveAll(x => x.name == RecievedData.List[0].Name);

                    //Send update to clients on this game server!
                    Comms.RemoveSingleItem(RecievedData.List[0]);
                }
            }
            else if (RecievedData.Type == MessageType.PlayerAccountUpdated)
            {
                Server.BroadcastLine(e.MessageString);


                //Store values
                foreach (PlayerAccount account in RecievedData.BalanceUpdate)
                {
                    EconUtils.TryUpdatePlayerBalance(account);

                    if (!Main.PlayerAccounts.ContainsKey(account.SteamID))
                    {
                        if (!account.AccountAdjustment)
                        {
                            Main.PlayerAccounts.Add(account.SteamID, account.AccountBalance);
                        }
                    }
                    else
                    {
                        if (!account.AccountAdjustment)
                        {
                            Main.PlayerAccounts[account.SteamID] = account.AccountBalance;
                        }
                        else
                        {
                            //Add this to the general list
                            Main.PlayerAccounts[account.SteamID] = account.AccountBalance + Main.PlayerAccounts[account.SteamID];
                        }

                    }
                }

                Accounts accounts = new Accounts();
                accounts.PlayerAccounts = Main.PlayerAccounts;



                using (StreamWriter file = File.CreateText(Path.Combine(Main.Dir, "PlayerAccounts.json")))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, accounts);
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
                AllData.List = Main.GridList;
                //AllData.BalanceUpdate = Main.PlayerAccountUpdate;
                AllData.Type = MessageType.RequestAll;

                string AllDataString = JsonConvert.SerializeObject(AllData);

                Server.BroadcastLine(AllDataString);

            }





            //Save data to file (This is server!)
            MarketData Data = new MarketData();
           // Data.GridDefinition = Main.GridDefinition;
            Data.List = Main.GridList;
            //Data.BalanceUpdate = Main.PlayerAccountUpdate;

            string FileData = JsonConvert.SerializeObject(Data);
            File.WriteAllText(Path.Combine(Main.Dir, "Market.json"), FileData);


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
                Main.Debug("Server Data Recieved! " + RecievedData.Type.ToString());

                if (RecievedData.Type == MessageType.AddItem)
                {
                    if (!Main.GridList.Contains(RecievedData.List[0]))
                    {
                        //If its not already in the list, add it.
                        Main.GridList.Add(RecievedData.List[0]);
                        //Main.GridDefinition.Add(RecievedData.GridDefinition[0]);

                        //Send update to clients on this game server!
                        Comms.AddSingleItem(RecievedData.List[0]);
                    }
                }
                else if (RecievedData.Type == MessageType.RemoveItem)
                {
                    
                    if (Main.GridList.Any(x => x.Name == RecievedData.List[0].Name))
                    {
                        Main.Debug("Removing: " + RecievedData.List[0].Name + " from market!");
                        Main.GridList.RemoveAll( x => x.Name == RecievedData.List[0].Name);
                        //Main.GridDefinition.RemoveAll(x => x.name == RecievedData.List[0].Name);

                        //Send update to clients on this game server!
                        Comms.RemoveSingleItem(RecievedData.List[0]);
                    }
                }
                else if (RecievedData.Type == MessageType.PlayerAccountUpdated)
                {
                    foreach (PlayerAccount account in RecievedData.BalanceUpdate)
                    {
                        EconUtils.TryUpdatePlayerBalance(account);

                        if (!Main.PlayerAccounts.ContainsKey(account.SteamID))
                        {
                            if (!account.AccountAdjustment)
                            {
                                Main.PlayerAccounts.Add(account.SteamID, account.AccountBalance);
                            }
                        }
                        else
                        {
                            if (!account.AccountAdjustment)
                            {
                                Main.PlayerAccounts[account.SteamID] = account.AccountBalance;
                            }
                            else
                            {
                                //Add this to the general list
                                Main.PlayerAccounts[account.SteamID] = account.AccountBalance + Main.PlayerAccounts[account.SteamID];
                            }

                        }
                    }
                    
                    
                    Accounts accounts = new Accounts();
                    accounts.PlayerAccounts = Main.PlayerAccounts;

                    try
                    {
                        using (StreamWriter file = File.CreateText(Path.Combine(Main.Dir, "PlayerAccounts.json")))
                        {
                            JsonSerializer serializer = new JsonSerializer();
                            serializer.Serialize(file, accounts);
                        }
                    }catch(Exception a)
                    {
                        Main.Debug("IO Exception!", a, Main.ErrorType.Warn);
                    }
                    


                }
                else if(RecievedData.Type == MessageType.PlayerOnline)
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
                    Main.GridList = RecievedData.List;
                    //Main.PlayerAccountUpdate = RecievedData.BalanceUpdate;
                    RecievedInitialRequest = true;
                }
                else if (RecievedData.Type == MessageType.PurchasedGrid)
                {
                    var t = Task.Run(() => Main.PurchaseGrid(RecievedData.GridDefinition[0]));
                }

                MarketData Data = new MarketData();
                //Data.GridDefinition = Main.GridDefinition;
                Data.List = Main.GridList;


                using (StreamWriter file = File.CreateText(Path.Combine(Main.Dir, "Market.json")))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, Data);
                }




            }
            catch (Exception c)
            {
                Main.Debug("Client DeserializeObject Error! ", c, Main.ErrorType.Fatal);
            }
            //throw new NotImplementedException();

        }


        //Force update
        public bool Update(CrossServerMessage Message)
        {
            if (Main.IsHostServer)
            {

                string MarketServerData = JsonConvert.SerializeObject(Message);
                //Write to file and broadcast to all clients!
                Main.Debug("Sending new market data to clients! " + Message.Type.ToString()) ;
                if (Message.Type == MessageType.AddItem)
                {
                    //Main.Debug("Point1");
                    Server.BroadcastLine(MarketServerData);
                    //Main.Debug("Point2");
                    //Update server.
                    Main.GridList.Add(Message.List[0]);
                    //Main.GridDefinition.Add(Message.GridDefinition[0]);


                    //Send update to clients on this game server!
                    Comms.AddSingleItem(Message.List[0]);
                    //Main.Debug("Point3");
                }
                else if (Message.Type == MessageType.RemoveItem)
                {
                    Server.BroadcastLine(MarketServerData);

                    //Update server.
                    Main.GridList.Remove(Message.List[0]);
                    //Main.GridDefinition.RemoveAll(Message.GridDefinition[0]);
                    //Main.GridDefinition.RemoveAll(x => x.name == Message.List[0].Name);

                    //Send update to clients on this game server!
                    Comms.RemoveSingleItem(Message.List[0]);
                }
                else if (Message.Type == MessageType.PlayerAccountUpdated)
                {
                    //Do nothing. (Send data to server and wait for reply)

                    Server.BroadcastLine(MarketServerData);


                    foreach (PlayerAccount account in Message.BalanceUpdate)
                    {
                        EconUtils.TryUpdatePlayerBalance(account);

                        if (!Main.PlayerAccounts.ContainsKey(account.SteamID))
                        {
                            if (!account.AccountAdjustment) {
                                Main.PlayerAccounts.Add(account.SteamID, account.AccountBalance);
                            }
                        }
                        else
                        {
                            if (!account.AccountAdjustment)
                            {
                                Main.PlayerAccounts[account.SteamID] = account.AccountBalance;
                            }
                            else
                            {
                                //Add this to the general list
                                Main.PlayerAccounts[account.SteamID] = account.AccountBalance + Main.PlayerAccounts[account.SteamID];
                            }
                            
                        }

                        
                    }

                    Accounts accounts = new Accounts();
                    accounts.PlayerAccounts = Main.PlayerAccounts;



                    using (StreamWriter file = File.CreateText(Path.Combine(Main.Dir, "PlayerAccounts.json")))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        serializer.Serialize(file, accounts);
                    }

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

                    var t = Task.Run(() => Main.PurchaseGrid(Message.GridDefinition[0]));
                }

                //Save data to file (This is server!)
                MarketData Data = new MarketData();
                //Data.GridDefinition = Main.GridDefinition;
                Data.List = Main.GridList;



                using (StreamWriter file = File.CreateText(Path.Combine(Main.Dir, "Market.json")))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.Serialize(file, Data);
                }
                //We also need to save file at the end

            }
            else
            {
                try
                {
                    string MarketClientData = JsonConvert.SerializeObject(Message);
                    //Send to server to get reply
                    Client.Write(MarketClientData);

                    if(Message.Type == MessageType.AddItem)
                    {
                        //Send update to clients on this game server!
                        Comms.AddSingleItem(Message.List[0]);
                    }else if(Message.Type == MessageType.RemoveItem)
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
                    Main.Debug("Server closed! Trying to Update!", e, Main.ErrorType.Warn);
                    //Remove client DataRecieved Event!
                    Client.DelimiterDataReceived -= Client_DelimiterDataReceived;

                    //Re run create servers and client class!
                    CreateServers();

                    //Re run the update
                    Update(Message);


                }
                catch (Exception g)
                {
                    Main.Debug("CrossServer Market Network Failed Fatally!", g, Main.ErrorType.Fatal);
                    
                }
            }
            

            return true;
        }

        public void Dispose()
        {
            //Dispose of events. This is called at server close


            if (Main.IsHostServer)
            {
                Server.DataReceived += Server_DataReceived;
            }
            else
            {
                Client.DelimiterDataReceived -= Client_DelimiterDataReceived;
            }

        }
    }

}
