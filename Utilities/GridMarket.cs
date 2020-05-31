using Newtonsoft.Json;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch.Managers.ChatManager;
using VRage.Game;
using VRage.ObjectBuilders;
using Torch.Session;
using Torch.API.Session;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.Managers;
using Sandbox.Game.Entities.Character;
using VRage.Game.ModAPI;
using static QuantumHangar.Hangar;
using System.ComponentModel;

namespace QuantumHangar.Utilities
{
    public class GridMarket
    {

        public static List<MarketList> GridList { get; set; }
        public static List<MarketList> PublicOfferseGridList { get; set; }

        public string ServerMarketFileDir;
        public string ServerOffersDir;


        public static Dictionary<ulong, long> PlayerAccounts { get; set; }
        public static Dictionary<ulong, long> BalanceAdjustment { get; set; }
        public CrossServer MarketServers { get; set; }


        private static ChatManagerServer _ChatManager;
        private static MultiplayerManagerBase _MP;
        private string _StoragePath;
        public bool IsHostServer = false;
        

        
        public static Comms Comms;
        public Settings Config => Hangar._config?.Data;




        public GridMarket(string InstancePath)
        {
            _StoragePath = InstancePath;
            GridList = new List<MarketList>();
            PublicOfferseGridList = new List<MarketList>();
            PlayerAccounts = new Dictionary<ulong, long>();
            BalanceAdjustment = new Dictionary<ulong, long>();


            //InitilizeComms();
        }

        public void InitilizeGridMarket()
        {
            //Initilize new market servers
            MarketServers = new CrossServer(Config.MarketPort, this);
            //Attempt to create market servers!
            if (MarketServers.CreateServers() == false)
            {
                Hangar.Debug("Unable to start market servers! Check logs & contact plugin dev!");
                Config.GridMarketEnabled = false;
            }



            Config.HostServer = IsHostServer;


            ServerOffersDir = Path.Combine(_StoragePath, "HangarServerOffers");
            ServerMarketFileDir = Path.Combine(ServerOffersDir, "HangarServerOffers.json");
            Directory.CreateDirectory(ServerOffersDir);
            MarketData PublicData = new MarketData();


            if (File.Exists(ServerMarketFileDir))
            {
                using (StreamReader file = File.OpenText(ServerMarketFileDir))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    PublicData = (MarketData)serializer.Deserialize(file, typeof(MarketData));
                }
            }

            if (IsHostServer)
            {
               
                MarketData Data = new MarketData();
                Accounts Accounts = new Accounts();
                string MarketPath = Path.Combine(Config.FolderDirectory, "Market.json");
                string PlayerAccountsPath = Path.Combine(Config.FolderDirectory, "PlayerAccounts.json");


                //Initilize loading of files!
                if (File.Exists(MarketPath))
                {
                    using (StreamReader file = File.OpenText(MarketPath))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        Data = (MarketData)serializer.Deserialize(file, typeof(MarketData));
                    }
                }


                if (File.Exists(PlayerAccountsPath))
                {
                    using (StreamReader file = File.OpenText(PlayerAccountsPath))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        Accounts = (Accounts)serializer.Deserialize(file, typeof(Accounts));
                    }
                }


                //GridDefinition = Data.GridDefinition;
                PublicOfferseGridList = PublicData.List;
                GridList = Data.List;
                PlayerAccounts = Accounts.PlayerAccounts;


            }
            else
            {
                Hangar.Debug("Requesting update!");

                //Client start. (Tell server to broadcast to all clients!)
                CrossServerMessage SendMessage = new CrossServerMessage();
                SendMessage.Type = CrossServer.MessageType.RequestAll;

                MarketServers.Update(SendMessage);
                Hangar.Debug("Initial Send Request");
            }
        }

        public void InitilizeComms(ChatManagerServer ChatServer, MultiplayerManagerBase Multiplayer)
        {
            Comms = new Comms(this);
            Comms.RegisterHandlers();

            _ChatManager = ChatServer;
            _MP = Multiplayer;

            //initilize PlayerWatcher
            MP.PlayerJoined += MP_PlayerJoined;
            MP.PlayerLeft += MP_PlayerLeft;
        }

        public void Dispose()
        {
            MP.PlayerJoined -= MP_PlayerJoined;
            MP.PlayerLeft -= MP_PlayerLeft;

            Comms.UnregisterHandlers();

            MarketData Data = new MarketData();
            Data.List = GridList;

            if (Config.GridMarketEnabled && IsHostServer)
            {
                    FileSaver.Save(System.IO.Path.Combine(Hangar.Dir, "Market.json"), Data);
            }
            MarketServers.Dispose();

        }



        private void MP_PlayerLeft(IPlayer obj)
        {
            //Main.Debug("Player Left! : " + obj.State.ToString());

            if (!Config.GridMarketEnabled || !Config.CrossServerEcon)
                return;

            try
            {
                Debug("PlayerState: " + obj.State.ToString());
                Hangar.Debug("Attempting to send account data!");
                Utilis.TryGetPlayerBalance(obj.SteamId, out long balance);


                CrossServerMessage message = new CrossServerMessage();
                message.Type = CrossServer.MessageType.PlayerAccountUpdated;
                message.BalanceUpdate.Add(new PlayerAccount(obj.Name, obj.SteamId, balance));
                //KeyValuePair<ulong, long> account = new KeyValuePair<ulong, long>(obj.SteamId, balance);


                //Send data to all servers!
                MarketServers.Update(message);
            }
            catch (Exception e)
            {
                Debug("Unable to update players balance!", e, ErrorType.Warn);
            }
            //throw new NotImplementedException();
        }

        private void MP_PlayerJoined(IPlayer obj)
        {
            if (!Config.GridMarketEnabled || !Config.CrossServerEcon)
                return;
            //Debug("Player Joined! Attempting to create IMyPlayer!");
            //MyMultiplayer.Static.SendChatMessage
            //long id = MyAPIGateway.Players.TryGetIdentityId(obj.SteamId);
            //Log.Info("Player ID: "+id);

            if (PlayerAccounts.ContainsKey(obj.SteamId))
            {
                BackgroundWorker worker = new BackgroundWorker();
                worker.DoWork += new DoWorkEventHandler(backgroundWorker1_DoWork);
                worker.RunWorkerAsync(obj);
            }

        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            IPlayer obj = (IPlayer)e.Argument;
            // Get the BackgroundWorker that raised this event.
            // BackgroundWorker worker = sender as BackgroundWorker;

            //Go ahead and check if player is in the dictionary. This means we need to wait until the player complteley connects

            DateTime Timer = DateTime.Now;
            Debug("Starting player watcher!");
            int LoopCounter = 0;
            int BadLoopCounter = 0;
            while (obj.State == ConnectionState.Connected)
            {
                BadLoopCounter++;

                if (Timer.AddSeconds(5) > DateTime.Now)
                    continue;

                LoopCounter++;


                //Debug("Checking playerID!");
                long IdentityID = MySession.Static.Players.TryGetIdentityId(obj.SteamId);

                if (IdentityID != 0)
                {
                    if (PlayerAccounts[obj.SteamId] < 0)
                    {
                        PlayerAccounts[obj.SteamId] = 0;
                        return;
                    }


                    bool Updated = Utilis.TryUpdatePlayerBalance(new PlayerAccount(obj.Name, obj.SteamId, PlayerAccounts[obj.SteamId]));
                    Hangar.Debug("Account updated: " + Updated);
                    return;
                }


                //Perform sanity vibe check
                if (LoopCounter >= 100)
                {
                    //Fucking who cares. return
                    return;
                }

                Timer = DateTime.Now;
            }

            Debug("PlayerLoopFinished! " + LoopCounter + ":" + BadLoopCounter);

        }



        //Forces refresh of all admin offered grids and syncs them to the blocks in the server
        public void UpdatePublicOffers()
        {

            //Update all public offer market items!


            //Need to remove existing ones
            for (int i = 0; i < GridList.Count; i++)
            {
                if (GridList[i].Steamid != 0)
                    continue;

                Hangar.Debug("Removing public offer: " + GridList[i].Name);
                GridList.RemoveAt(i);
            }

            string PublicOfferPath = ServerOffersDir;

            //Clear list
            PublicOfferseGridList.Clear();

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
                        Hangar.Debug("Error on BP: " + offer.Name + "! Most likely you put in the SBC5 file! Dont do that!");
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
                    PublicOfferseGridList.Add(NewList);

                    Hangar.Debug("Adding new public offer: " + offer.Name);
                }
            }

            //Update Everything!
            Comms.SendListToModOnInitilize();

            MarketData Data = new MarketData();
            Data.List = PublicOfferseGridList;

            FileSaver.Save(ServerMarketFileDir, Data);
        }
        public void PurchaseGrid(GridsForSale grid)
        {
            //Need to check hangar of the person who bought the grid

            if (!Config.GridMarketEnabled)
            {
                return;
            }
            //Get Item from market list for price
            MarketList Item = null;

            List<MarketList> Allitems = new List<MarketList>();

            //Incluse all offers!
            Allitems.AddRange(GridList);
            Allitems.AddRange(PublicOfferseGridList);


            try
            {
                Item = Allitems.First(x => x.Name == grid.name);
            }
            catch
            {
                _ChatManager.SendMessageAsOther("GridMarket", "Grid has been removed from market!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                return;
            }


            MyPlayer.PlayerId BuyPlayer = new MyPlayer.PlayerId(grid.BuyerSteamid);
            MySession.Static.Players.TryGetPlayerById(BuyPlayer, out MyPlayer Buyer);

            GridMethods BuyerMethods = new GridMethods(grid.BuyerSteamid, Config.FolderDirectory);
            //string BuyerPath = GridMethods.CreatePathForPlayer(Config.FolderDirectory, grid.BuyerSteamid);

            if (Buyer == null)
            {
                Hangar.Debug("Unable to get steamID. Glitched player?");
                return;
                //Some kind of error message directed to player.
            }

            //Debug("Seller SteamID: " + Item.Steamid);
            MyCharacter Player = Buyer.Character;
            Hangar.Debug("Player Buying grid: " + Player.DisplayName + " [" + grid.BuyerSteamid + "]");

            //Start transfer of grids!
            int MaxStorage = Config.NormalHangarAmount;
            if (Player.PromoteLevel >= MyPromoteLevel.Scripter)
            {
                MaxStorage = Config.ScripterHangarAmount;
            }

            PlayerInfo BuyerData = new PlayerInfo();
            //Get PlayerInfo from file
            try
            {
                BuyerData = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(BuyerMethods.FolderPath, "PlayerInfo.json")));
                if (BuyerData.Grids.Count >= MaxStorage)
                {
                    _ChatManager.SendMessageAsOther("GridMarket", "Unable to purchase grid! No room in hangar!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    return;
                }
            }
            catch
            {
                Hangar.Debug("Buyer doesnt have anything stored in their hangar! THIS IS NOT AN ERROR!");
                //ChatManager.SendMessageAsOther("GridMarket", "Unknown error! Contact admin!", MyFontEnum.Red, grid.BuyerSteamid);
                //Debug("Deserialization error! Make sure files exist! \n" + e);
                //New player. Go ahead and create new. Should not have a timer.
            }



            //Adjust player prices (We need to check if buyer has enough moneyies hehe)
            bool RetrieveSuccessful = Utilis.TryGetPlayerBalance(grid.BuyerSteamid, out long BuyerBallance);
            if (!RetrieveSuccessful || BuyerBallance < Item.Price)
            {
                _ChatManager.SendMessageAsOther("GridMarket", "Unable to purchase grid! Not enough credits!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                return;
            }




            string NewGridName = Item.Name;
            bool FileStilExists = true;
            int i = 0;
            if (File.Exists(Path.Combine(BuyerMethods.FolderPath, NewGridName + ".sbc")))
            {

                while (FileStilExists)
                {
                    i++;
                    if (!File.Exists(Path.Combine(BuyerMethods.FolderPath, NewGridName + "[" + i + "].sbc")))
                    {
                        FileStilExists = false;

                    }
                    //Turns out there is already a ship with that name in this players hangar!
                    if (i > 50)
                    {
                        _ChatManager.SendMessageAsOther("GridMarket", "Dude what the actual fuck do you need 50 of these for?", VRageMath.Color.Yellow, grid.BuyerSteamid);
                        Hangar.Debug("Dude what the actual fuck do you need 50 of these for? Will not continue due to... security reasons. @" + NewGridName);
                        return;
                    }

                }

                NewGridName = NewGridName + "[" + i + "]";
            }








            if (Item.Steamid == 0)
            {
                //Check to see if the item existis in the dir
                string PublicOfferPath = ServerOffersDir;
                string GridPath = Path.Combine(PublicOfferPath, Item.Name + ".sbc");

                //Add counter just in case some idiot


                string BuyerGridPath = Path.Combine(BuyerMethods.FolderPath, NewGridName + ".sbc");

                if (!File.Exists(GridPath))
                {
                    _ChatManager.SendMessageAsOther("GridMarket", "Server Offer got manually deleted from folder! Blame admin!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    Hangar.Debug("Someone tried to buy a ship that doesnt exist! Did you delete it?? @" + GridPath);
                }

                //Need to check if player can buy
                PublicOffers Offer;
                int Index;
                try
                {
                    Offer = Config.PublicOffers.First(x => x.Name == Item.Name);
                    Index = Config.PublicOffers.IndexOf(Offer);
                }
                catch (Exception e)
                {
                    _ChatManager.SendMessageAsOther("GridMarket", "Unknown error! Contact admin!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    Hangar.Debug("Unknown error! @" + GridPath, e, ErrorType.Fatal);
                    return;
                    //Something went wrong
                }



                if (Offer.TotalPerPlayer != 0 && !WithinPlayerLimits(Item.PlayerPurchases, grid.BuyerSteamid, Offer.TotalPerPlayer))
                {
                    _ChatManager.SendMessageAsOther("GridMarket", "Youve reached your buy limit on this grid!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    return;
                }





                //File check complete!
                Hangar.Debug("Old Buyers Balance: " + BuyerBallance);

                //Create PlayerAccount
                PlayerAccount BuyerAccount = new PlayerAccount(Player.DisplayName, grid.BuyerSteamid, BuyerBallance - Item.Price);


                //Need to figure out HOW to get the data in here
                GridStamp stamp = new GridStamp();
                stamp.GridForSale = false;
                stamp.GridName = NewGridName;


                //Add it to buyers info
                File.Copy(GridPath, BuyerGridPath);
                BuyerData.Grids.Add(stamp);


                //Update ALL blocks
                MyObjectBuilderSerializer.DeserializeXML(BuyerGridPath, out MyObjectBuilder_Definitions myObjectBuilder_Definitions);

                MyObjectBuilder_ShipBlueprintDefinition[] shipBlueprint = myObjectBuilder_Definitions.ShipBlueprints;
                foreach (MyObjectBuilder_ShipBlueprintDefinition definition in myObjectBuilder_Definitions.ShipBlueprints)
                {

                    foreach (MyObjectBuilder_CubeGrid CubeGridDef in definition.CubeGrids)
                    {
                        foreach (MyObjectBuilder_CubeBlock block in CubeGridDef.CubeBlocks)
                        {
                            block.Owner = Buyer.Identity.IdentityId;
                            block.BuiltBy = Buyer.Identity.IdentityId;
                            //Could turnoff warheads etc here
                        }
                    }
                }

                MyObjectBuilderSerializer.SerializeXML(BuyerGridPath, false, myObjectBuilder_Definitions);


                CrossServerMessage SendAccountUpdate = new CrossServerMessage();
                SendAccountUpdate.Type = CrossServer.MessageType.PlayerAccountUpdated;
                SendAccountUpdate.BalanceUpdate.Add(BuyerAccount);

                MarketServers.Update(SendAccountUpdate);

                _ChatManager.SendMessageAsOther("HangarMarket", Player.DisplayName + " just bought a " + grid.name, VRageMath.Color.Yellow);

                //Write all files!
                FileSaver.Save(Path.Combine(BuyerMethods.FolderPath, "PlayerInfo.json"), BuyerData);
                //File.WriteAllText(Path.Combine(BuyerPath, "PlayerInfo.json"), JsonConvert.SerializeObject(BuyerData));

                Offer.NumberOfBuys++;

                if (Offer.TotalPerPlayer != 0)
                {
                    AddPlayerPurchase(PublicOfferseGridList, Item, grid.BuyerSteamid);
                }

                //Check Total Limit
                if (Offer.NumberOfBuys >= Offer.TotalAmount)
                {
                    Config.PublicOffers[Index].Forsale = false;
                    //Update offers and refresh
                    UpdatePublicOffers();
                }

            }
            else
            {
                //This is a player offer

                GridMethods SellerMethods = new GridMethods(Item.Steamid, Dir);
                //string SellerPath = GridMethods.CreatePathForPlayer(Dir, Item.Steamid);
                Debug("Seller SteamID: " + Item.Steamid);


                PlayerInfo SellerData = new PlayerInfo();


                try
                {
                    SellerData = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(SellerMethods.FolderPath, "PlayerInfo.json")));
                }
                catch (Exception e)
                {
                    Hangar.Debug("Seller Hangar Playerinfo is missing! Did they get deleted by admin?", e, ErrorType.Warn);
                    _ChatManager.SendMessageAsOther("GridMarket", "Seller hangar info is missing! Contact admin!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    return;
                }



                CrossServerMessage SendMessage = new CrossServerMessage();
                SendMessage.Type = CrossServer.MessageType.RemoveItem;
                SendMessage.List.Add(Item);
                MarketServers.Update(SendMessage);


                PlayerAccount BuyerAccount = new PlayerAccount(Player.DisplayName, grid.BuyerSteamid, BuyerBallance - Item.Price);
                PlayerAccount SellerAccount = new PlayerAccount(Item.Seller, Item.Steamid, Item.Price, true);



                //Get grids
                GridStamp gridsold = SellerData.Grids.FirstOrDefault(x => x.GridName == grid.name);
                //Reset grid for sale and remove it from the sellers hangarplayerinfo
                gridsold.GridForSale = false;
                SellerData.Grids.Remove(gridsold);
                gridsold.GridName = NewGridName;

                //Add it to buyers info
                BuyerData.Grids.Add(gridsold);

                //Move grid in folders!
                string SellerGridPath = Path.Combine(SellerMethods.FolderPath, grid.name + ".sbc");
                string BuyerGridPath = Path.Combine(BuyerMethods.FolderPath, NewGridName + ".sbc");

                File.Move(SellerGridPath, BuyerGridPath);

                //After move we need to update all block owners and authors! this meanings opening up the grid and deserializing it to iterate the grid! (we dont want the seller to still be the author)

                MyObjectBuilderSerializer.DeserializeXML(BuyerGridPath, out MyObjectBuilder_Definitions myObjectBuilder_Definitions);

                MyObjectBuilder_ShipBlueprintDefinition[] shipBlueprint = myObjectBuilder_Definitions.ShipBlueprints;
                foreach (MyObjectBuilder_ShipBlueprintDefinition definition in myObjectBuilder_Definitions.ShipBlueprints)
                {

                    foreach (MyObjectBuilder_CubeGrid CubeGridDef in definition.CubeGrids)
                    {
                        foreach (MyObjectBuilder_CubeBlock block in CubeGridDef.CubeBlocks)
                        {
                            block.Owner = Buyer.Identity.IdentityId;
                            block.BuiltBy = Buyer.Identity.IdentityId;
                            //Could turnoff warheads etc here
                        }
                    }
                }

                MyObjectBuilderSerializer.SerializeXML(BuyerGridPath, false, myObjectBuilder_Definitions);

                //byte[] Definition = MyAPIGateway.Utilities.SerializeToBinary(grid);

                //Get grid definition of removed grid. (May not even need to get this lol)
                //GridsForSale RemovedGrid = Main.GridDefinition.FirstOrDefault(x => x.name == Item.Name);


                //We need to send to all to remove the item from the list

                CrossServerMessage SendAccountUpdate = new CrossServerMessage();
                SendAccountUpdate.Type = CrossServer.MessageType.PlayerAccountUpdated;
                SendAccountUpdate.BalanceUpdate.Add(BuyerAccount);
                SendAccountUpdate.BalanceUpdate.Add(SellerAccount);

                MarketServers.Update(SendAccountUpdate);
                _ChatManager.SendMessageAsOther("HangarMarket", Player.DisplayName + " just bought a " + grid.name, VRageMath.Color.Yellow);


                

                //Write all files!
                FileSaver.Save(Path.Combine(SellerMethods.FolderPath, "PlayerInfo.json"), SellerData);
                FileSaver.Save(Path.Combine(BuyerMethods.FolderPath, "PlayerInfo.json"), BuyerData);
                //File.WriteAllText(Path.Combine(SellerPath, "PlayerInfo.json"), JsonConvert.SerializeObject(SellerData));
                //File.WriteAllText(Path.Combine(BuyerPath, "PlayerInfo.json"), JsonConvert.SerializeObject(BuyerData));
            }
            //Transfer Grid and transfer Author/owner!
        }


        public bool WithinPlayerLimits(Dictionary<ulong, int> Players, ulong ID, int BuyLimit)
        {

            if (Players.TryGetValue(ID, out int value))
            {
                if (value >= BuyLimit)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                //Player is not even in the collection
                return true;
            }
        }
        public void AddPlayerPurchase(List<MarketList> PublicMarketItems, MarketList Item, ulong ID)
        {
            Dictionary<ulong, int> Players = Item.PlayerPurchases;
            //Added player to list
            if (Players.TryGetValue(ID, out int value))
            {
                Players[ID] = value + 1;
            }
            else
            {
                //No value for player. Add
                Players.Add(ID, 1);
            }



            for (int i = 0; i < PublicMarketItems.Count(); i++)
            {
                if (PublicMarketItems[i].Name == Item.Name)
                {
                    //Re-apply new dictionary
                    PublicMarketItems[i].PlayerPurchases = Players;
                    return;
                }
            }


        }

    }
}
