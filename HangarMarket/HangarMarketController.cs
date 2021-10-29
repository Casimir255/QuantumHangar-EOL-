using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantumHangar.HangarMarket
{
    public class HangarMarketController
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        //files will by .json 


        private static string MarketFolderDir;




        public static ConcurrentDictionary<string, MarketListing> MarketOffers = new ConcurrentDictionary<string, MarketListing>();




        // We use this to read new offers
        private FileSystemWatcher MarketWatcher;





        public HangarMarketController()
        {
            //Run this when server initilizes
            MarketFolderDir = Path.Combine(Hangar.Config.FolderDirectory, "HangarMarket");


            //Make sure to create the market directory
            Directory.CreateDirectory(MarketFolderDir);


            //Initilize server and read all exsisting market files
            string[] MarketFileOffers = Directory.GetFiles(MarketFolderDir, "*.json", SearchOption.TopDirectoryOnly);
            foreach(var OfferPath in MarketFileOffers)
            {
                string FileName = Path.GetFileName(OfferPath);
                Log.Error($"Adding File: {FileName}");


                if (!TryGetReadMarketFile(OfferPath, out MarketListing Offer))
                    continue;

                MarketOffers.TryAdd(FileName, Offer);
            }


          



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



        }

        private void MarketWatcher_Created(object sender, FileSystemEventArgs e)
        {
            //New market offer created
            //Log.Warn($"File {e.FullPath} created! {e.Name}");

            

            byte[] Data = File.ReadAllBytes(e.FullPath);


            if (!TryGetReadMarketFile(e.FullPath, out MarketListing Offer))
                return;




            if (MarketOffers.TryAdd(e.Name, Offer))
                Log.Error("Added this file to the dictionary!");

        }





      
        private static bool TryGetReadMarketFile(string FilePath, out MarketListing Listing)
        {
            //Reads market file from path

            Listing = null;
            try
            {
                Listing = JsonConvert.DeserializeObject<MarketListing>(File.ReadAllText(FilePath));
                return true;

            }
            catch(Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }




        public static void RemoveMarketListing(ulong Owner, string Name)
        {
            string FileName = Owner + "-" + Name + ".json";

            File.Delete(Path.Combine(MarketFolderDir, FileName));
        }


        public static bool SaveNewMarketFile(MarketListing NewListing)
        {
            //Saves a new market listing

            string FileName = NewListing.SteamID + "-" + NewListing.Name + ".json";

            try
            {
                //Save new market offer
                File.WriteAllText(Path.Combine(MarketFolderDir,FileName), JsonConvert.SerializeObject(NewListing, Formatting.Indented));
                return true;

            }catch(Exception ex)
            {
                Log.Error(ex);
                return false;
            }
        }
    }
}
