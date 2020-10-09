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
using Torch.Managers.PatchManager;
using Sandbox.Game.GameSystems.BankingAndCurrency;
using VRageMath;

namespace QuantumHangar
{
    public class Hangar : TorchPluginBase, IWpfPlugin
    {
        private static readonly Logger Log = LogManager.GetLogger("QuantumHangar");


        public Settings Config => _config?.Data;
        public static Persistent<Settings> _config;


        public Dictionary<long, CurrentCooldown> ConfirmationsMap { get; } = new Dictionary<long, CurrentCooldown>();
        public static string Dir;
        public static MultiplayerManagerBase MP;
        public static TorchSessionManager TorchSession;

        private static bool EnableDebug = true;
        public static bool IsRunning = false;
        public static bool IsEnabled = true;

        private bool ServerRunning;
        public static MethodInfo CheckFuture;
        public ITorchPlugin GridBackup;

        public GridMarket Market;

        //Used to compare times
        public DateTime AutoHangarStamp;
        public DateTime AutoVoxelStamp;

        private int TickCounter = 0;
     


        public enum ErrorType
        {
            Debug,
            Fatal,
            Trace,
            Warn
        }


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





            if (Config.GridMarketEnabled)
            {
                Market = new GridMarket(StoragePath);
                Market.InitilizeGridMarket();
            }
            else
            {
                Debug("Starting plugin WITHOUT the Hangar Market!", null, ErrorType.Warn);
            }


            try
            {

            }
            catch (Exception e)
            {
                Log.Info("Unable to load grid market files! " + e);
            }

            EnableDebug = Config.AdvancedDebug;
            Dir = Config.FolderDirectory;


            PatchManager manager = DependencyProviderExtensions.GetManager<PatchManager>(Torch.Managers);
            Patcher patcher = new Patcher();
            patcher.Apply(manager.AcquireContext(), this);
            //Load files
        }



        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            ServerRunning = state == TorchSessionState.Loaded;
            switch (state)
            {
                case TorchSessionState.Loaded:
                    IsRunning = true;

                    

                    MP = Torch.CurrentSession.Managers.GetManager<MultiplayerManagerBase>();
                    ChatManager = Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
                    PluginManager Plugins = Torch.CurrentSession.Managers.GetManager<PluginManager>();


                    BlockLimiterConnection(Plugins);
                    GridBackupConnection(Plugins);

                    if (Config.GridMarketEnabled)
                    {
                        Market.InitilizeComms(ChatManager, MP);
                    }

                    AutoHangarStamp = DateTime.Now;
                    break;


                case TorchSessionState.Unloading:
                    PluginDispose();
                    break;


            }
        }



        public override void Update()
        {
            //Optional how often to check
            if (TickCounter > 1000)
            {
                if (Config.PluginEnabled && AutoHangarStamp.AddMinutes(30) < DateTime.Now)
                {
                    //Run checks

                    if (Config.AutoHangarGrids)
                    {
                        AutoHangar Auto = new AutoHangar(this);
                        Auto.RunAutoHangar();
                    }


                    if (Config.GridMarketEnabled && Config.AutosellHangarGrids && Market.IsHostServer)
                    {
                        AutoHangar Auto = new AutoHangar(this, Market);
                        Auto.RunAutoSell();
                    }

                    AutoHangarStamp = DateTime.Now;
                }

                if (Config.PluginEnabled && AutoVoxelStamp.AddMinutes(2.5) < DateTime.Now && Config.HangarGridsFallenInPlanet)
                {
                    Debug("Getting grids in voxels!!");
                    AutoHangar Auto = new AutoHangar(this);

                    Auto.RunAutoHangarUnderPlanet();
                    AutoVoxelStamp = DateTime.Now;
                }

                TickCounter = 0;
            }
            TickCounter++;
        }

        private void BlockLimiterConnection(PluginManager Plugins)
        {
            //Guid for BlockLimiter:
            Guid BlockLimiterGUID = new Guid("11fca5c4-01b6-4fc3-a215-602e2325be2b");
            Plugins.Plugins.TryGetValue(BlockLimiterGUID, out ITorchPlugin BlockLimiterT);

            if (BlockLimiterT != null)
            {
                Hangar.Debug("Plugin: " + BlockLimiterT.Name + " " + BlockLimiterT.Version + " is installed!");
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
        }


        private void GridBackupConnection(PluginManager Plugins)
        {
            Guid GridBackupGUID = new Guid("75e99032-f0eb-4c0d-8710-999808ed970c");
            Plugins.Plugins.TryGetValue(GridBackupGUID, out ITorchPlugin BlockLimiterT);

            if (BlockLimiterT != null)
            {
                Hangar.Debug("Plugin: " + BlockLimiterT.Name + " " + BlockLimiterT.Version + " is installed!");
                try
                {
                    //Grab refrence to TorchPluginBase class in the plugin
                    GridBackup = BlockLimiterT;


                    if(GridBackup != null)
                    {
                        Log.Debug("Successfully attached to GridBackup!");
                    }


                    //Example Method call
                    //object value = CandAddMethod.Invoke(Class, new object[] { grid });
                    //Convert to value return type
                    Log.Info("GridBackup Reference added to backup grids upon hangar save!");

                }
                catch (Exception e)
                {
                    Log.Warn(e, "Could not connect to GridBackup Plugin.");
                }
            }
        }

        public void PluginDispose()
        {
            //Un register events
            if (Config.GridMarketEnabled && Market != null)
            {
                Market.Dispose();
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



