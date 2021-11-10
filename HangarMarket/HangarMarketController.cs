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


        private static string MarketFolderDir;
        public static string PublicOffersDir;


        public static ConcurrentBag<MarketListing> Listings = new ConcurrentBag<MarketListing>();


        public static ConcurrentDictionary<string, MarketListing> MarketOffers = new ConcurrentDictionary<string, MarketListing>();

        public static Queue<string> NewFileQueue = new Queue<string>();

        public static Timer NewFileTimer = new Timer(500);



        // We use this to read new offers
        private FileSystemWatcher MarketWatcher;
        private ClientCommunication Communication;

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
                Log.Error($"Adding File: {FileName}");



                try
                {

                    GetReadMarketFile(OfferPath, out MarketListing Offer);
                    MarketOffers.TryAdd(FileName, Offer);
                }
                catch(Exception ex)
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
                        Log.Error("Added this file to the dictionary!");

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
            Log.Warn($"File {e.FullPath} renamed!");
        }

        private void MarketWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //Market offer changed
            if (e.ChangeType != WatcherChangeTypes.Changed)
                return;

            Log.Warn($"File {e.FullPath} changed!");

        }

        private void MarketWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            //Market offer deleted

            //Log.Warn($"File {e.FullPath} deleted! {e.Name}");

            if (MarketOffers.TryRemove(e.Name, out _))
                Log.Error("Removed this file from the dictionary!");


            //Send new offer update to all clients
            Communication.UpdateAllOffers();

        }

        private void MarketWatcher_Created(object sender, FileSystemEventArgs e)
        {
            //New market offer created
            //Log.Warn($"File {e.FullPath} created! {e.Name}");



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


          


            if (Owner == 0)
            {
                //This is for if its a server offer

                Offer = Hangar.Config.PublicMarketOffers.FirstOrDefault(x => x.Name == GridName);
                if (Offer == null)
                    return false;


                if (!File.Exists(Offer.FileSBCPath) || Offer.ForSale == false)
                {
                    var Remove = Offer;
                    UserControlInterface.Thread.Invoke(() => { Hangar.Config.PublicMarketOffers.Remove(Remove); });
                    return false;
                }

                Offer.ServerOffer = true;
                GridPath = Offer.FileSBCPath;

            }
            else
            {

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


                //Log.Error("3");
                var FolderPath = Path.Combine(Hangar.Config.FolderDirectory, Owner.ToString());
                GridPath = Path.Combine(FolderPath, GridName + ".sbc");

                //Confirm files exsits
                if (!Directory.Exists(FolderPath) || !File.Exists(GridPath))
                {
                    RemoveMarketListing(Owner, GridName);
                    return false;
                }
            }




            //Log.Error("4");

            return true;
        }


        public static void SetGridPreview(long EntityID, ulong Owner, string GridName)
        {

            if (!ValidGrid(Owner, GridName, out MarketListing Offer, out string GridPath))
                return;


            if(Offer.NumberofBlocks > 100000)
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

            Log.Error($"Buyer {Buyer}, Owner {Owner}, GridName {GridName}");

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

            //Log.Error("B");

           

            //Have a successfull buy
            RemoveMarketListing(Owner, Offer.Name);

            //Transfer grid
            if (PlayerHangar.TransferGrid(Owner, Buyer, Offer.Name))
            {
                MyBankingSystem.ChangeBalance(BuyerIdentity.IdentityId, -1 * Offer.Price);
                MyBankingSystem.ChangeBalance(OwnerIdentity.IdentityId, Offer.Price);
            }


        }
        private static void PurchaseServerGrid(MarketListing Offer, ulong Buyer, MyIdentity BuyerIdentity)
        {
            //Cannot buy if its over max amount
            if (Offer.TotalAmount != 0 && Offer.TotalBuys > Offer.TotalAmount)
            {
                Offer.ForSale = false;
                return;
            }

            //Log.Error("A");

            int Index = Offer.PlayerPurchases.FindIndex(x => x.Key == Buyer);

            if (Offer.TotalPerPlayer != 0 && Index != -1 && Offer.PlayerPurchases[Index].Value >= Offer.TotalPerPlayer)
            {
                //Log.Error("B");
                //Player doesnt have any buys left
                return;
            }

            //Log.Error("C");
            if (PlayerHangar.TransferGrid(Buyer, Offer.FileSBCPath, Offer.Name))
            {

                //Log.Error("Changing Balance");
                MyBankingSystem.ChangeBalance(BuyerIdentity.IdentityId, -1 * Offer.Price);
                
                if(Index == -1)
                {
                    Offer.PlayerPurchases.Add(new KeyValuePair<ulong, int>(Buyer, 1));
                }
                else
                {

                    Offer.PlayerPurchases[Index] = new KeyValuePair<ulong, int>(Buyer, Offer.PlayerPurchases[Index].Value + 1);
                }

                //Hangar.Config.RefreshModel();
            }
        }


        private static string GetNameFormat(ulong Onwer, string GridName)
        {
            if(Onwer == 0)
            {
                return "ServerOffer-" + GridName;
            }
            else
            {
                return Onwer + "-" + GridName + ".json";
            }
        }
    }

}
