using Newtonsoft.Json;
using NLog;
using NLog.Fluent;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantumHangar.Utilities
{
    /*
     * This will handle the backup and restore/deletion depending on server save.
     * 
     * 
     * Part A:
     * For checking if a grid got sent to hangar, then server crashed and got rolled back to where grid exists:
     *  We will have a hangar save queue PER server. This will be cleared on server save success.
     *  On success, we will clear the save queue.
     *  
     *  Once server loads after crash, we will read the non-empty save queue and go through and delete those grids from peoples hangars
     *  
     *  If a grid gets loaded after save and before crash, we delete the grid from the delete queue. (grid would no longer be in their hangar)
     *  
     *  
     *  Part B:
     * For checking if a grid got pasted in, then server rolled back, then grid doesnt exist:
     *      -We will have to roll back players hangars. and their grids. 
     *      -Do this by having a hangar delete queue. Everytime a grid gets loaded, we send the gridstamp to player backups in their info file.
     *      -SBC file wont be deleted until server save success
     *      
     *      -Once a grid gets saved, we have to check if that grid is in the hangar delete queue, and remove it
     *
     * 
     * 
     * 
     * 
     * 
     */


    public class GridTracker
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private string StoragePath { get; set; }

        private string HangarFolderDir;

        private GridTrackerObject Grids { get; set; }

        public void ServerStarted(string HangarDirectory)
        {
            return;

            HangarFolderDir = HangarDirectory;

            Log.Warn("Starting AutoGridBackup System!");
            //Log.Warn(MySession.Static.CurrentPath);

            Directory.CreateDirectory(Path.Combine(MySession.Static.CurrentPath, "Storage"));
            StoragePath = Path.Combine(MySession.Static.CurrentPath, "Storage", "QuantumHangarGridTracker.json");

            LoadFile(out GridTrackerObject Data);
            Grids = Data;

            InitilizeReadSystem();
        }

        private void InitilizeReadSystem()
        {
            //True = SavedToFile
            //False = LoadedIntoServer


            foreach (var item in Grids.TrackedGrids)
            {
                GridMethods methods = new GridMethods(item.Key, HangarFolderDir);
                methods.LoadInfoFile(out PlayerInfo Data);


                List<int> GridsToBeRemoved = new List<int>();
                foreach (var StampKey in item.Value)
                {

                    if (StampKey.Key)
                    {
                        //If true, that mean the grid was saved to hangar before server saved. 
                        //Means that there is a duplicate grid in the server


                        //To fix this, we will remove this grid from the players file to revert back to the version that is still ingame
                        int? i = Data.Grids.FindIndex(x => x.GridName.Equals(StampKey.Value.GridName));
                        if (!i.HasValue || !Data.Grids.IsValidIndex(i.Value))
                            continue;

                        //Now that weve got the index,
                        Log.Warn("Grid needs to be removed: " + Data.Grids[i.Value].GridName);
                        string GridPath = Path.Combine(methods.FolderPath, Data.Grids[i.Value].GridName + ".bak");


                        File.Delete(GridPath);
                        GridsToBeRemoved.Add(i.Value);
                        

                    }
                    else
                    {
                        //If false, this means the grid was loaded into the world before server saved.
                        //Means the grid is gone on the user side

                        //To fix this, we wont delete grids in peoples hangars until server saves.
                        //This will allow us to simply add this stamp to the players info file (the sbc has to be renamed from the .bak created on load)
                        Data.Grids.Add(StampKey.Value);
                        string GridBakPath = Path.Combine(methods.FolderPath, StampKey.Value.GridName + ".bak");
                        string GridPath = Path.Combine(methods.FolderPath, StampKey.Value.GridName + ".sbc");
                        Log.Warn($"Restored grid that was not saved after being unhangared: {StampKey.Value.GridName}");
                        //File.Move(GridBakPath, GridPath);
                    }

                }

                //Remove grids
                foreach (int indice in GridsToBeRemoved.OrderByDescending(v => v))
                {
                    Data.Grids.RemoveAt(indice);
                }

                //Save the new files
                methods.SaveInfoFile(Data);
            }


            Grids.TrackedGrids.Clear();
            SaveFile(Grids);
        }

        public void ServerSave()
        {
            try
            {

                foreach (var item in Grids.TrackedGrids)
                {
                    GridMethods methods = new GridMethods(item.Key, HangarFolderDir);
                    if (methods.LoadInfoFile(out PlayerInfo Data))
                    {
                        foreach (var StampKey in item.Value)
                        {
                            if (!StampKey.Key)
                            {
                                //This means that we successfully saved the server with the grid loaded into the server
                                //Need to delete the file now
                                Log.Warn("Server Successfully saved with " + StampKey.Value.GridName + " owned by "  + item.Key + " loaded in server! Deleting bak!");
                                string GridPath = Path.Combine(methods.FolderPath, StampKey.Value.GridName + ".bak");
                                File.Delete(GridPath);
                            }
                            else
                            {
                                Log.Warn("Server succesfully saved with grid " + StampKey.Value.GridName + " owned by " + item.Key + " in hangar! Clearing ID!");
                                int? Grid = Data.Grids.FindIndex(x => x.GridName.Equals(StampKey.Value.GridName));
                                if (Grid.HasValue && Data.Grids.IsValidIndex(Grid.Value))
                                {
                                    //This grid no longer esists?
                                    Data.Grids[Grid.Value].ServerPort = 0;
                                }
                            }

                        }

                        methods.SaveInfoFile(Data);
                    }

                }

                Log.Warn("Deleted Hangar Files via GridTracker!");

                Grids.TrackedGrids.Clear();
                SaveFile(Grids);
            }catch(Exception ex)
            {
                Log.Fatal(ex);
            }
        }

        public void HangarUpdate(ulong SteamID, bool Saving, GridStamp Stamp)
        {
            //Need to check if this item already exists

            return;

            KeyValuePair<bool, GridStamp> KPair = new KeyValuePair<bool, GridStamp>(Saving, Stamp);
            if (Grids.TrackedGrids.ContainsKey(SteamID))
            {
  
                Log.Warn("Current GRIDID: " + Stamp.GridID);
                int? Index = Grids.TrackedGrids[SteamID].FindIndex(x => x.Value.GridID == KPair.Value.GridID);
               // Log.Warn("Attempting to Updated GridTracker A! " + KPair.Value.GridName);
                if (Index.HasValue && Index != -1)
                {
                    Log.Warn("Index: " + Index.Value);
                    Grids.TrackedGrids[SteamID].RemoveAt(Index.Value);
                    Log.Warn("Found exsisting gridID in GridTracker! Removing");

                }
                Grids.TrackedGrids[SteamID].Add(KPair);
            }
            else
            {
                //Log.Warn("Attempting to Updated GridTracker B! " + KPair.Value.GridName);
                List<KeyValuePair<bool, GridStamp>> List = new List<KeyValuePair<bool, GridStamp>>();
                List.Add(KPair);
                Grids.TrackedGrids.Add(SteamID, List);
            }


            //We really only care to change it when a player loads a grid
            if (!Saving)
            {
                //If the grid is saving... We need to convert to .bak
                string PlayersFolder = Path.Combine(HangarFolderDir, SteamID.ToString());
                string GridPath = Path.Combine(PlayersFolder, Stamp.GridName + ".sbc");

                if (File.Exists(GridPath))
                {
                    string BackupGridPath = Path.Combine(PlayersFolder, Stamp.GridName + Stamp.GridID+ ".bak");
                    FileInfo F = new FileInfo(GridPath);
                    F.MoveTo(BackupGridPath);

                    //We need to make sure old one gets removed (TheSBC)
                }
            }




            SaveFile(Grids);

        }

        private void SaveFile(GridTrackerObject Data)
        {
            //Log.Warn(StoragePath);
            FileSaver.Save(StoragePath, Data);
        }

        private bool LoadFile(out GridTrackerObject Data)
        {
            GridTrackerObject Info = new GridTrackerObject();


            if (!File.Exists(StoragePath))
            {
                Data = Info;
                return true;
            }


            try
            {
                Info = JsonConvert.DeserializeObject<GridTrackerObject>(File.ReadAllText(StoragePath));
            }
            catch (Exception e)
            {
                Log.Warn(e, "For some reason the file is broken");
                Data = Info;
                return false;
            }


            Data = Info;
            return true;
        }


    }


    class GridTrackerObject
    {
        public Dictionary<ulong, List<KeyValuePair<bool, GridStamp>>> TrackedGrids = new Dictionary<ulong, List<KeyValuePair<bool, GridStamp>>>();






    }
}
