using Newtonsoft.Json;
using NLog;
using QuantumHangar.HangarChecks;
using QuantumHangar.Serialization;
using QuantumHangar.UI;
using QuantumHangar.Utilities;
using QuantumHangar.Utils;
using Sandbox;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;
using VRageMath;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;

namespace QuantumHangar.HangarMarket
{
    public class HangarMarketController
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        //files will by .json 

        private static Settings Config { get { return Hangar.Config; } }
        private static string MarketFolderDir;
        public static string PublicOffersDir;


        public static ConcurrentBag<MarketListing> Listings = new ConcurrentBag<MarketListing>();


        public static ConcurrentDictionary<string, MarketListing> MarketOffers = new ConcurrentDictionary<string, MarketListing>();

        public static Queue<string> NewFileQueue = new Queue<string>();

        public static Timer NewFileTimer = new Timer(500);



        // We use this to read new offers
        private FileSystemWatcher MarketWatcher;
        public static ClientCommunication Communication { get; private set; }

        private static MethodInfo SendNewProjection;


        public HangarMarketController()
        {
            //Run this when server initilizes
            MarketFolderDir = Path.Combine(Hangar.Config.FolderDirectory, "HangarMarket");
            PublicOffersDir = Path.Combine(MarketFolderDir, "PublicServerOffers");


            //Make sure to create the market directory
            Directory.CreateDirectory(MarketFolderDir);
            Directory.CreateDirectory(PublicOffersDir);



            //Initilize server and read all exsisting market files
            string[] MarketFileOffers = Directory.GetFiles(MarketFolderDir, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var OfferPath in MarketFileOffers)
            {
                string FileName = Path.GetFileName(OfferPath);
                // Log.Error($"Adding File: {FileName}");



                try
                {

                    GetReadMarketFile(OfferPath, out MarketListing Offer);
                    MarketOffers.TryAdd(FileName, Offer);
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    continue;
                }


            }


            NewFileTimer.Elapsed += NewFileTimer_Elapsed;
            NewFileTimer.Start();


            //Read all grids and get their objectbuilders serialized and ready...

            //Create fileSystemWatcher

            MarketWatcher = new FileSystemWatcher(MarketFolderDir);
            MarketWatcher.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.Security | NotifyFilters.FileName;




            MarketWatcher.Created += MarketWatcher_Created;
            MarketWatcher.Deleted += MarketWatcher_Deleted;
            MarketWatcher.Changed += MarketWatcher_Changed;
            MarketWatcher.Renamed += MarketWatcher_Renamed;


            MarketWatcher.IncludeSubdirectories = true;
            MarketWatcher.EnableRaisingEvents = true;





            SendNewProjection = typeof(MyProjectorBase).GetMethod("SendNewBlueprint", BindingFlags.NonPublic | BindingFlags.Instance);


        }

        private void NewFileTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            //Use this to periodically check the new file queue


            if (NewFileQueue.Count == 0)
                return;


            //Loop through queue and add new items
            while (NewFileQueue.Count != 0)
            {
                string FilePath = NewFileQueue.Peek();

                try
                {
                    GetReadMarketFile(FilePath, out MarketListing Offer);


                    if (MarketOffers.TryAdd(Path.GetFileName(FilePath), Offer))
                        //Log.Error("Added this file to the dictionary!");

                        NewFileQueue.Dequeue();
                }
                catch (System.IO.IOException)
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


        private void MarketWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            if (".json" != Path.GetExtension(e.Name))
                return;

            Log.Warn($"File {e.FullPath} renamed!");
        }

        private void MarketWatcher_Changed(object sender, FileSystemEventArgs e)
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

        private void MarketWatcher_Created(object sender, FileSystemEventArgs e)
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

            string Data = sr.ReadToEnd();



            Listing = JsonConvert.DeserializeObject<MarketListing>(File.ReadAllText(FilePath));


        }
        public static void RemoveMarketListing(ulong Owner, string Name)
        {
            string FileName = GetNameFormat(Owner, Name);
            MarketOffers.TryGetValue(FileName, out MarketListing Listing);

            if (Listing != null)
                GirdOfferRemoved(Listing);

            File.Delete(Path.Combine(MarketFolderDir, FileName));
        }
        public static bool SaveNewMarketFile(MarketListing NewListing)
        {
            //Saves a new market listing

            string FileName = GetNameFormat(NewListing.SteamID, NewListing.Name);

            try
            {
                //Save new market offer
                File.WriteAllText(Path.Combine(MarketFolderDir, FileName), JsonConvert.SerializeObject(NewListing, Formatting.Indented));
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

            string FileName = GetNameFormat(Owner, GridName);


            if (!MarketOffers.TryGetValue(FileName, out Offer))
                return false;

    

            //Check if file exsists
            if (!File.Exists(Path.Combine(MarketFolderDir, FileName)))
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
  
                string FolderPath;
                //Log.Error("3");
                FolderPath = Path.Combine(Hangar.Config.FolderDirectory, Owner.ToString());
                GridPath = Path.Combine(FolderPath, GridName + ".sbc");
            }


            //Confirm files exsits
            if (!File.Exists(GridPath))
            {

                RemoveMarketListing(Owner, GridName);
                return false;
            }

            return true;
        }
        public static void SetGridPreview(long EntityID, ulong Owner, string GridName)
        {

            if (!ValidGrid(Owner, GridName, out MarketListing Offer, out string GridPath))
                return;


            if (Offer.NumberofBlocks > 100000)
            {
                Log.Warn("MarketPreview Blocked. Grid is greater than 100000 blocks");
                return;
            }


            //Need async

            Log.Warn("Loading Grid");
            if (!GridSerializer.LoadGrid(GridPath, out IEnumerable<MyObjectBuilder_CubeGrid> GridBuilders))
            {
                RemoveMarketListing(Owner, GridName);
                return;
            }


            //Now attempt to load grid
            if (MyEntities.TryGetEntityById(EntityID, out MyEntity entity))
            {
                MyProjectorBase proj = entity as MyProjectorBase;
                if (proj != null)
                {


                    proj.SendRemoveProjection();
                    var Grids = GridBuilders.ToList();
                    Log.Warn("Setting projection!");
                    SendNewProjection.Invoke(proj, new object[] { Grids });
                }
            }

        }
        public static void PurchaseGridOffer(ulong Buyer, ulong Owner, string GridName)
        {

            Log.Error($"BuyerRequest: {Buyer}, Owner {Owner}, GridName {GridName}");

            if (!ValidGrid(Owner, GridName, out MarketListing Offer, out string GridPath))
                return;



            if (!MySession.Static.Players.TryGetIdentityFromSteamID(Buyer, out MyIdentity BuyerIdentity))
                return;


            long BuyerBalance = MyBankingSystem.GetBalance(BuyerIdentity.IdentityId);
            if (BuyerBalance < Offer.Price)
            {
                //Yell shit at player for trying to cheat those bastards
                MyMultiplayer.Static.BanClient(Buyer, true);
                return;
            }

            if (Offer.ServerOffer)
            {
                PurchaseServerGrid(Offer, Buyer, BuyerIdentity);
            }
            else
            {
                PurchasePlayerGrid(Offer, Buyer, BuyerIdentity, Owner);
            }

        }





        private static void PurchasePlayerGrid(MarketListing Offer, ulong Buyer, MyIdentity BuyerIdentity, ulong Owner)
        {
            //Log.Error("A");


            if (!MySession.Static.Players.TryGetIdentityFromSteamID(Owner, out MyIdentity OwnerIdentity))
                return;

            //Have a successfull buy
            RemoveMarketListing(Owner, Offer.Name);

            //Transfer grid
            if (PlayerHangar.TransferGrid(Owner, Buyer, Offer.Name))
            {
                Chat.Send($"Successfully purchased {Offer.Name} from {OwnerIdentity.DisplayName}! Check your hangar!", Buyer);
                MyBankingSystem.ChangeBalance(BuyerIdentity.IdentityId, -1 * Offer.Price);
                MyBankingSystem.ChangeBalance(OwnerIdentity.IdentityId, Offer.Price);
            }


        }
        private static void PurchaseServerGrid(MarketListing Offer, ulong Buyer, MyIdentity BuyerIdentity)
        {


            if (!File.Exists(Offer.FileSBCPath))
            {
                Log.Error($"{Offer.FileSBCPath} doesnt exsist! Was this removed prematurely?");
                return;
            }


            var ToInfo = new PlayerInfo();
            ToInfo.LoadFile(Config.FolderDirectory, Buyer);

            //Log.Error("TotalBuys: " + ToInfo.GetServerOfferPurchaseCount(Offer.Name));
            if (Offer.TotalPerPlayer != 0 && ToInfo.GetServerOfferPurchaseCount(Offer.Name) >= Offer.TotalPerPlayer)
            {
                Chat.Send($"You have reached your buy limit for this offer!", Buyer);
                return;
            }


            GridStamp Stamp = new GridStamp(Offer.FileSBCPath);
            Stamp.GridName = Offer.Name;




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

        private static string GetNameFormat(ulong Onwer, string GridName)
        {
            if (Onwer == 0)
            {
                return "ServerOffer-" + GridName + ".json";
            }
            else
            {
                return Onwer + "-" + GridName + ".json";
            }
        }





        /* Following are for discord status messages */
        public static void NewGridOfferListed(MarketListing NewOffer)
        {
            if (!NexusSupport.RunningNexus)
                return;


            string Title = "Hangar Market - New Offer";


            StringBuilder Msg = new StringBuilder();
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

            if (!MySession.Static.Players.TryGetIdentityFromSteamID(NewOffer.SteamID, out MyIdentity Player))
                return;

            var Fac = MySession.Static.Factions.GetPlayerFaction(Player.IdentityId);

            string Footer;

            if (Fac != null)
            {
                Footer = $"Seller: [{Fac.Tag}] {Player.DisplayName}";
            }
            else
            {
                Footer = $"Seller: {Player.DisplayName}";
            }


            NexusAPI.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, Title, Msg.ToString(), Footer, "#FFFF00");
        }
        public static void GirdOfferRemoved(MarketListing NewOffer)
        {
            if (!NexusSupport.RunningNexus)
                return;

            string Title = "Hangar Market - Offer Removed";


            if (!MySession.Static.Players.TryGetIdentityFromSteamID(NewOffer.SteamID, out MyIdentity Player))
                return;

            StringBuilder Msg = new StringBuilder();
            Msg.AppendLine($"Grid {NewOffer.Name} is no longer for sale!");


            var Fac = MySession.Static.Factions.GetPlayerFaction(Player.IdentityId);

            string Footer;

            if (Fac != null)
            {
                Footer = $"Seller: [{Fac.Tag}] {Player.DisplayName}";
            }
            else
            {
                Footer = $"Seller: {Player.DisplayName}";
            }


            NexusAPI.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, Title, Msg.ToString(), Footer, "#FFFF00");
        }

        public static void GridOfferBought(MarketListing NewOffer, ulong Buyer)
        {

            if (!NexusSupport.RunningNexus)
                return;

            string Title = "Hangar Market - Offer Purchased";



            if (!MySession.Static.Players.TryGetIdentityFromSteamID(NewOffer.SteamID, out MyIdentity Seller))
                return;


            if (!MySession.Static.Players.TryGetIdentityFromSteamID(NewOffer.SteamID, out MyIdentity BuyerIdentity))
                return;





            var Fac = MySession.Static.Factions.GetPlayerFaction(Seller.IdentityId);
            var BuyerFac = MySession.Static.Factions.GetPlayerFaction(BuyerIdentity.IdentityId);
            string Footer;

            if (Fac != null)
            {
                Footer = $"Seller: [{Fac.Tag}] {Seller.DisplayName}";
            }
            else
            {
                Footer = $"Seller: {Seller.DisplayName}";
            }



            StringBuilder Msg = new StringBuilder();
            if (BuyerFac != null)
            {
                Msg.AppendLine($"Grid {NewOffer.Name} was purchased by [{BuyerFac.Tag}] {BuyerIdentity.DisplayName} for {NewOffer.Price}sc!");
            }
            else
            {
                Msg.AppendLine($"Grid {NewOffer.Name} was purchased by {BuyerIdentity.DisplayName} for {NewOffer.Price}sc!");
            }


            NexusAPI.SendEmbedMessageToDiscord(Hangar.Config.MarketUpdateChannel, Title, Msg.ToString(), Footer, "#FFFF00");
        }

    }

}
