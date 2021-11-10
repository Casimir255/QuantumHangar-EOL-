using QuantumHangar.HangarMarket;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Torch;

namespace QuantumHangar
{
    public class Settings : ViewModel
    {
        private bool _Enabled = false;
        public bool PluginEnabled { get => _Enabled; set => SetValue(ref _Enabled, value); }

        private bool _NexusAPI = false;
        public bool NexusAPI { get => _NexusAPI; set => SetValue(ref _NexusAPI, value); }

        private string _FolderDirectory;
        public string FolderDirectory { get => _FolderDirectory; set => SetValue(ref _FolderDirectory, value); }

        private double _WaitTime = 60;
        public double WaitTime { get => _WaitTime; set => SetValue(ref _WaitTime, value); }

        private double _DistanceCheck = 30000;
        public double DistanceCheck { get => _DistanceCheck; set => SetValue(ref _DistanceCheck, value); }

        private double _GridDistanceCheck = 0;
        public double GridDistanceCheck { get => _GridDistanceCheck; set => SetValue(ref _GridDistanceCheck, value); }

        private int _GridCheckMinBlock = 25;
        public int GridCheckMinBlock { get => _GridCheckMinBlock; set => SetValue(ref _GridCheckMinBlock, value); }

        private int _ScripterHangarAmount = 6;
        public int ScripterHangarAmount { get => _ScripterHangarAmount; set => SetValue(ref _ScripterHangarAmount, value); }

        private int _NormalHangarAmount = 2;
        public int NormalHangarAmount { get => _NormalHangarAmount; set => SetValue(ref _NormalHangarAmount, value); }


        private bool _EnableBlackListBlocks = false;
        public bool EnableBlackListBlocks { get => _EnableBlackListBlocks; set => SetValue(ref _EnableBlackListBlocks, value); }


        //SaveGrid Currency
        private bool _RequireCurrency = false;
        public bool RequireCurrency { get => _RequireCurrency; set => SetValue(ref _RequireCurrency, value); }


        private double _CustomLargeGridCurrency = 1;
        private double _CustomStaticGridCurrency = 1;
        private double _CustomSmallGridCurrency = 1;
        public double CustomLargeGridCurrency { get => _CustomLargeGridCurrency; set => SetValue(ref _CustomLargeGridCurrency, value); }
        public double CustomStaticGridCurrency { get => _CustomStaticGridCurrency; set => SetValue(ref _CustomStaticGridCurrency, value); }
        public double CustomSmallGridCurrency { get => _CustomSmallGridCurrency; set => SetValue(ref _CustomSmallGridCurrency, value); }
        private CostType _HangarSaveCostType = CostType.PerGrid;
        public CostType HangarSaveCostType { get => _HangarSaveCostType; set => SetValue(ref _HangarSaveCostType, value); }



        //LoadGrid Currency
        private bool _RequireLoadCurrency = false;
        public bool RequireLoadCurrency { get => _RequireLoadCurrency; set => SetValue(ref _RequireLoadCurrency, value); }
        private double _LoadLargeGridCurrency = 1;
        private double _LoadStaticGridCurrency = 1;
        private double _LoadSmallGridCurrency = 1;
        public double LoadLargeGridCurrency { get => _LoadLargeGridCurrency; set => SetValue(ref _LoadLargeGridCurrency, value); }
        public double LoadStaticGridCurrency { get => _LoadStaticGridCurrency; set => SetValue(ref _LoadStaticGridCurrency, value); }
        public double LoadSmallGridCurrency { get => _LoadSmallGridCurrency; set => SetValue(ref _LoadSmallGridCurrency, value); }

        private CostType _HangarLoadCostType = CostType.PerGrid;
        public CostType HangarLoadCostType { get => _HangarLoadCostType; set => SetValue(ref _HangarLoadCostType, value); }


        private LoadType _LoadType = LoadType.ForceLoadMearPlayer;
        public LoadType LoadType { get => _LoadType; set => SetValue(ref _LoadType, value); }


        private double _LoadRadius = 100;
        public double LoadRadius { get => _LoadRadius; set => SetValue(ref _LoadRadius, value); }

        private bool _RequireLoadRadius = true;
        public bool RequireLoadRadius { get => _RequireLoadRadius; set => SetValue(ref _RequireLoadRadius, value); }

        private bool _DigVoxels = false;
        public bool DigVoxels { get => _DigVoxels; set => SetValue(ref _DigVoxels, value); }

        private bool _BlackListRadioButton = false;
        public bool SBlockLimits { get => _BlackListRadioButton; set => SetValue(ref _BlackListRadioButton, value); }

      
        private bool _EnableSubGrids = false;
        public bool EnableSubGrids { get => _EnableSubGrids; set => SetValue(ref _EnableSubGrids, value); }

        //public HangarMarketConfigs MarketConfigs { get; set; }


        //Saving & Loading around a point:
        private ObservableCollection<ZoneRestrictions> _ZoneRestrictions = new ObservableCollection<ZoneRestrictions>();
        public ObservableCollection<ZoneRestrictions> ZoneRestrictions { get => _ZoneRestrictions; set => SetValue(ref _ZoneRestrictions, value); }


        private ObservableCollection<HangarBlacklist> _AutoHangarPlayerBlacklist = new ObservableCollection<HangarBlacklist>();
        public ObservableCollection<HangarBlacklist> AutoHangarPlayerBlacklist { get => _AutoHangarPlayerBlacklist; set => SetValue(ref _AutoHangarPlayerBlacklist, value); }

        private bool _AutoHangarGrids = false;
        public bool AutoHangarGrids { get => _AutoHangarGrids; set => SetValue(ref _AutoHangarGrids, value); }

        private bool _DeleteRespawnPods = false;
        public bool DeleteRespawnPods { get => _DeleteRespawnPods; set => SetValue(ref _DeleteRespawnPods, value); }

        private int _AutoHangarDayAmount = 20;
        public int AutoHangarDayAmount { get => _AutoHangarDayAmount; set => SetValue(ref _AutoHangarDayAmount, value); }

        private bool _HangarGridsFallenInPlanet = false;
        public bool HangarGridsFallenInPlanet { get => _HangarGridsFallenInPlanet; set => SetValue(ref _HangarGridsFallenInPlanet, value); }

        private bool _KeepPlayersLargestGrid = false;
        public bool KeepPlayersLargestGrid { get => _KeepPlayersLargestGrid; set => SetValue(ref _KeepPlayersLargestGrid, value); }

        private bool _AutoHangarStaticGrids = true;
        public bool AutoHangarStaticGrids { get => _AutoHangarStaticGrids; set => SetValue(ref _AutoHangarStaticGrids, value); }

        private bool _AutoHangarLargeGrids = true;
        public bool AutoHangarLargeGrids { get => _AutoHangarLargeGrids; set => SetValue(ref _AutoHangarLargeGrids, value); }

        private bool _AutoHangarSmallGrids = true;
        public bool AutoHangarSmallGrids { get => _AutoHangarSmallGrids; set => SetValue(ref _AutoHangarSmallGrids, value); }

        private bool _OnLoadTransfer = false;
        public bool OnLoadTransfer { get => _OnLoadTransfer; set => SetValue(ref _OnLoadTransfer, value); }


        //Single slot
        private int _SingleMaxBlocks = 0;
        public int SingleMaxBlocks { get => _SingleMaxBlocks; set => SetValue(ref _SingleMaxBlocks, value); }

        private int _SingleMaxPCU = 0;
        public int SingleMaxPCU { get => _SingleMaxPCU; set => SetValue(ref _SingleMaxPCU, value); }

        private bool _AllowStaticGrids = true;
        public bool AllowStaticGrids { get => _AllowStaticGrids; set => SetValue(ref _AllowStaticGrids, value); }

        private int _SingleMaxStaticGrids = 0;
        public int SingleMaxStaticGrids { get => _SingleMaxStaticGrids; set => SetValue(ref _SingleMaxStaticGrids, value); }

        private bool _AllowLargeGrids = true;
        public bool AllowLargeGrids { get => _AllowLargeGrids; set => SetValue(ref _AllowLargeGrids, value); }

        private int _SingleMaxLargeGrids = 0;
        public int SingleMaxLargeGrids { get => _SingleMaxLargeGrids; set => SetValue(ref _SingleMaxLargeGrids, value); }

        private bool _AllowSmallGrids = true;
        public bool AllowSmallGrids { get => _AllowSmallGrids; set => SetValue(ref _AllowSmallGrids, value); }

        private int _SingleMaxSmallGrids = 0;
        public int SingleMaxSmallGrids { get => _SingleMaxSmallGrids; set => SetValue(ref _SingleMaxSmallGrids, value); }


        //Max Configs
        private int _TotalMaxBlocks = 0;
        public int TotalMaxBlocks { get => _TotalMaxBlocks; set => SetValue(ref _TotalMaxBlocks, value); }

        private int _TotalMaxPCU = 0;
        public int TotalMaxPCU { get => _TotalMaxPCU; set => SetValue(ref _TotalMaxPCU, value); }

        private int _TotalMaxStaticGrids = 0;
        public int TotalMaxStaticGrids { get => _TotalMaxStaticGrids; set => SetValue(ref _TotalMaxStaticGrids, value); }

        private int _TotalMaxLargeGrids = 0;
        public int TotalMaxLargeGrids { get => _TotalMaxLargeGrids; set => SetValue(ref _TotalMaxLargeGrids, value); }

        private int _TotalMaxSmallGrids = 0;
        public int TotalMaxSmallGrids { get => _TotalMaxSmallGrids; set => SetValue(ref _TotalMaxSmallGrids, value); }


        private bool _AllowInGravity = true;
        public bool AllowInGravity { get => _AllowInGravity; set => SetValue(ref _AllowInGravity, value); }

        private double _MaxGravityAmount = 0;
        public double MaxGravityAmount { get => _MaxGravityAmount; set => SetValue(ref _MaxGravityAmount, value); }







        private bool _GridMarketEnabled = false;
        public bool GridMarketEnabled { get => _GridMarketEnabled; set => SetValue(ref _GridMarketEnabled, value); }


        private double _StaticGridMarketMultiplier = 1;
        public double StaticGridMarketMultiplier { get => _StaticGridMarketMultiplier; set => SetValue(ref _StaticGridMarketMultiplier, value); }

        private double _LargeGridMarketMultiplier = 1;
        public double LargeGridMarketMultiplier { get => _LargeGridMarketMultiplier; set => SetValue(ref _LargeGridMarketMultiplier, value); }

        private double _SmallGridMarketMultiplier = 1;
        public double SmallGridMarketMultiplier { get => _SmallGridMarketMultiplier; set => SetValue(ref _SmallGridMarketMultiplier, value); }

        private double _AutoSellDiscountPricePercent = .75;
        public double AutoSellDiscountPricePercent { get => _AutoSellDiscountPricePercent; set => SetValue(ref _AutoSellDiscountPricePercent, value); }

        private int _SellAFKDayAmount = 30;
        public int SellAFKDayAmount { get => _SellAFKDayAmount; set => SetValue(ref _SellAFKDayAmount, value); }


        private bool _AdvancedDebug = false;
        public bool AdvancedDebug { get => _AdvancedDebug; set => SetValue(ref _AdvancedDebug, value); }


        private double _RestockAmount = 1000;
        public double RestockAmount { get => _RestockAmount; set => SetValue(ref _RestockAmount, value); }



        private ObservableCollection<MarketListing> _PublicMarketOffers = new ObservableCollection<MarketListing>();
        public ObservableCollection<MarketListing> PublicMarketOffers { get => _PublicMarketOffers; set => SetValue(ref _PublicMarketOffers, value); }


        private bool _AutosellHangarGrids = false;
        public bool AutosellHangarGrids { get => _AutosellHangarGrids; set => SetValue(ref _AutosellHangarGrids, value); }

    }
}
