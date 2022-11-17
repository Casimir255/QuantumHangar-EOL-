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
            var marketFileOffers = Directory.GetFiles(_marketFolderDir, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var offerPath in marketFileOffers)
            {
                var fileName = Path.GetFileName(offerPath);
                // Log.Error($"Adding File: {FileName}");


                try
                {
                    GetReadMarketFile(offerPath, out var offer);
                    MarketOffers.TryAdd(fileName, offer);
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
                var filePath = NewFileQueue.Peek();

                try
                {
                    GetReadMarketFile(filePath, out var offer);


                    if (MarketOffers.TryAdd(Path.GetFileName(filePath), offer))
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
                    File.Delete(filePath);
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
            Communication?.Close();
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


        private static void GetReadMarketFile(string filePath, out MarketListing listing)
        {
            //Reads market file from path

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var sr = new StreamReader(fs, Encoding.UTF8);

            var data = sr.ReadToEnd();


            listing = JsonConvert.DeserializeObject<MarketListing>(File.ReadAllText(filePath));
        }

        public static void RemoveMarketListing(ulong owner, string name)
        {
            var fileName = GetNameFormat(owner, name);
            MarketOffers.TryGetValue(fileName, out var listing);

            if (listing != null)
                GirdOfferRemoved(listing);

            File.Delete(Path.Combine(_marketFolderDir, fileName));
        }

        public static bool SaveNewMarketFile(MarketListing newListing)
        {
            //Saves a new market listing

            var fileName = GetNameFormat(newListing.SteamId, newListing.Name);

            try
            {
                //Save new market offer
                File.WriteAllText(Path.Combine(_marketFolderDir, fileName),
                    JsonConvert.SerializeObject(newListing, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }


        private static bool ValidGrid(ulong owner, string gridName, out MarketListing offer, out string gridPath)
        {
            gridPath = string.Empty;

            var fileName = GetNameFormat(owner, gridName);


            if (!MarketOffers.TryGetValue(fileName, out offer))
                return false;


            //Check if file exists
            if (!File.Exists(Path.Combine(_marketFolderDir, fileName)))
            {
                //Someone this happened?
                MarketOffers.TryRemove(fileName, out _);
                return false;
            }


            if (offer.ServerOffer)
            {
                gridPath = offer.FileSbcPath;
            }
            else
            {
                var folderPath = Path.Combine(Hangar.MainPlayerDirectory, owner.ToString());
                gridPath = Path.Combine(folderPath, gridName + ".sbc");
            }


            //Confirm files exits
            if (File.Exists(gridPath)) return true;
            RemoveMarketListing(owner, gridName);
            return false;

        }

        public static void SetGridPreview(long entityId, ulong owner, string gridName)
        {
            if (!ValidGrid(owner, gridName, out var offer, out var gridPath))
                return;


            if (offer.NumberofBlocks > 100000)
            {
                Log.Warn("MarketPreview Blocked. Grid is greater than 100000 blocks");
                return;
            }


            //Need async

            Log.Warn("Loading Grid");
            if (!GridSerializer.LoadGrid(gridPath, out var gridBuilders))
            {
                RemoveMarketListing(owner, gridName);
                return;
            }


            //Now attempt to load grid
            if (!MyEntities.TryGetEntityById(entityId, out var entity)) return;
            if (!(entity is MyProjectorBase proj)) return;
            proj.SendRemoveProjection();
            var grids = gridBuilders.ToList();
            Log.Warn("Setting projection!");
            _sendNewProjection.Invoke(proj, new object[] { grids });
        }

        public static void PurchaseGridOffer(ulong buyer, ulong owner, string gridName)
        {
            Log.Error($"BuyerRequest: {buyer}, Owner {owner}, GridName {gridName}");

            if (!ValidGrid(owner, gridName, out var offer, out var gridPath))
                return;


            if (!MySession.Static.Players.TryGetPlayerBySteamId(buyer, out var buyerIdentity))
                return;


            var buyerBalance = MyBankingSystem.GetBalance(buyerIdentity.Identity.IdentityId);
            if (buyerBalance < offer.Price)
            {
                //Yell shit at player for trying to cheat those bastards
                MyMultiplayer.Static.BanClient(buyer, true);
                return;
            }

            if (offer.ServerOffer)
                PurchaseServerGrid(offer, buyer, buyerIdentity.Identity);
            else
                PurchasePlayerGrid(offer, buyer, buyerIdentity.Identity, owner);
        }


        private static void PurchasePlayerGrid(MarketListing offer, ulong buyer, MyIdentity buyerIdentity, ulong owner)
        {
            //Log.Error("A");


            if (!MySession.Static.Players.TryGetPlayerBySteamId(owner, out var ownerIdentity))
                return;

            //Have a successful buy
            RemoveMarketListing(owner, offer.Name);

            //Transfer grid
            if (!PlayerHangar.TransferGrid(owner, buyer, offer.Name)) return;
            Chat.Send($"Successfully purchased {offer.Name} from {ownerIdentity.DisplayName}! Check your hangar!",
                buyer);
            MyBankingSystem.ChangeBalance(buyerIdentity.IdentityId, -1 * offer.Price);
            MyBankingSystem.ChangeBalance(ownerIdentity.Identity.IdentityId, offer.Price);
        }

        private static void PurchaseServerGrid(MarketListing offer, ulong buyer, MyIdentity buyerIdentity)
        {
            if (!File.Exists(offer.FileSbcPath))
            {
                Log.Error($"{offer.FileSbcPath} doesnt exsist! Was this removed prematurely?");
                return;
            }


            var toInfo = new PlayerInfo();
            toInfo.LoadFile(Hangar.MainPlayerDirectory, buyer);

            //Log.Error("TotalBuys: " + ToInfo.GetServerOfferPurchaseCount(Offer.Name));
            if (offer.TotalPerPlayer != 0 && toInfo.GetServerOfferPurchaseCount(offer.Name) >= offer.TotalPerPlayer)
            {
                Chat.Send($"You have reached your buy limit for this offer!", buyer);
                return;
            }


            var stamp = new GridStamp(offer.FileSbcPath)
            {
                GridName = offer.Name
            };


            //Log.Error("C");
            if (PlayerHangar.TransferGrid(toInfo, stamp))
            {
                //Log.Error("Changing Balance");
                MyBankingSystem.ChangeBalance(buyerIdentity.IdentityId, -1 * offer.Price);

                Chat.Send($"Successfully purchased {offer.Name}! Check your hangar!", buyer);

                GridOfferBought(offer, buyer);

                //Hangar.Config.RefreshModel();
            }
        }

        private static string GetNameFormat(ulong owner, string gridName)
        {
            if (owner == 0)
                return "ServerOffer-" + gridName + ".json";
            else
                return owner + "-" + gridName + ".json";
        }


        /* Following are for discord status messages */
        public static void NewGridOfferListed(MarketListing newOffer)
        {
            if (!NexusSupport.RunningNexus)
                return;


            const string title = "Hangar Market - New Offer";


            var msg = new StringBuilder();
            msg.AppendLine($"GridName: {newOffer.Name}");
            msg.AppendLine($"Price: {newOffer.Price}sc");
            msg.AppendLine($"PCU: {newOffer.Pcu}");
            msg.AppendLine($"Mass: {newOffer.GridMass}kg");
            msg.AppendLine($"Jump Distance: {newOffer.JumpDistance}m");
            msg.AppendLine($"Number Of Blocks: {newOffer.NumberofBlocks}");
            msg.AppendLine($"PowerOutput: {newOffer.MaxPowerOutput / 1000}kW");
            msg.AppendLine($"Built-Percent: {newOffer.GridBuiltPercent * 100}%");
            msg.AppendLine($"Total Grids: {newOffer.NumberOfGrids}");
            msg.AppendLine($"Static Grids: {newOffer.StaticGrids}");
            msg.AppendLine($"Large Grids: {newOffer.LargeGrids}");
            msg.AppendLine($"Small Grids: {newOffer.SmallGrids}");


            msg.AppendLine();
            msg.AppendLine($"Description: {newOffer.Description}");

            if (!MySession.Static.Players.TryGetPlayerBySteamId(newOffer.SteamId, out var player))
                return;

            var fac = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);

            var footer = fac != null ? $"Seller: [{fac.Tag}] {player.DisplayName}" : $"Seller: {player.DisplayName}";


            NexusApi.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, title, msg.ToString(), footer,
                "#FFFF00");
        }

        public static void GirdOfferRemoved(MarketListing newOffer)
        {
            if (!NexusSupport.RunningNexus)
                return;

            const string title = "Hangar Market - Offer Removed";


            if (!MySession.Static.Players.TryGetPlayerBySteamId(newOffer.SteamId, out var player))
                return;

            var msg = new StringBuilder();
            msg.AppendLine($"Grid {newOffer.Name} is no longer for sale!");


            var fac = MySession.Static.Factions.GetPlayerFaction(player.Identity.IdentityId);

            var footer = fac != null ? $"Seller: [{fac.Tag}] {player.DisplayName}" : $"Seller: {player.DisplayName}";


            NexusApi.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, title, msg.ToString(), footer,
                "#FFFF00");
        }

        public static void GridOfferBought(MarketListing newOffer, ulong buyer)
        {
            if (!NexusSupport.RunningNexus)
                return;

            const string title = "Hangar Market - Offer Purchased";


            if (!MySession.Static.Players.TryGetPlayerBySteamId(newOffer.SteamId, out var seller))
                return;


            if (!MySession.Static.Players.TryGetPlayerBySteamId(newOffer.SteamId, out var buyerIdentity))
                return;


            var fac = MySession.Static.Factions.GetPlayerFaction(seller.Identity.IdentityId);
            var buyerFac = MySession.Static.Factions.GetPlayerFaction(buyerIdentity.Identity.IdentityId);

            var footer = fac != null ? $"Seller: [{fac.Tag}] {seller.DisplayName}" : $"Seller: {seller.DisplayName}";


            var msg = new StringBuilder();
            msg.AppendLine(
                buyerFac != null
                    ? $"Grid {newOffer.Name} was purchased by [{buyerFac.Tag}] {buyerIdentity.DisplayName} for {newOffer.Price}sc!"
                    : $"Grid {newOffer.Name} was purchased by {buyerIdentity.DisplayName} for {newOffer.Price}sc!");


            NexusApi.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, title, msg.ToString(), footer,
                "#FFFF00");
        }
    }
}