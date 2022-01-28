using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace QuantumHangar.Serialization
{
    public static class JsonSerializer
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private static Timer FileSaveChecker = new Timer(100);
        private static bool FileSaveCheckReady = true;


        public class FileData
        {
            public string Path;
            public object Data;

            public FileData(string Path, object Data)
            {
                this.Path = Path;
                this.Data = Data;
            }

        }

        public static ConcurrentQueue<FileData> SaveQueue = new ConcurrentQueue<FileData>();



        public static void InitFileSaver()
        {
            FileSaveChecker.Elapsed += FileSaveChecker_Elapsed;
        }

        private static void FileSaveChecker_Elapsed(object sender, ElapsedEventArgs e)
        {
            //Skip if the previous check didnt save. (Other one is still inprogress)
            if (!FileSaveCheckReady)
                return;


            FileSaveCheckReady = false;
            while (!SaveQueue.IsEmpty)
            {
                if (SaveQueue.TryDequeue(out FileData Result))
                {
                    SaveData(Result);
                }
                else
                {
                    Log.Fatal("Unable to Dequeue FileData from SaveQueue! Players file has been skipped!");
                }
            }
            FileSaveCheckReady = true;
        }

        private static void SaveData(FileData Data)
        {
            try
            {
                File.WriteAllText(Data.Path, JsonConvert.SerializeObject(Data.Data, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to save file @" + Data.Path);
            }
        }

        public static void QueueSave(string Path, object Data)
        {
            SaveQueue.Append(new FileData(Path, Data));
        }

        public static bool GetData<T>(string Path, out T Data)
        {
            Data = default(T);

            //Check if there are any files in the save queue. If not we good!
            if (!SaveQueue.IsEmpty)
            {
                if(SaveQueue.Any(x=> x.Path == Path))
                {
                    return false;
                }
            }


            try
            {
                Data = JsonConvert.DeserializeObject<T>(File.ReadAllText(Path));
                return true;
            }
            catch(Exception Ex)
            {
                Log.Error(Ex, "Unable to read file @" + Path);
                return false;
            }


        }


    }
}
