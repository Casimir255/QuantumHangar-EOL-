using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.API.Plugins;
using Torch.Managers;
using VRage.Game;
using VRage.Game.ModAPI;
using NLog;
using System.Windows.Controls;
using QuantumHangar.UI;
using Newtonsoft.Json;
using Sandbox.Game.Entities.Character;
using Torch.Managers.ChatManager;
using VRage.ObjectBuilders;
using System.ComponentModel;
using Torch.Session;
using Torch.API.Session;
using System.Collections.ObjectModel;
using System.Reflection;
using QuantumHangar.Utilities;

namespace QuantumHangar
{
    public class Main : TorchPluginBase, IWpfPlugin
    {
        private static readonly Logger Log = LogManager.GetLogger("QuantumHangar");
        public bool RunOnce = false;
        public const ushort NETWORK_ID = 2934;

        public Settings Config => _config?.Data;

        private Persistent<Settings> _config;
        public static List<MarketList> GridList = new List<MarketList>();
        public static List<MarketList> PublicOfferseGridList = new List<MarketList>();
        public static string ServerMarketFileDir;
        public static string ServerOffersDir;


        public Dictionary<long, CurrentCooldown> ConfirmationsMap { get; } = new Dictionary<long, CurrentCooldown>();
        public static string Dir;
        public static MultiplayerManagerBase MP;
        public static TorchSessionManager TorchSession;

        private static bool EnableDebug = true;
        public static bool IsRunning = false;

        public static bool IsHostServer = false;

        public CrossServer MarketServers = new CrossServer();
        public static Comms Comms;
        private bool ServerRunning;
        public static MethodInfo CheckFuture;


        public static Dictionary<ulong, long> PlayerAccounts = new Dictionary<ulong, long>();
        public static Dictionary<ulong, long> BalanceAdjustment = new Dictionary<ulong, long>();

        //Used to compare times
        public DateTime Stamp;


        public enum ErrorType
        {
            Debug,
            Fatal,
            Trace,
            Warn
        }


        public int TickCounter = 0;

        //public ObservableCollection<TimeStamp> TimedGroup = new ObservableCollection<TimeStamp>();
        public UserControl _control;
        public UserControl GetControl() => _control ?? (_control = new UserControlInterface(this));
        public static ChatManagerServer ChatManager;
        

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            //Grab Settings
            string path = Path.Combine(StoragePath, "QuantumHangar.cfg");




            _config = Persistent<Settings>.Load(path);

            if (Config.FolderDirectory == null || Config.FolderDirectory == "")
            {

                Config.FolderDirectory = Path.Combine(StoragePath, "QuantumHangar");
            }

            TorchSession = Torch.Managers.GetManager<TorchSessionManager>();
            if (TorchSession != null)
                TorchSession.SessionStateChanged += SessionChanged;

            try
            {
                if (Config.GridMarketEnabled)
                {
                    //Attempt to create market servers!
                    if (MarketServers.CreateServers() == false)
                    {
                        Debug("Unable to start market servers! Check logs & contact plugin dev!");
                        Config.GridMarketEnabled = false;
                    }

                    Config.HostServer = IsHostServer;

                    if (IsHostServer)
                    {
                        MarketServers.Main = this;
                        MarketData Data = new MarketData();
                        MarketData PublicData = new MarketData();
                        Accounts Accounts = new Accounts();
                        string MarketPath = Path.Combine(Config.FolderDirectory, "Market.json");
                        string PlayerAccountsPath = Path.Combine(Config.FolderDirectory, "PlayerAccounts.json");
                        ServerOffersDir = Path.Combine(StoragePath, "HangarServerOffers");
                        ServerMarketFileDir = Path.Combine(ServerOffersDir, "HangarServerOffers.json");
                        Directory.CreateDirectory(ServerOffersDir);


                        //Initilize loading of files!
                        if (File.Exists(MarketPath))
                        {
                            using (StreamReader file = File.OpenText(MarketPath))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                Data = (MarketData)serializer.Deserialize(file, typeof(MarketData));
                            }
                        }


                        if (File.Exists(ServerMarketFileDir))
                        {
                            using (StreamReader file = File.OpenText(ServerMarketFileDir))
                            {
                                JsonSerializer serializer = new JsonSerializer();
                                PublicData = (MarketData)serializer.Deserialize(file, typeof(MarketData));
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
                        Debug("Requesting update!");

                        //Client start. (Tell server to broadcast to all clients!)
                        CrossServerMessage SendMessage = new CrossServerMessage();
                        SendMessage.Type = CrossServer.MessageType.RequestAll;

                        MarketServers.Update(SendMessage);
                        Main.Debug("Initial Send Request");
                    }
                }
                else
                {
                    Debug("Starting plugin WITHOUT the Hangar Market!", null, ErrorType.Warn);
                }




            }
            catch (Exception e)
            {
                Log.Info("Unable to load grid market files! " + e);
            }

            EnableDebug = Config.AdvancedDebug;
            Dir = Config.FolderDirectory;
            //Load files
        }



        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            ServerRunning = state == TorchSessionState.Loaded;
            switch (state)
            {
                case TorchSessionState.Loaded:
                    Comms = new Comms();
                    Comms.Main = this;
                    IsRunning = true;
                    Comms.RegisterHandlers();

                    MP = Torch.CurrentSession.Managers.GetManager<MultiplayerManagerBase>();
                    ChatManager = Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
                    PluginManager Plugins = Torch.CurrentSession.Managers.GetManager<PluginManager>();

                    //Guid for BlockLimiter:
                    Guid BlockLimiterGUID = new Guid("11fca5c4-01b6-4fc3-a215-602e2325be2b");
                    Plugins.Plugins.TryGetValue(BlockLimiterGUID, out ITorchPlugin BlockLimiterT);

                    
                    if (BlockLimiterT != null)
                    {
                        Main.Debug("Plugin: " + BlockLimiterT.Name + " " + BlockLimiterT.Version + " is installed!");
                        try
                        {
                            //Grab refrence to TorchPluginBase class in the plugin
                            Type Class = BlockLimiterT.GetType();

                            //Grab simple MethoInfo when using BlockLimiter
                            CheckFuture = Class.GetMethod("CheckLimits_future");


                            //Example Method call
                            //object value = CandAddMethod.Invoke(Class, new object[] { grid });
                            //Convert to value return type
                            Log.Info("BlockLimiter Reference added to PCU-Transferrer for limit checks.");

                        }
                        catch (Exception e)
                        {
                            Log.Warn(e, "Could not connect to Blocklimiter Plugin.");
                        }
                    }





                    MP.PlayerJoined += MP_PlayerJoined;
                    MP.PlayerLeft += MP_PlayerLeft;

                    Stamp = DateTime.Now;
                    break;


                case TorchSessionState.Unloading:
                    PluginDispose();


                    break;


            }
        }

        private void MP_PlayerLeft(IPlayer obj)
        {
            //Main.Debug("Player Left! : " + obj.State.ToString());

            if (!Config.GridMarketEnabled)
                return;

            if (obj.State == ConnectionState.Connected)
            {
                Main.Debug("Attempting to send account data!");
                EconUtils.TryGetPlayerBalance(obj.SteamId, out long balance);


                CrossServerMessage message = new CrossServerMessage();
                message.Type = CrossServer.MessageType.PlayerAccountUpdated;
                message.BalanceUpdate.Add(new PlayerAccount(obj.Name, obj.SteamId, balance));
                //KeyValuePair<ulong, long> account = new KeyValuePair<ulong, long>(obj.SteamId, balance);


                //Send data to all servers!
                MarketServers.Update(message);
            }
            //throw new NotImplementedException();
        }

        private void MP_PlayerJoined(IPlayer obj)
        {
            if (!Config.GridMarketEnabled)
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
                    bool Updated = EconUtils.TryUpdatePlayerBalance(new PlayerAccount(obj.Name, obj.SteamId, PlayerAccounts[obj.SteamId]));
                    Main.Debug("Account updated: " + Updated);
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

            Debug("PlayerLoopFinished! "+LoopCounter+":"+BadLoopCounter);

        }

        public override void Update()
        {
         
            //Optional how often to check
            if(Stamp.AddHours(3) < DateTime.Now)
            {
                //Run checks
                if (IsHostServer)
                {
                    if (Config.AutoHangarGrids)
                    {
                        Debug("Attempting Autohangar!");
                        //Stamp.AddHours(.5);
                        BackgroundWorker worker = new BackgroundWorker();
                        worker.DoWork += new DoWorkEventHandler(HangarScans.AutoHangar);
                        worker.RunWorkerAsync(Config);

                    }

                    if (Config.AutosellHangarGrids)
                    {
                        var p = Task.Run(() => HangarScans.AutoSell(MarketServers, Config.FolderDirectory, Config.SellAFKDayAmount));
                    }
                }


                Stamp = DateTime.Now;
            }



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
                ChatManager.SendMessageAsOther("GridMarket", "Grid has been removed from market!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                return;
            }


            MyPlayer.PlayerId BuyPlayer = new MyPlayer.PlayerId(grid.BuyerSteamid);
            MySession.Static.Players.TryGetPlayerById(BuyPlayer, out MyPlayer Buyer);
            string BuyerPath = GridMethods.CreatePathForPlayer(Dir, grid.BuyerSteamid);

            if (Buyer == null)
            {
                Debug("Unable to get steamID!");
                return;
                //Some kind of error message directed to player.
            }

            //Debug("Seller SteamID: " + Item.Steamid);
            MyCharacter Player = Buyer.Character;
            Debug("Player Buying grid: " + Player.DisplayName+ " ["+ grid.BuyerSteamid+"]");

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
                BuyerData = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(BuyerPath, "PlayerInfo.json")));
                if (BuyerData.Grids.Count >= MaxStorage)
                {
                    ChatManager.SendMessageAsOther("GridMarket", "Unable to purchase grid! No room in hangar!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    return;
                }
            }
            catch
            {
                Main.Debug("Buyer doesnt have anything stored in their hangar! THIS IS NOT AN ERROR!");
                //ChatManager.SendMessageAsOther("GridMarket", "Unknown error! Contact admin!", MyFontEnum.Red, grid.BuyerSteamid);
                //Debug("Deserialization error! Make sure files exist! \n" + e);
                //New player. Go ahead and create new. Should not have a timer.
            }

            //Adjust player prices (We need to check if buyer has enough moneyies hehe)
            bool RetrieveSuccessful = EconUtils.TryGetPlayerBalance(grid.BuyerSteamid, out long BuyerBallance);
            if (!RetrieveSuccessful || BuyerBallance < Item.Price)
            {
                ChatManager.SendMessageAsOther("GridMarket", "Unable to purchase grid! Not enough credits!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                return;
            }

            string NewGridName = Item.Name;
            bool FileStilExists = true;
            int i = 0;
            if(File.Exists(Path.Combine(BuyerPath, NewGridName + ".sbc"))){

                while (FileStilExists)
                {
                    i++;
                    if (!File.Exists(Path.Combine(BuyerPath, NewGridName + "[" + i + "].sbc")))
                    {
                        FileStilExists = false;
                        
                    }
                    //Turns out there is already a ship with that name in this players hangar!
                    if (i > 50)
                    {
                        ChatManager.SendMessageAsOther("GridMarket", "Dude what the actual fuck do you need 50 of these for?", VRageMath.Color.Yellow, grid.BuyerSteamid);
                        Main.Debug("Dude what the actual fuck do you need 50 of these for? Will not continue due to... security reasons. @" + NewGridName);
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
               

                string BuyerGridPath = Path.Combine(BuyerPath, NewGridName + ".sbc");

                if (!File.Exists(GridPath))
                {
                    ChatManager.SendMessageAsOther("GridMarket", "Server Offer got manually deleted from folder! Blame admin!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    Main.Debug("Someone tried to buy a ship that doesnt exist! Did you delete it?? @"+ GridPath);
                }

                //Need to check if player can buy
                PublicOffers Offer;
                int Index;
                try
                {
                    Offer = Config.PublicOffers.First(x => x.Name == Item.Name);
                    Index = Config.PublicOffers.IndexOf(Offer);
                }catch(Exception e)
                {
                    ChatManager.SendMessageAsOther("GridMarket", "Unknown error! Contact admin!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                    Main.Debug("Unknown error! @" + GridPath,e,ErrorType.Fatal);
                    return;
                    //Something went wrong
                }

                //Dictionary<ulong, int> PlayerBuys = Offer.PlayersPurchased;

                /*
                if (Offer.TotalPerPlayer != 0)
                {
                    if (PlayerBuys.TryGetValue(grid.BuyerSteamid, out int Value))
                    {
                        if(Value >= Offer.TotalPerPlayer)
                        {
                            ChatManager.SendMessageAsOther("GridMarket", "You cannot buy anymore of this grid!", VRageMath.Color.Yellow, grid.BuyerSteamid);
                            return;
                        }
                    }
                }*/



                //File check complete!
                Main.Debug("Old Buyers Balance: " + BuyerBallance);

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
                
                ChatManager.SendMessageAsOther("HangarMarket", Player.DisplayName + " just bought a " + grid.name, VRageMath.Color.Yellow);

                //Write all files!
                FileSaver.Save(Path.Combine(BuyerPath, "PlayerInfo.json"), BuyerData);
                //File.WriteAllText(Path.Combine(BuyerPath, "PlayerInfo.json"), JsonConvert.SerializeObject(BuyerData));

                Offer.NumberOfBuys++;


                /*
                //Add PerPlayerSave
                if (Offer.TotalPerPlayer != 0)
                {
                    if (PlayerBuys.TryGetValue(grid.BuyerSteamid, out int Value))
                    {
                        PlayerBuys[grid.BuyerSteamid] = Value + 1;
                    }
                    else
                    {
                        PlayerBuys.Add(grid.BuyerSteamid, 1);
                    }
                }
                */

                //Check Total Limit
                if(Offer.NumberOfBuys >= Offer.TotalAmount)
                {
                    Config.PublicOffers[Index].Forsale = false;
                    //Update offers and refresh
                    GridMarket.UpdatePublicOffers(Config);
                }

            }
            else
            {
                //This is a player offer
                string SellerPath = GridMethods.CreatePathForPlayer(Dir, Item.Steamid);
                Debug("Seller SteamID: " + Item.Steamid);
                

                PlayerInfo SellerData = new PlayerInfo();


                try
                {
                    SellerData = JsonConvert.DeserializeObject<PlayerInfo>(File.ReadAllText(Path.Combine(SellerPath, "PlayerInfo.json")));
                }
                catch (Exception e)
                {
                    Main.Debug("Seller Hangar Playerinfo is missing! Did they get deleted by admin?", e, ErrorType.Warn);
                    ChatManager.SendMessageAsOther("GridMarket", "Seller hangar info is missing! Contact admin!", VRageMath.Color.Yellow, grid.BuyerSteamid);
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
                string SellerGridPath = Path.Combine(SellerPath, grid.name + ".sbc");
                string BuyerGridPath = Path.Combine(BuyerPath, NewGridName + ".sbc");

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
                ChatManager.SendMessageAsOther("HangarMarket", Player.DisplayName + " just bought a " + grid.name, VRageMath.Color.Yellow);

                //Write all files!
                FileSaver.Save(Path.Combine(SellerPath, "PlayerInfo.json"), SellerData);
                FileSaver.Save(Path.Combine(BuyerPath, "PlayerInfo.json"), BuyerData);
                //File.WriteAllText(Path.Combine(SellerPath, "PlayerInfo.json"), JsonConvert.SerializeObject(SellerData));
                //File.WriteAllText(Path.Combine(BuyerPath, "PlayerInfo.json"), JsonConvert.SerializeObject(BuyerData));
            }
            //Transfer Grid and transfer Author/owner!
        }
        public void PluginDispose()
        {
            //Un register events
            MP.PlayerJoined -= MP_PlayerJoined;
            MP.PlayerLeft -= MP_PlayerLeft;
            Comms.UnregisterHandlers();


            //Save market data!
            MarketData Data = new MarketData();
            Data.List = Main.GridList;


            //Save market Items
            if (Config.GridMarketEnabled)
            {
                FileSaver.Save(System.IO.Path.Combine(Main.Dir, "Market.json"), Data);
                //File.WriteAllText(System.IO.Path.Combine(Main.Dir, "Market.json"), JsonConvert.SerializeObject(Data));

                if (IsHostServer)
                {
                    MarketServers.Dispose();
                }
            }
        }


        public static void Debug(string message, Exception e = null, ErrorType error = ErrorType.Debug)
        {

            if (e != null)
            {
                if (error == ErrorType.Debug)
                {
                    Log.Debug(e, message);
                }
                else if (error == ErrorType.Fatal)
                {
                    Log.Fatal(e, message);
                }
                else if (error == ErrorType.Warn)
                {
                    Log.Warn(e, message);
                }
                else
                {
                    Log.Trace(e, message);
                }

            }
            else
            {
                if (!EnableDebug)
                {
                    return;
                }

                Log.Info(message);

            }

        }


    }


    public class CurrentCooldown
    {

        private long _startTime;
        //private long _currentCooldown;

        private string grid;
        public void StartCooldown(string command)
        {
            this.grid = command;
            _startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        public bool CheckCommandStatus(string command)
        {

            if (this.grid != command)
                return true;

            long elapsedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _startTime;

            if (elapsedTime >= 30000)
                return true;

            return false;

        }
    }


}



