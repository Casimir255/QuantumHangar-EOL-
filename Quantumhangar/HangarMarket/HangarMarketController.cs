using Newtonsoft.Json;
using NLog;
using QuantumHangar.HangarChecks;
using QuantumHangar.Serialization;
using QuantumHangar.Utils;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Timers;

namespace QuantumHangar.HangarMarket
{
    public class HangarMarketController
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        //files will by .json 

        private static Settings Config => Hangar.Config;
        private static string _marketFolderDir;
        public static string PublicOffersDir;


        public static ConcurrentBag<MarketListing> Listings = new ConcurrentBag<MarketListing>();


        public static ConcurrentDictionary<string, MarketListing> MarketOffers =
            new ConcurrentDictionary<string, MarketListing>();

        public static Queue<string> NewFileQueue = new Queue<string>();

        public static Timer NewFileTimer = new Timer(500);


        // We use this to read new offers
        public static ClientCommunication Communication { get; private set; }

        private static MethodInfo _sendNewProjection;


        public HangarMarketController()
        {
            //Run this when server initializes
            _marketFolderDir = Path.Combine(Hangar.Config.FolderDirectory, "HangarMarket");
            PublicOffersDir = Path.Combine(_marketFolderDir, "PublicServerOffers");


            //Make sure to create the market directory
            Directory.CreateDirectory(_marketFolderDir);
            Directory.CreateDirectory(PublicOffersDir);


            //Initialize server and read all existing market files
            var MarketFileOffers = Directory.GetFiles(_marketFolderDir, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var OfferPath in MarketFileOffers)
            {
                var FileName = Path.GetFileName(OfferPath);
                // Log.Error($"Adding File: {FileName}");


                try
                {
                    GetReadMarketFile(OfferPath, out var Offer);
                    MarketOffers.TryAdd(FileName, Offer);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                }
            }


            NewFileTimer.Elapsed += NewFileTimer_Elapsed;
            NewFileTimer.Start();


            //Read all grids and get their objectbuilders serialized and ready...
            //Create fileSystemWatcher
            var marketWatcher = new FileSystemWatcher(_marketFolderDir);
            marketWatcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.LastWrite |
                                         NotifyFilters.CreationTime | NotifyFilters.Security | NotifyFilters.FileName;


            marketWatcher.Created += MarketWatcher_Created;
            marketWatcher.Deleted += MarketWatcher_Deleted;
            marketWatcher.Changed += MarketWatcher_Changed;
            marketWatcher.Renamed += MarketWatcher_Renamed;


            marketWatcher.IncludeSubdirectories = true;
            marketWatcher.EnableRaisingEvents = true;


            _sendNewProjection =
                typeof(MyProjectorBase).GetMethod("SendNewBlueprint", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static void NewFileTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //Use this to periodically check the new file queue


            if (NewFileQueue.Count == 0)
                return;


            //Loop through queue and add new items
            while (NewFileQueue.Count != 0)
            {
                var FilePath = NewFileQueue.Peek();

                try
                {
                    GetReadMarketFile(FilePath, out var Offer);


                    if (MarketOffers.TryAdd(Path.GetFileName(FilePath), Offer))
                        //Log.Error("Added this file to the dictionary!");

                        NewFileQueue.Dequeue();
                }
                catch (IOException)
                {
                    //Throws if we are still currently writing to file
                    return;
                }
                catch (Exception ex)
                {
                    //Only remove this file from the queue if its not a file access

                    Log.Error(ex);
                    NewFileQueue.Dequeue();
                    File.Delete(FilePath);
                }
            }

            //Send new offer update to all clients
            Communication.UpdateAllOffers();
        }

        public void ServerStarted()
        {
            Communication = new ClientCommunication();
        }

        public void Close()
        {
            Communication?.close();
        }


        private static void MarketWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (".json" != Path.GetExtension(e.Name))
                return;

            Log.Warn($"File {e.FullPath} renamed!");
        }

        private static void MarketWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //Market offer changed
            if (".json" != Path.GetExtension(e.Name))
                return;

            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;

            Log.Warn($"File {e.FullPath} changed!");
        }

        private void MarketWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            if (".json" != Path.GetExtension(e.Name))
                return;

            //Market offer deleted

            //Log.Warn($"File {e.FullPath} deleted! {e.Name}");

            MarketOffers.TryRemove(e.Name, out _);
            //Log.Error("Removed this file from the dictionary!");


            //Send new offer update to all clients
            Communication.UpdateAllOffers();
        }

        private static void MarketWatcher_Created(object sender, FileSystemEventArgs e)
        {
            //New market offer created
            //Log.Warn($"File {e.FullPath} created! {e.Name}");

            if (".json" != Path.GetExtension(e.Name))
                return;

            NewFileQueue.Enqueue(e.FullPath);
        }


        private static void GetReadMarketFile(string FilePath, out MarketListing Listing)
        {
            //Reads market file from path

            using var fs = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, Encoding.UTF8);

            var Data = sr.ReadToEnd();


            Listing = JsonConvert.DeserializeObject<MarketListing>(File.ReadAllText(FilePath));
        }

        public static void RemoveMarketListing(ulong Owner, string Name)
        {
            var FileName = GetNameFormat(Owner, Name);
            MarketOffers.TryGetValue(FileName, out var Listing);

            if (Listing != null)
                GirdOfferRemoved(Listing);

            File.Delete(Path.Combine(_marketFolderDir, FileName));
        }

        public static bool SaveNewMarketFile(MarketListing NewListing)
        {
            //Saves a new market listing

            var FileName = GetNameFormat(NewListing.SteamID, NewListing.Name);

            try
            {
                //Save new market offer
                File.WriteAllText(Path.Combine(_marketFolderDir, FileName),
                    JsonConvert.SerializeObject(NewListing, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }


        private static bool ValidGrid(ulong Owner, string GridName, out MarketListing Offer, out string GridPath)
        {
            GridPath = string.Empty;

            var FileName = GetNameFormat(Owner, GridName);


            if (!MarketOffers.TryGetValue(FileName, out Offer))
                return false;


            //Check if file exists
            if (!File.Exists(Path.Combine(_marketFolderDir, FileName)))
            {
                //Someone this happened?
                MarketOffers.TryRemove(FileName, out _);
                return false;
            }


            if (Offer.ServerOffer)
            {
                GridPath = Offer.FileSBCPath;
            }
            else
            {
                var FolderPath = Path.Combine(Hangar.MainPlayerDirectory, Owner.ToString());
                GridPath = Path.Combine(FolderPath, GridName + ".sbc");
            }


            //Confirm files exits
            if (File.Exists(GridPath)) return true;
            RemoveMarketListing(Owner, GridName);
            return false;

        }

        public static void SetGridPreview(long EntityID, ulong Owner, string GridName)
        {
            if (!ValidGrid(Owner, GridName, out var Offer, out var GridPath))
                return;


            if (Offer.NumberofBlocks > 100000)
            {
                Log.Warn("MarketPreview Blocked. Grid is greater than 100000 blocks");
                return;
            }


            //Need async

            Log.Warn("Loading Grid");
            if (!GridSerializer.LoadGrid(GridPath, out var GridBuilders))
            {
                RemoveMarketListing(Owner, GridName);
                return;
            }


            //Now attempt to load grid
            if (!MyEntities.TryGetEntityById(EntityID, out var entity)) return;
            if (!(entity is MyProjectorBase proj)) return;
            proj.SendRemoveProjection();
            var Grids = GridBuilders.ToList();
            Log.Warn("Setting projection!");
            _sendNewProjection.Invoke(proj, new object[] { Grids });
        }

        public static void PurchaseGridOffer(ulong Buyer, ulong Owner, string GridName)
        {
            Log.Error($"BuyerRequest: {Buyer}, Owner {Owner}, GridName {GridName}");

            if (!ValidGrid(Owner, GridName, out var Offer, out var GridPath))
                return;


            if (!MySession.Static.Players.TryGetPlayerBySteamId(Buyer, out var BuyerIdentity))
                return;


            var BuyerBalance = MyBankingSystem.GetBalance(BuyerIdentity.Identity.IdentityId);
            if (BuyerBalance < Offer.Price)
            {
                //Yell shit at player for trying to cheat those bastards
                MyMultiplayer.Static.BanClient(Buyer, true);
                return;
            }

            if (Offer.ServerOffer)
                PurchaseServerGrid(Offer, Buyer, BuyerIdentity.Identity);
            else
                PurchasePlayerGrid(Offer, Buyer, BuyerIdentity.Identity, Owner);
        }


        private static void PurchasePlayerGrid(MarketListing Offer, ulong Buyer, MyIdentity BuyerIdentity, ulong Owner)
        {
            //Log.Error("A");


            if (!MySession.Static.Players.TryGetPlayerBySteamId(Owner, out var OwnerIdentity))
                return;

            //Have a successful buy
            RemoveMarketListing(Owner, Offer.Name);

            //Transfer grid
            if (!PlayerHangar.TransferGrid(Owner, Buyer, Offer.Name)) return;
            Chat.Send($"Successfully purchased {Offer.Name} from {OwnerIdentity.DisplayName}! Check your hangar!",
                Buyer);
            MyBankingSystem.ChangeBalance(BuyerIdentity.IdentityId, -1 * Offer.Price);
            MyBankingSystem.ChangeBalance(OwnerIdentity.Identity.IdentityId, Offer.Price);
        }

        private static void PurchaseServerGrid(MarketListing Offer, ulong Buyer, MyIdentity BuyerIdentity)
        {
            if (!File.Exists(Offer.FileSBCPath))
            {
                Log.Error($"{Offer.FileSBCPath} doesnt exsist! Was this removed prematurely?");
                return;
            }


            var ToInfo = new PlayerInfo();
            ToInfo.LoadFile(Hangar.MainPlayerDirectory, Buyer);

            //Log.Error("TotalBuys: " + ToInfo.GetServerOfferPurchaseCount(Offer.Name));
            if (Offer.TotalPerPlayer != 0 && ToInfo.GetServerOfferPurchaseCount(Offer.Name) >= Offer.TotalPerPlayer)
            {
                Chat.Send($"You have reached your buy limit for this offer!", Buyer);
                return;
            }


            var Stamp = new GridStamp(Offer.FileSBCPath)
            {
                GridName = Offer.Name
            };


            //Log.Error("C");
            if (PlayerHangar.TransferGrid(ToInfo, Stamp))
            {
                //Log.Error("Changing Balance");
                MyBankingSystem.ChangeBalance(BuyerIdentity.IdentityId, -1 * Offer.Price);

                Chat.Send($"Successfully purchased {Offer.Name}! Check your hangar!", Buyer);

                GridOfferBought(Offer, Buyer);

                //Hangar.Config.RefreshModel();
            }
        }

        private static string GetNameFormat(ulong Owner, string GridName)
        {
            if (Owner == 0)
                return "ServerOffer-" + GridName + ".json";
            else
                return Owner + "-" + GridName + ".json";
        }


        /* Following are for discord status messages */
        public static void NewGridOfferListed(MarketListing NewOffer)
        {
            if (!NexusSupport.RunningNexus)
                return;


            const string title = "Hangar Market - New Offer";


            var Msg = new StringBuilder();
            Msg.AppendLine($"GridName: {NewOffer.Name}");
            Msg.AppendLine($"Price: {NewOffer.Price}sc");
            Msg.AppendLine($"PCU: {NewOffer.PCU}");
            Msg.AppendLine($"Mass: {NewOffer.GridMass}kg");
            Msg.AppendLine($"Jump Distance: {NewOffer.JumpDistance}m");
            Msg.AppendLine($"Number Of Blocks: {NewOffer.NumberofBlocks}");
            Msg.AppendLine($"PowerOutput: {NewOffer.MaxPowerOutput / 1000}kW");
            Msg.AppendLine($"Built-Percent: {NewOffer.GridBuiltPercent * 100}%");
            Msg.AppendLine($"Total Grids: {NewOffer.NumberOfGrids}");
            Msg.AppendLine($"Static Grids: {NewOffer.StaticGrids}");
            Msg.AppendLine($"Large Grids: {NewOffer.LargeGrids}");
            Msg.AppendLine($"Small Grids: {NewOffer.SmallGrids}");


            Msg.AppendLine();
            Msg.AppendLine($"Description: {NewOffer.Description}");

            if (!MySession.Static.Players.TryGetPlayerBySteamId(NewOffer.SteamID, out var Player))
                return;

            var Fac = MySession.Static.Factions.GetPlayerFaction(Player.Identity.IdentityId);

            var Footer = Fac != null ? $"Seller: [{Fac.Tag}] {Player.DisplayName}" : $"Seller: {Player.DisplayName}";


            NexusAPI.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, title, Msg.ToString(), Footer,
                "#FFFF00");
        }

        public static void GirdOfferRemoved(MarketListing NewOffer)
        {
            if (!NexusSupport.RunningNexus)
                return;

            const string title = "Hangar Market - Offer Removed";


            if (!MySession.Static.Players.TryGetPlayerBySteamId(NewOffer.SteamID, out var Player))
                return;

            var Msg = new StringBuilder();
            Msg.AppendLine($"Grid {NewOffer.Name} is no longer for sale!");


            var Fac = MySession.Static.Factions.GetPlayerFaction(Player.Identity.IdentityId);

            var Footer = Fac != null ? $"Seller: [{Fac.Tag}] {Player.DisplayName}" : $"Seller: {Player.DisplayName}";


            NexusAPI.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, title, Msg.ToString(), Footer,
                "#FFFF00");
        }

        public static void GridOfferBought(MarketListing NewOffer, ulong Buyer)
        {
            if (!NexusSupport.RunningNexus)
                return;

            const string title = "Hangar Market - Offer Purchased";


            if (!MySession.Static.Players.TryGetPlayerBySteamId(NewOffer.SteamID, out var Seller))
                return;


            if (!MySession.Static.Players.TryGetPlayerBySteamId(NewOffer.SteamID, out var BuyerIdentity))
                return;


            var Fac = MySession.Static.Factions.GetPlayerFaction(Seller.Identity.IdentityId);
            var BuyerFac = MySession.Static.Factions.GetPlayerFaction(BuyerIdentity.Identity.IdentityId);

            var Footer = Fac != null ? $"Seller: [{Fac.Tag}] {Seller.DisplayName}" : $"Seller: {Seller.DisplayName}";


            var Msg = new StringBuilder();
            Msg.AppendLine(
                BuyerFac != null
                    ? $"Grid {NewOffer.Name} was purchased by [{BuyerFac.Tag}] {BuyerIdentity.DisplayName} for {NewOffer.Price}sc!"
                    : $"Grid {NewOffer.Name} was purchased by {BuyerIdentity.DisplayName} for {NewOffer.Price}sc!");


            NexusAPI.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, title, Msg.ToString(), Footer,
                "#FFFF00");
        }
    }
}