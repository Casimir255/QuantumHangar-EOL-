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


        private static string MarketFolderDir { get { return Path.Combine(Hangar.Config.FolderDirectory, "HangarMarket"); } }
        private static string MarketOfferFiles { get { return Path.Combine(Hangar.Config.FolderDirectory, "Offers"); } }
        private static string MarketGridBuilders { get { return Path.Combine(Hangar.Config.FolderDirectory, "Grids"); } }



        private static ConcurrentDictionary<string, byte[]> MarketOffers = new ConcurrentDictionary<string, byte[]>();




        // We use this to read new offers
        private FileSystemWatcher MarketWatcher;





        public HangarMarketController()
        {

            //Make sure to create the market directory
            Directory.CreateDirectory(MarketOfferFiles);
            Directory.CreateDirectory(MarketGridBuilders);


            string[] MarketFileOffers = Directory.GetFiles(MarketFolderDir, "*.txt", SearchOption.TopDirectoryOnly);

            foreach(var Offer in MarketFileOffers)
            {
                string FileName = Path.GetFileName(Offer);
                Log.Error($"Adding File: {FileName}");
                MarketOffers.TryAdd(FileName, File.ReadAllBytes(Offer));
            }


            //Run this when server initilizes



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
            

            if (MarketOffers.TryAdd(e.Name, Data))
                Log.Error("Added this file to the dictionary!");

        }




        private static void ReadMarketFile(string FilePath)
        {







        }
    }
}
