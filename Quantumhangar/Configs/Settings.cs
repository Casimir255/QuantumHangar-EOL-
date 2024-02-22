using System.Collections.ObjectModel;
using Torch;

namespace QuantumHangar
{

    public class Settings : ViewModel
    {
        private bool _enabled;
        public bool PluginEnabled { get => _enabled; set => SetValue(ref _enabled, value); }

        private bool _nexusApi;
        public bool NexusApi { get => _nexusApi; set => SetValue(ref _nexusApi, value); }

        private string _folderDirectory;
        public string FolderDirectory { get => _folderDirectory; set => SetValue(ref _folderDirectory, value); }

        private double _waitTime = 60;
        public double WaitTime { get => _waitTime; set => SetValue(ref _waitTime, value); }

        private double _distanceCheck = 30000;
        public double DistanceCheck { get => _distanceCheck; set => SetValue(ref _distanceCheck, value); }

        private double _gridDistanceCheck;
        public double GridDistanceCheck { get => _gridDistanceCheck; set => SetValue(ref _gridDistanceCheck, value); }

        private int _gridCheckMinBlock = 25;
        public int GridCheckMinBlock { get => _gridCheckMinBlock; set => SetValue(ref _gridCheckMinBlock, value); }

        private int _scripterHangarAmount = 6;
        public int ScripterHangarAmount { get => _scripterHangarAmount; set => SetValue(ref _scripterHangarAmount, value); }

        private int _normalHangarAmount = 2;
        public int NormalHangarAmount { get => _normalHangarAmount; set => SetValue(ref _normalHangarAmount, value); }

        private int _factionHangarAmount = 2;
        public int FactionHangarAmount { get => _factionHangarAmount; set => SetValue(ref _factionHangarAmount, value); }

        private int _allianceHangarAmount = 2;
        public int AllianceHangarAmount { get => _allianceHangarAmount; set => SetValue(ref _allianceHangarAmount, value); }



        private bool _enableBlackListBlocks;
        public bool EnableBlackListBlocks { get => _enableBlackListBlocks; set => SetValue(ref _enableBlackListBlocks, value); }


        //SaveGrid Currency
        private bool _requireCurrency;
        public bool RequireCurrency { get => _requireCurrency; set => SetValue(ref _requireCurrency, value); }


        private double _customLargeGridCurrency = 1;
        private double _customStaticGridCurrency = 1;
        private double _customSmallGridCurrency = 1;
        public double CustomLargeGridCurrency { get => _customLargeGridCurrency; set => SetValue(ref _customLargeGridCurrency, value); }
        public double CustomStaticGridCurrency { get => _customStaticGridCurrency; set => SetValue(ref _customStaticGridCurrency, value); }
        public double CustomSmallGridCurrency { get => _customSmallGridCurrency; set => SetValue(ref _customSmallGridCurrency, value); }
        private CostType _hangarSaveCostType = CostType.PerGrid;
        public CostType HangarSaveCostType { get => _hangarSaveCostType; set => SetValue(ref _hangarSaveCostType, value); }



        //LoadGrid Currency
        private bool _requireLoadCurrency;
        public bool RequireLoadCurrency { get => _requireLoadCurrency; set => SetValue(ref _requireLoadCurrency, value); }
        private double _loadLargeGridCurrency = 1;
        private double _loadStaticGridCurrency = 1;
        private double _loadSmallGridCurrency = 1;
        public double LoadLargeGridCurrency { get => _loadLargeGridCurrency; set => SetValue(ref _loadLargeGridCurrency, value); }
        public double LoadStaticGridCurrency { get => _loadStaticGridCurrency; set => SetValue(ref _loadStaticGridCurrency, value); }
        public double LoadSmallGridCurrency { get => _loadSmallGridCurrency; set => SetValue(ref _loadSmallGridCurrency, value); }

        private CostType _hangarLoadCostType = CostType.PerGrid;
        public CostType HangarLoadCostType { get => _hangarLoadCostType; set => SetValue(ref _hangarLoadCostType, value); }


        private LoadType _loadType = LoadType.ForceLoadMearPlayer;
        public LoadType LoadType { get => _loadType; set => SetValue(ref _loadType, value); }


        private double _loadRadius = 100;
        public double LoadRadius { get => _loadRadius; set => SetValue(ref _loadRadius, value); }

        private bool _requireLoadRadius = true;
        public bool RequireLoadRadius { get => _requireLoadRadius; set => SetValue(ref _requireLoadRadius, value); }

        private bool _digVoxels;
        public bool DigVoxels { get => _digVoxels; set => SetValue(ref _digVoxels, value); }

        private bool _blackListRadioButton;
        public bool SBlockLimits { get => _blackListRadioButton; set => SetValue(ref _blackListRadioButton, value); }

      
        private bool _enableSubGrids;
        public bool EnableSubGrids { get => _enableSubGrids; set => SetValue(ref _enableSubGrids, value); }

        //public HangarMarketConfigs MarketConfigs { get; set; }


        //Saving & Loading around a point:
        private ObservableCollection<ZoneRestrictions> _zoneRestrictions = new ObservableCollection<ZoneRestrictions>();
        public ObservableCollection<ZoneRestrictions> ZoneRestrictions { get => _zoneRestrictions; set => SetValue(ref _zoneRestrictions, value); }


        private ObservableCollection<HangarBlacklist> _autoHangarPlayerBlacklist = new ObservableCollection<HangarBlacklist>();
        public ObservableCollection<HangarBlacklist> AutoHangarPlayerBlacklist { get => _autoHangarPlayerBlacklist; set => SetValue(ref _autoHangarPlayerBlacklist, value); }

        private bool _autoHangarGrids;
        public bool AutoHangarGrids { get => _autoHangarGrids; set => SetValue(ref _autoHangarGrids, value); }
        private bool _autoHangarGridsByType;
        public bool AutoHangarGridsByType { get => _autoHangarGridsByType; set => SetValue(ref _autoHangarGridsByType, value); }

        private bool _deleteRespawnPods;
        public bool DeleteRespawnPods { get => _deleteRespawnPods; set => SetValue(ref _deleteRespawnPods, value); }

        private int _autoHangarDayAmount = 20;
        public int AutoHangarDayAmount { get => _autoHangarDayAmount; set => SetValue(ref _autoHangarDayAmount, value); }

        private int _autoHangarDayAmountStation = 20;
        public int AutoHangarDayAmountStation { get => _autoHangarDayAmountStation; set => SetValue(ref _autoHangarDayAmountStation, value); }

        private int _autoHangarDayAmountLargeGrid = 20;
        public int AutoHangarDayAmountLargeGrid { get => _autoHangarDayAmountLargeGrid; set => SetValue(ref _autoHangarDayAmountLargeGrid, value); }

        private int _autoHangarDayAmountSmallGrid = 20;
        public int AutoHangarDayAmountSmallGrid { get => _autoHangarDayAmountSmallGrid; set => SetValue(ref _autoHangarDayAmountSmallGrid, value); }

        private bool _hangarGridsFallenInPlanet;
        public bool HangarGridsFallenInPlanet { get => _hangarGridsFallenInPlanet; set => SetValue(ref _hangarGridsFallenInPlanet, value); }

        private bool _keepPlayersLargestGrid;
        public bool KeepPlayersLargestGrid { get => _keepPlayersLargestGrid; set => SetValue(ref _keepPlayersLargestGrid, value); }

        private bool _autoHangarStaticGrids = true;
        public bool AutoHangarStaticGrids { get => _autoHangarStaticGrids; set => SetValue(ref _autoHangarStaticGrids, value); }

        private bool _autoHangarLargeGrids = true;
        public bool AutoHangarLargeGrids { get => _autoHangarLargeGrids; set => SetValue(ref _autoHangarLargeGrids, value); }

        private bool _autoHangarSmallGrids = true;
        public bool AutoHangarSmallGrids { get => _autoHangarSmallGrids; set => SetValue(ref _autoHangarSmallGrids, value); }

        private bool _onLoadTransfer;
        public bool OnLoadTransfer { get => _onLoadTransfer; set => SetValue(ref _onLoadTransfer, value); }


        //Single slot Configs
        private int _singleMaxBlocks;
        public int SingleMaxBlocks { get => _singleMaxBlocks; set => SetValue(ref _singleMaxBlocks, value); }

        private int _singleMaxPcu = 0;
        public int SingleMaxPcu { get => _singleMaxPcu; set => SetValue(ref _singleMaxPcu, value); }

        private bool _allowStaticGrids = true;
        public bool AllowStaticGrids { get => _allowStaticGrids; set => SetValue(ref _allowStaticGrids, value); }

        private int _singleMaxStaticGrids = 0;
        public int SingleMaxStaticGrids { get => _singleMaxStaticGrids; set => SetValue(ref _singleMaxStaticGrids, value); }

        private bool _allowLargeGrids = true;
        public bool AllowLargeGrids { get => _allowLargeGrids; set => SetValue(ref _allowLargeGrids, value); }

        private int _singleMaxLargeGrids = 0;
        public int SingleMaxLargeGrids { get => _singleMaxLargeGrids; set => SetValue(ref _singleMaxLargeGrids, value); }

        private bool _allowSmallGrids = true;
        public bool AllowSmallGrids { get => _allowSmallGrids; set => SetValue(ref _allowSmallGrids, value); }

        private int _singleMaxSmallGrids = 0;
        public int SingleMaxSmallGrids { get => _singleMaxSmallGrids; set => SetValue(ref _singleMaxSmallGrids, value); }

        //Max Configs
        private int _playerMaxBlocks = 0;
        public int PlayerMaxBlocks { get => _playerMaxBlocks; set => SetValue(ref _playerMaxBlocks, value); }

        private int _playerMaxPCU = 0;
        public int PlayerMaxPCU { get => _playerMaxPCU; set => SetValue(ref _playerMaxPCU, value); }

        private int _playerMaxStaticGrids = 0;
        public int PlayerMaxStaticGrids { get => _playerMaxStaticGrids; set => SetValue(ref _playerMaxStaticGrids, value); }

        private int _playerMaxLargeGrids = 0;
        public int PlayerMaxLargeGrids { get => _playerMaxLargeGrids; set => SetValue(ref _playerMaxLargeGrids, value); }

        private int _playerMaxSmallGrids = 0;
        public int PlayerMaxSmallGrids { get => _playerMaxSmallGrids; set => SetValue(ref _playerMaxSmallGrids, value); }

        // Other configs
        private bool _allowInGravity = true;
        public bool AllowInGravity { get => _allowInGravity; set => SetValue(ref _allowInGravity, value); }

        private double _maxGravityAmount = 0;
        public double MaxGravityAmount { get => _maxGravityAmount; set => SetValue(ref _maxGravityAmount, value); }


        private bool _gridMarketEnabled = false;
        public bool GridMarketEnabled { get => _gridMarketEnabled; set => SetValue(ref _gridMarketEnabled, value); }


        private double _staticGridMarketMultiplier = 1;
        public double StaticGridMarketMultiplier { get => _staticGridMarketMultiplier; set => SetValue(ref _staticGridMarketMultiplier, value); }

        private double _largeGridMarketMultiplier = 1;
        public double LargeGridMarketMultiplier { get => _largeGridMarketMultiplier; set => SetValue(ref _largeGridMarketMultiplier, value); }

        private double _smallGridMarketMultiplier = 1;
        public double SmallGridMarketMultiplier { get => _smallGridMarketMultiplier; set => SetValue(ref _smallGridMarketMultiplier, value); }

        private double _autoSellDiscountPricePercent = .75;
        public double AutoSellDiscountPricePercent { get => _autoSellDiscountPricePercent; set => SetValue(ref _autoSellDiscountPricePercent, value); }

        private int _sellAfkDayAmount = 30;
        public int SellAfkDayAmount { get => _sellAfkDayAmount; set => SetValue(ref _sellAfkDayAmount, value); }


        private bool _advancedDebug;
        public bool AdvancedDebug { get => _advancedDebug; set => SetValue(ref _advancedDebug, value); }


        private double _restockAmount = 1000;
        public double RestockAmount { get => _restockAmount; set => SetValue(ref _restockAmount, value); }


        private ulong _marketUpdateChannel;
        public ulong MarketUpdateChannel { get => _marketUpdateChannel; set => SetValue(ref _marketUpdateChannel, value); }


        //private ObservableCollection<MarketListing> _PublicMarketOffers = new ObservableCollection<MarketListing>();
        //public ObservableCollection<MarketListing> PublicMarketOffers { get => _PublicMarketOffers; set => SetValue(ref _PublicMarketOffers, value); }


        private bool _autosellHangarGrids;
        public bool AutosellHangarGrids { get => _autosellHangarGrids; set => SetValue(ref _autosellHangarGrids, value); }


        private bool _allowLoadNearEnemy;
        public bool AllowLoadNearEnemy { get => _allowLoadNearEnemy; set => SetValue(ref _allowLoadNearEnemy, value); }
    }
}
