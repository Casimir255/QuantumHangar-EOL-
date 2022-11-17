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
using QuantumHangar.Utils;
using QuantumHangar.HangarMarket;

namespace QuantumHangar
{
    public class Hangar : TorchPluginBase, IWpfPlugin
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static Settings Config => _config?.Data;
        private static Persistent<Settings> _config;
        public static Dictionary<long, CurrentCooldown> ConfirmationsMap { get; } = new Dictionary<long, CurrentCooldown>();


        public TorchSessionManager TorchSession { get; private set; }
        public static bool ServerRunning { get; private set; }

        public static string MainPlayerDirectory { get; private set; }


        public enum ErrorType
        {
            Debug,
            Fatal,
            Trace,
            Warn
        }


        public UserControl Control;
        public UserControl GetControl() => Control ?? (Control = new UserControlInterface());

        private HangarMarketController _controller;


        public override void Init(ITorchBase torch)
        {
            //Settings S = new Settings();

            base.Init(torch);
            //Grab Settings
            string path = Path.Combine(StoragePath, "QuantumHangar.cfg");

            _config = Persistent<Settings>.Load(path);

            if (Config.FolderDirectory == null || Config.FolderDirectory == "")
            {
                Config.FolderDirectory = Path.Combine(StoragePath, "QuantumHangar");
                Directory.CreateDirectory(Config.FolderDirectory);
            }

            MainPlayerDirectory = Path.Combine(Config.FolderDirectory, "PlayerHangars");
            Directory.CreateDirectory(MainPlayerDirectory);


            TorchSession = Torch.Managers.GetManager<TorchSessionManager>();
            if (TorchSession != null)
                TorchSession.SessionStateChanged += SessionChanged;


            if (Config.GridMarketEnabled)
            {
                _controller = new HangarMarketController();
            }
            else
            {
                Log.Info("Starting plugin WITHOUT the Hangar Market!");
            }



            PatchManager manager = DependencyProviderExtensions.GetManager<PatchManager>(Torch.Managers);
            Patcher patcher = new Patcher();
            patcher.Apply(manager.AcquireContext(), this);
            //Load files

            MigrateHangar();
        }

        private void MigrateHangar()
        {
  

            string[] dirs = Directory.GetDirectories(Config.FolderDirectory, "*", SearchOption.TopDirectoryOnly);
      
            foreach (string dir in dirs)
            {
                DirectoryInfo info = new DirectoryInfo(dir);

                if(UInt64.TryParse(info.Name, out _))
                {
                    Log.Warn("Moving!");

                    string destination = Path.Combine(MainPlayerDirectory, info.Name);
                    Log.Warn($"Destination: {destination}");

                    Directory.Move(dir, destination);

                }
            }
        }



        private void SessionChanged(ITorchSession session, TorchSessionState state)
        {
            switch (state)
            {

                case TorchSessionState.Loading:
                    Hangar.Config.RefreshModel();
                    break;

                case TorchSessionState.Loaded:

                    //MP = Torch.CurrentSession.Managers.GetManager<MultiplayerManagerBase>();
                    //ChatManager = Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
                    PluginManager plugins = Torch.CurrentSession.Managers.GetManager<PluginManager>();
                    PluginDependencies.InitPluginDependencies(plugins);
                    ServerRunning = true;
                    AutoHangar.StartAutoHangar();
                    _controller?.ServerStarted();
                    break;

                    
                case TorchSessionState.Unloading:
                    ServerRunning = false;
                    PluginDispose();
                    break;


            }
        }

        // 60 frames =~ 1 sec, run update about every min
        int _maxUpdateTime = 60 * 60;
        int _currentFrameCount = 0;

        public override void Update()
        {
            if (_currentFrameCount >= _maxUpdateTime)
            {
                Update1Min();
                _currentFrameCount = 0;
                return;
            }

            _currentFrameCount++;
        }

        public void Update1Min()
        {
            AutoHangar.UpdateAutoHangar();
        }



        public void PluginDispose()
        {
            _controller?.Close();
            AutoHangar.Dispose();
            PluginDependencies.Dispose();
        }
    }


    public class CurrentCooldown
    {

        private long _startTime;
        //private long _currentCooldown;

        private string _grid;
        public void StartCooldown(string command)
        {
            this._grid = command;
            _startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
        public bool CheckCommandStatus(string command)
        {

            if (this._grid != command)
                return true;

            long elapsedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _startTime;

            if (elapsedTime >= 30000)
                return true;

            return false;

        }
    }


}



