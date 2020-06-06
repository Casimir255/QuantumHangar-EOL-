using Newtonsoft.Json;
using NLog;
using QuantumHangar.Utilities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using VRage.Game;
using VRage.ObjectBuilders;

namespace QuantumHangar.UI
{
    /// <summary>
    /// Interaction logic for UserControlInterface.xaml
    /// </summary>
    public partial class UserControlInterface : UserControl
    {
        private static readonly Logger Log = LogManager.GetLogger("QuantumHangarUI");
        //private bool GridLoaded = false;
        private Hangar Plugin { get; }



        public UserControlInterface()
        {
            InitializeComponent();
        }

        public UserControlInterface(Hangar plugin) : this()
        {
            Plugin = plugin;
            DataContext = plugin.Config;

            //plugin.PublicOffersAdded.CollectionChanged += PublicOffersAdded_CollectionChanged;

            plugin.Config.PublicOffers.CollectionChanged += PublicOffers_CollectionChanged;
        }

        private void PublicOffers_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Hangar.Debug(e.Action.ToString());
            //Plugin.Config.RefreshModel();
            //throw new NotImplementedException();
        }

        private void TabController_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }






        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            //Refresh list
            string[] subdirectoryEntries = null;

            try
            {
                subdirectoryEntries = Directory.GetFiles(Plugin.Market.ServerOffersDir, "*.sbc");
            }
            catch (Exception b)
            {
                //Prevent from continuing
                Hangar.Debug("Unable to get ServerOffersFolder!", b, Hangar.ErrorType.Fatal);
                return;
            }

            //Get only filenames
            string[] FolderGrids = new string[subdirectoryEntries.Count()];
            for(int i = 0; i< subdirectoryEntries.Count(); i++)
            {
                FolderGrids[i] = System.IO.Path.GetFileNameWithoutExtension(subdirectoryEntries[i]);
            }


            for(int i = 0; i< Plugin.Config.PublicOffers.Count; i++)
            {
                if (!FolderGrids.Contains(Plugin.Config.PublicOffers[i].Name))
                {
                    Plugin.Config.PublicOffers.RemoveAt(i);
                }
            }


            for(int i = 0; i < FolderGrids.Count(); i++)
            {
                if(!Plugin.Config.PublicOffers.Any(x=> x.Name == FolderGrids[i])){

                    string FileName = FolderGrids[i];

                    Hangar.Debug("Adding item: " + FileName);

                    PublicOffers offer = new PublicOffers();
                    offer.Name = FileName;
                    Plugin.Config.PublicOffers.Add(offer);
                    Plugin.Config.RefreshModel();
                }
            }

        }

        private void Imported_LostFocus(object sender, RoutedEventArgs e)
        {
            Plugin.Config.RefreshModel();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var t = Task.Run(() => UpdatePublicOffers());
            //This will force the market to update the market items
        }

        private void UpdatePublicOffers()
        {

            //Update all public offer market items!
            if(!Hangar.IsRunning)
            {
                return;
            }

            //Need to remove existing ones

            string PublicOfferPath = Plugin.Market.ServerOffersDir;

            //Clear list
            GridMarket.PublicOfferseGridList.Clear();


            foreach (PublicOffers offer in Plugin.Config.PublicOffers)
            {
                if (offer.Forsale && (offer.Name != null || offer.Name != ""))
                {
                    

                    string GridFilePath = System.IO.Path.Combine(PublicOfferPath, offer.Name + ".sbc");
                    Hangar.Debug("Blueprint Path: "+ GridFilePath);

                    MyObjectBuilderSerializer.DeserializeXML(GridFilePath, out MyObjectBuilder_Definitions myObjectBuilder_Definitions);
                    MyObjectBuilder_ShipBlueprintDefinition[] shipBlueprint = null;
                    try
                    {
                        shipBlueprint = myObjectBuilder_Definitions.ShipBlueprints;
                    }
                    catch
                    {
                        Hangar.Debug("Error on BP: "+ offer.Name + "! Most likely you put in the SBC5 file! Dont do that!");
                        continue;
                    }
                    MyObjectBuilder_CubeGrid grid = shipBlueprint[0].CubeGrids[0];
                    byte[] Definition = MyAPIGateway.Utilities.SerializeToBinary(grid);

                    HangarChecks.GetPublicOfferBPDetails(shipBlueprint, out GridStamp stamp);


                    //Straight up add new ones
                    MarketList NewList = new MarketList();
                    NewList.Steamid = 0;
                    NewList.Name = offer.Name;
                    NewList.Seller = offer.Seller;
                    NewList.SellerFaction = offer.SellerFaction;
                    NewList.Price = offer.Price;
                    NewList.Description = offer.Description;
                    NewList.GridDefinition = Definition;
                    

                    NewList.GridBuiltPercent = stamp.GridBuiltPercent;
                    NewList.NumberofBlocks = stamp.NumberofBlocks;
                    NewList.SmallGrids = stamp.SmallGrids;
                    NewList.LargeGrids = stamp.LargeGrids;
                    NewList.BlockTypeCount = stamp.BlockTypeCount;


                    //Need to setTotalBuys
                    GridMarket.PublicOfferseGridList.Add(NewList);

                   Hangar.Debug("Adding new public offer: " + offer.Name);
                }
            }


            //Update Everything!
            Comms.SendListToModOnInitilize();

            MarketData Data = new MarketData();
            Data.List = GridMarket.PublicOfferseGridList;

            //Write to file
            FileSaver.Save(Plugin.Market.ServerMarketFileDir, Data);
            //File.WriteAllText(Main.ServerMarketFileDir, JsonConvert.SerializeObject(Data));
            //This will force the market to update the market items
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;

            if(button.Name == "FixedPrice")
            {
                StaticGBox.Visibility = Visibility.Visible;
                StaticGText.Visibility = Visibility.Visible;
                StaticGText.Text = "Fixed Price Amount:";

                LargeGBox.Visibility = Visibility.Hidden;
                LargeGText.Visibility = Visibility.Hidden;

                SmallGBox.Visibility = Visibility.Hidden;
                SmallGText.Visibility = Visibility.Hidden;


            }
            else
            {
                StaticGBox.Visibility = Visibility.Visible;
                StaticGText.Visibility = Visibility.Visible;
                StaticGText.Text = "Static Grid Currency:";

                LargeGBox.Visibility = Visibility.Visible;
                LargeGText.Visibility = Visibility.Visible;

                SmallGBox.Visibility = Visibility.Visible;
                SmallGText.Visibility = Visibility.Visible;
            }
        }


        private void RadioButtonLoad_Checked(object sender, RoutedEventArgs e)
        {
            var button = sender as RadioButton;

            if (button.Name == "FixedPriceLoad")
            {
                StaticLGBox.Visibility = Visibility.Visible;
                StaticLGText.Visibility = Visibility.Visible;
                StaticLGText.Text = "Fixed Price Amount:";

                LargeLGBox.Visibility = Visibility.Hidden;
                LargeLGText.Visibility = Visibility.Hidden;

                SmallLGBox.Visibility = Visibility.Hidden;
                SmallLGText.Visibility = Visibility.Hidden;


            }
            else if (button.Name == "BlockCountLoad")
            {
                StaticLGBox.Visibility = Visibility.Visible;
                StaticLGText.Visibility = Visibility.Visible;
                StaticLGText.Text = "Price PerBlock Amount:";

                LargeLGBox.Visibility = Visibility.Hidden;
                LargeLGText.Visibility = Visibility.Hidden;

                SmallLGBox.Visibility = Visibility.Hidden;
                SmallLGText.Visibility = Visibility.Hidden;
            }
            else
            {
                StaticLGBox.Visibility = Visibility.Visible;
                StaticLGText.Visibility = Visibility.Visible;
                StaticLGText.Text = "Static Grid Currency:";

                LargeLGBox.Visibility = Visibility.Visible;
                LargeLGText.Visibility = Visibility.Visible;

                SmallLGBox.Visibility = Visibility.Visible;
                SmallLGText.Visibility = Visibility.Visible;
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

            Process.Start(Plugin.Market.ServerOffersDir);
        }



        private void AddNewGpsButton(object sender, RoutedEventArgs e)
        {
            foreach (Match item in Regex.Matches(GpsTextBox.Text, Utilis.m_ScanPattern))
            {
                ZoneRestrictions r = new ZoneRestrictions();

                string value = item.Groups[1].Value;
                double value2;
                double value3;
                double value4;
                try
                {
                    value2 = double.Parse(item.Groups[2].Value, CultureInfo.InvariantCulture);
                    value2 = Math.Round(value2, 2);
                    value3 = double.Parse(item.Groups[3].Value, CultureInfo.InvariantCulture);
                    value3 = Math.Round(value3, 2);
                    value4 = double.Parse(item.Groups[4].Value, CultureInfo.InvariantCulture);
                    value4 = Math.Round(value4, 2);
                }
                catch (SystemException)
                {
                    continue;
                }

                r.Name = value;

                r.X = value2;
                r.Y = value3;
                r.Z = value4;

                Log.Warn(r.X);

                r.Radius = 100;
                r.AllowLoading = true;
                r.AllowSaving = true;

                Plugin.Config.ZoneRestrictions.Add(r);

                GpsTextBox.Text = "";

                Plugin.Config.RefreshModel();

                return;
            }
        }

        private void DeleteZone(object sender, RoutedEventArgs e)
        {
            ZoneRestrictions selectedItem = (ZoneRestrictions)ZoneRestrictions.SelectedItem;
            if (selectedItem != null)
            {
                Plugin.Config.ZoneRestrictions.Remove(selectedItem);
                Plugin.Config.RefreshModel();

            }
        }

        private void ZoneGridLostFocust(object sender, RoutedEventArgs e)
        {
            Plugin.Config.RefreshModel();
        }






        private void AddNewBlacklistPlayer(object sender, RoutedEventArgs e)
        {
            //Update all public offer market items!
            if (!Hangar.IsRunning)
            {
                HangarBlacklist b = new HangarBlacklist();
                b.Name = AutoHangarNameBox.Text;

                Plugin.Config.AutoHangarPlayerBlacklist.Add(b);
            }
            else
            {
                //Server is running. We can attempt to get steamID
                HangarBlacklist b = new HangarBlacklist();
                b.Name = AutoHangarNameBox.Text;

                /*
                foreach(MyIdentity player in MySession.Static.Players.GetAllIdentities())
                {
                    Log.Info(player.DisplayName);
                }
                */

                try
                {
                    MyIdentity player = MySession.Static.Players.GetAllIdentities().First(x => x.DisplayName.Equals(AutoHangarNameBox.Text));
                    b.SteamID = MySession.Static.Players.TryGetSteamId(player.IdentityId);
                    
                }
                catch
                {
                    Log.Warn("Unable to find given character. Enter SteamID in manually");
                }
                Plugin.Config.AutoHangarPlayerBlacklist.Add(b);
            }
            AutoHangarNameBox.Text = "";
            Plugin.Config.RefreshModel();
        }

        private void DeletePlayer(object sender, RoutedEventArgs e)
        {
            HangarBlacklist selectedItem = (HangarBlacklist)AutoHangarBlacklist.SelectedItem;
            if (selectedItem != null)
            {
                Plugin.Config.AutoHangarPlayerBlacklist.Remove(selectedItem);
                Plugin.Config.RefreshModel();
            }
        }

        private void PlayerBlacklistLostFocus(object sender, RoutedEventArgs e)
        {
            Plugin.Config.RefreshModel();
        }
    }

}

namespace QuantumHangar.Converters
{
    public class EnumBooleanConverter : IValueConverter
    {
        #region IValueConverter Members
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string parameterString = parameter as string;
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            if (Enum.IsDefined(value.GetType(), value) == false)
                return DependencyProperty.UnsetValue;

            object parameterValue = Enum.Parse(value.GetType(), parameterString);

            return parameterValue.Equals(value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string parameterString = parameter as string;
            if (parameterString == null)
                return DependencyProperty.UnsetValue;

            return Enum.Parse(targetType, parameterString);
        }
        #endregion
    }

    
    public class TextToBoolConverter : IValueConverter
    {
        #region IValueConverter Members

        public object Convert(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                return !(bool)value;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter,
            System.Globalization.CultureInfo culture)
        {
            if (value is bool)
            {
                return !(bool)value;
            }
            return value;
        }

        #endregion
    }
    

}
